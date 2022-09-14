namespace FsRX

open System

[<Struct>]
type Event<'T> =
    | Next of 'T
    | Completed
    | Error

type Observer<'T> =
    | Observer of (Event<'T> -> unit)
    member this.Notify(e) =
        let (Observer o) = this
        o e

    static member Create<'T>(onNext: 'T -> unit, onError, onCompleted) =
        (fun e ->
            match e with
            | Next value -> onNext value
            | Completed -> onCompleted ()
            | Error -> onError ())

        |> Observer

    static member Create<'T>(onNext: 'T -> unit) =
        Observer.Create(onNext, (fun _ -> ()), (fun _ -> ()))

    static member CreateForwarding<'T>(observerToForward: Observer<'T>) =
        Observer.Create(
            (fun v -> observerToForward.Notify(v |> Next)),
            (fun () -> observerToForward.Notify(Error)),
            (fun () -> observerToForward.Notify(Completed))
        )

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

[<AutoOpen>]
module Functions =
    open System

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

    let throw () =
        (fun (observer: Observer<'T>) ->
            observer.Notify(Error)
            Disposable.empty ())
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

    let generate2 (initial: 'TState) condition iter resultSelector (timeSelector: 'TState -> TimeSpan) =
        fun (observer: Observer<'T>) ->
            //let mutable counter = 0
            let rec generateInernal (curState) =
                if curState |> condition |> not then
                    observer.Notify(Completed)
                else
                    let timer = new Timers.Timer()

                    timer.Interval <- (curState |> timeSelector).TotalMilliseconds
                    timer.AutoReset <- false

                    timer.Elapsed.Add (fun _ ->
                        observer.Notify(curState |> resultSelector |> Next)
                        timer.Dispose()
                        //TODO:
                        let subscription = generateInernal (curState |> iter)
                        ())

                    timer.Start()
                //TODO:
                Disposable.empty ()

            generateInernal initial
        |> Subscribe

    let interval period =
        generate2 0 (fun _ -> true) (fun i -> i + 1) id (fun _ -> period)

    let range min max =
        generate min (fun cur -> cur < max) (fun cur -> cur + 1) id


    let bind (mapper: 'T -> Observable<'V>) (observable: Observable<'T>) : Observable<'V> =
        let (Subscribe subscribe) = observable

        fun (observer: Observer<'V>) ->
            let mutable outerCompleted = false
            let mutable childObservalesCompleted = 0

            let childSubscriptions =
                new Collections.Generic.List<IDisposable>()

            let observerForwarding =
                Observer.Create(
                    (fun v -> observer.Notify(v |> Next)),
                    (fun () ->
                        childSubscriptions.ForEach(fun d -> d.Dispose())
                        observer.Notify(Error)),
                    (fun () ->
                        childObservalesCompleted <- childObservalesCompleted + 1

                        if outerCompleted
                           && childObservalesCompleted = childSubscriptions.Count then
                            observer.Notify(Completed)
                            childSubscriptions.ForEach(fun d -> d.Dispose()))
                )

            let observerInternal =
                Observer.Create(
                    (fun value ->
                        let observableChild = (value |> mapper)

                        childSubscriptions.Add(observableChild.SubscribeWith(observerForwarding))
                        ()),
                    (fun () ->
                        childSubscriptions.ForEach(fun d -> d.Dispose())
                        observer.Notify(Error)),
                    fun () -> outerCompleted <- true
                )

            observable.SubscribeWith(observerInternal)
        |> Subscribe

    let map (mapper: 'T -> 'V) =
        bind (fun value -> value |> mapper |> ret)

    let join observables = observables |> bind id

    let take count (ovservable: Observable<'T>) =
        (fun (observer: Observer<'T>) ->
            let mutable curCount = 0

            ovservable.SubscribeWith(
                (fun value ->
                    if curCount < count then
                        observer.Notify(value |> Next)
                        curCount <- curCount + 1

                    if curCount = count then
                        observer.Notify(Completed)
                        curCount <- curCount + 1)
                |> createObserver
            ))
        |> Subscribe

    let filter predicate observable =
        observable
        |> bind (fun v ->
            if v |> predicate then
                v |> ret
            else
                empty ())

    let mergeAll observables = observables |> fromSeq |> join
    let merge obs1 obs2 = [ obs1; obs2 ] |> mergeAll
