namespace FsRX

open System


type Event<'T> =
    | Next of 'T
    | Completed
    | Error of Exception

type Observer<'T> =
    | Observer of (Event<'T> -> unit)
    member this.Notify(e) =
        let (Observer o) = this
        o e
            
    static member Create<'T>(onNext: 'T -> unit, (?onError: Exception -> unit), (?onCompleted: unit -> unit)) =

        let onCompleted = defaultArg onCompleted (fun () -> ())
        let onError = defaultArg onError (fun _ -> ())

        (fun e ->
            match e with
            | Next value -> value |> onNext
            | Completed -> onCompleted ()
            | Error e -> e |> onError)

        |> Observer

    // static member Create<'T>(onNext: 'T -> unit) =
    //     Observer.Create(onNext, (fun _ -> ()), (fun _ -> ()))

    static member CreateForwarding<'T>
        (
            observerToForward: Observer<'T>,
            (?onNext: 'T -> unit),
            (?onCompleted: unit -> unit),
            (?onError: Exception -> unit)
        ) =
        let onCompleted =
            defaultArg onCompleted (fun () -> observerToForward.Notify(Completed))

        let onError =
            defaultArg onError (fun e -> observerToForward.Notify(e |> Error))

        let onNext =
            defaultArg onNext (fun v -> observerToForward.Notify(v |> Next))

        Observer.Create(onNext, onError, onCompleted)

type Observable<'T> =
    | Subscribe of (Observer<'T> -> IDisposable)
    member this.SubscribeWith(observer) =
        let (Subscribe s) = this
        observer |> s

module Disposable =
    let empty () =
        { new IDisposable with
            member this.Dispose() = () }

    let create action =
        { new IDisposable with
            member this.Dispose() = action () }
    let composite (disposables: list<IDisposable>) = 
      { new IDisposable with
            member this.Dispose() = disposables |>  List.iter (fun d -> d.Dispose()) }
//[<AutoOpen>]
[<AutoOpen>]
module Functions =
    open System
    open System.Threading
    open System.Threading.Tasks

    // let private protect observable call =
    //     try
    //         call()
    //     with
    //         | :? SystemException as e ->
    let private createObserver<'T> (onNext: 'T -> unit) = Observer.Create(onNext)

    let fromSeq seq =
        (fun (observer: Observer<'T>) ->
            seq
            |> Seq.iter (fun v -> observer.Notify(v |> Next))

            observer.Notify(Completed)
            Disposable.empty ())
        |> Subscribe

    let ret value = fromSeq (Seq.singleton value)
    let empty () = fromSeq Seq.empty

    let never () =
        (fun _ -> Disposable.empty ()) |> Subscribe

    let throw e =
        (fun (observer: Observer<'T>) ->
            observer.Notify(e |> Error)
            Disposable.empty ())
        |> Subscribe

    let delay (period: TimeSpan) (observable: Observable<'T>) =
        fun (observer: Observer<'T>) ->
            let task =
                Task
                    .Delay(period)
                    .ContinueWith(fun _ -> observer |> observable.SubscribeWith |> ignore)

            Disposable.empty ()
        |> Subscribe

    let generate2
        (initial: 'TState)
        condition
        iter
        (resultSelector: 'TState -> 'T)
        (timeSelector: 'TState -> TimeSpan)
        =
        fun (observer: Observer<'T>) ->

            let rec generateInernal (curState) =
                if curState |> condition |> not then
                    observer.Notify(Completed)
                else
                    let observable =
                        curState
                        |> resultSelector
                        |> ret
                        |> delay (curState |> timeSelector)

                    let subs =
                        Observer.Create(
                            (fun v ->
                                do observer.Notify(v |> Next)
                                let subscription = generateInernal (curState |> iter)
                                ()),
                            (fun e -> do observer.Notify(e |> Error))
                        )
                        |> observable.SubscribeWith

                    ()

                Disposable.empty ()

            generateInernal initial
        |> Subscribe

    let generate (initial: 'TState) condition iter resultSelector =
        Seq.unfold
            (fun s ->
                if condition s then
                    (s |> resultSelector, s |> iter) |> Some
                else
                    None)
            initial
        |> fromSeq

    let interval period =
        generate2 0 (fun _ -> true) (fun i -> i + 1) id (fun _ -> period)

    let range min max =
        generate min (fun cur -> cur < max) (fun cur -> cur + 1) id

    let bind (mapper: 'T -> Observable<'V>) (observable: Observable<'T>) : Observable<'V> =
        let (Subscribe subscribe) = observable

        fun (observer: Observer<'V>) ->

            let outerCompleted = ref 0
            let isError = ref 0
            let childObservalesCompleted = ref 0

            let childSubscriptions =
                new Collections.Concurrent.ConcurrentBag<IDisposable>()

            let disposeChildren () =
                for c in childSubscriptions do
                    c.Dispose()

            let observerForwarding =
                Observer.Create(
                    (fun v ->
                        if isError.Value = 0 then
                            observer.Notify(v |> Next)),
                    (fun e ->
                        Interlocked.Exchange(isError, 1) |> ignore
                        disposeChildren ()
                        observer.Notify(e |> Error)),
                    (fun () ->
                        Interlocked.Increment(childObservalesCompleted)
                        |> ignore

                        if outerCompleted.Value = 1
                           && isError.Value = 0
                           && childObservalesCompleted.Value = childSubscriptions.Count then
                            observer.Notify(Completed)
                            disposeChildren ())
                )

            let observerInternal =
                Observer.Create(
                    (fun value ->
                        if outerCompleted.Value = 0 && isError.Value = 0 then
                            let observableChild = (value |> mapper)
                            childSubscriptions.Add(observableChild.SubscribeWith(observerForwarding))),
                    (fun e ->
                        Interlocked.Exchange(isError, 1) |> ignore
                        disposeChildren ()
                        observer.Notify(e |> Error)),
                    (fun () -> Interlocked.Exchange(outerCompleted, 1) |> ignore)
                )

            observable.SubscribeWith(observerInternal)
        |> Subscribe

    let map (mapper: 'T -> 'V) =
        bind (fun value -> value |> mapper |> ret)

    let join observables = observables |> bind id

    let takeWhile predicate (ovservable: Observable<'T>) =
        (fun (observer: Observer<'T>) ->
            let stopped = ref false

            ovservable.SubscribeWith(
                Observer.CreateForwarding(
                    observer,
                    (fun value ->
                        if not stopped.Value then
                            if value |> predicate then
                                value |> Next
                            else
                                stopped.Value <- true
                                Completed
                            |> observer.Notify)
                )
            ))
        |> Subscribe

    let take count (ovservable: Observable<'T>) =
        let curCount = ref 0

        ovservable
        |> takeWhile (fun _ ->
            let res = curCount.Value < count
            Interlocked.Increment(curCount) |> ignore
            res)

    let filter predicate observable =
        observable
        |> bind (fun v ->
            if v |> predicate then
                v |> ret
            else
                empty ())

    let mergeAll observables = observables |> fromSeq |> join
    let merge first second = [ first; second ] |> mergeAll

    let concat (first: Observable<'T>) (second: Observable<'T>) =
        fun (observer: Observer<'T>) ->
            let subs1 =
                Observer.CreateForwarding(
                    observer,
                    (fun v -> v |> Next |> observer.Notify),
                    (fun () ->
                        let subs2 =
                            observer
                            |> Observer.CreateForwarding
                            |> second.SubscribeWith

                        ())
                )
                |> first.SubscribeWith

            subs1 //TODO: not good
        |> Subscribe
