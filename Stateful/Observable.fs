namespace FsRX.Stateful

open System
open System.Collections.Concurrent
open FsRX
open System.Threading

[<AbstractClass>]
type BasicObservable<'T>() =
    let subscriptions = new ConcurrentBag<IObserver<'T>>()
    member internal this.Subscriptions = subscriptions

    interface IObservable<'T> with
        member this.Subscribe(observer: IObserver<'T>) : IDisposable = this.Subscribe(observer)

    abstract Subscribe : IObserver<'T> -> IDisposable

    member internal this.TryNotify (observer: IObserver<'T>) event =
        if this.Subscriptions.TryPeek(ref observer) then
            match event with
            | Next v -> v |> observer.OnNext
            | Error e -> e |> observer.OnError
            | Completed -> observer.OnCompleted()

        ()

    default this.Subscribe(observer: IObserver<'T>) : IDisposable =
        this.Subscriptions.Add(observer)

        Disposable.create (fun () ->
            this.Subscriptions.TryTake(ref observer) |> ignore
            ())

type private ObserverFactory<'T> =
    static member Create<'T>(onNext: 'T -> unit, (?onError: Exception -> unit), (?onCompleted: unit -> unit)) =

        let onCompleted = defaultArg onCompleted (fun () -> ())
        let onError = defaultArg onError (fun _ -> ())

        { new IObserver<'T> with
            member this.OnCompleted() : unit = onCompleted ()
            member this.OnError(error: exn) : unit = error |> onError
            member this.OnNext(value: 'T) : unit = value |> onNext }

    static member CreateForwarding<'T>
        (
            observerToForward: IObserver<'T>,
            (?onNext: 'T -> unit),
            (?onCompleted: unit -> unit),
            (?onError: Exception -> unit)
        ) =
        let onCompleted =
            defaultArg onCompleted (fun () -> observerToForward.OnCompleted())

        let onError =
            defaultArg onError (fun e -> observerToForward.OnError(e))

        let onNext =
            defaultArg onNext (fun v -> observerToForward.OnNext(v))

        ObserverFactory.Create(onNext, onError, onCompleted)

module Functions =
    open System.Threading.Tasks


    let fromObserver<'T> (o: FsRX.Observer<'T>) =
        { new IObserver<'T> with
            member this.OnCompleted() : unit = Completed |> o.Notify
            member this.OnError(error: exn) : unit = error |> Error |> o.Notify
            member this.OnNext(value: 'T) : unit = value |> Next |> o.Notify }

    // let createForwardingObserver

    let asObservable (o: BasicObservable<'T>) : IObservable<'T> = o

    let private fromFun (f: IObserver<'T> -> IDisposable) =
        { new BasicObservable<'T>() with
            override this.Subscribe(observer) =
                let subs = base.Subscribe(observer)

                [ observer |> f; subs ] |> Disposable.composite }

    let fromSeq<'T> seq : IObservable<'T> =
        { new BasicObservable<'T>() with
            override this.Subscribe(observer) =
                let subs = base.Subscribe(observer)

                seq
                |> Seq.iter (fun v -> v |> Next |> this.TryNotify(observer))

                Completed |> this.TryNotify(observer)

                subs }

    let ret value = fromSeq (Seq.singleton value)
    let empty () = fromSeq Seq.empty

    let never () =
        { new BasicObservable<'T>() with
            override _.Subscribe(_) = Disposable.empty () }

    let throw e =
        { new BasicObservable<'T>() with
            override this.Subscribe(observer) =
                let subs = base.Subscribe(observer)
                e |> Error |> this.TryNotify(observer)

                subs }

    let delay (period: TimeSpan) (observable: IObservable<'T>) =
        fun (observer: IObserver<'T>) ->

            let innerSubscription: IDisposable ref = ref null
            let isDisposing = ref 0

            let task =
                Task
                    .Delay(period)
                    .ContinueWith(
                        (fun _ ->
                            if isDisposing.Value = 0 then
                                let subs = observer |> observable.Subscribe

                                Interlocked.Exchange(innerSubscription, subs)
                                |> ignore),
                        TaskContinuationOptions.OnlyOnRanToCompletion
                    )

            Disposable.create (fun () ->
                Interlocked.Exchange(isDisposing, 1) |> ignore

                if innerSubscription <> ref null then
                    do innerSubscription.Value.Dispose())

        |> fromFun
        |> asObservable

    let generate (initial: 'TState) condition iter resultSelector =
        Seq.unfold
            (fun s ->
                if condition s then
                    (s |> resultSelector, s |> iter) |> Some
                else
                    None)
            initial
        |> fromSeq

    let range min max =
        generate min (fun cur -> cur < max) (fun cur -> cur + 1) id

    let generate2
        (initial: 'TState)
        condition
        iter
        (resultSelector: 'TState -> 'T)
        (timeSelector: 'TState -> TimeSpan)
        =
        fun (observer: IObserver<'T>) ->

            let currentSubscription: IDisposable ref = ref null

            let disposeCurrentSubscription () =
                if currentSubscription <> ref null then
                    do currentSubscription.Value.Dispose()

            let rec generateInernal curState =
                disposeCurrentSubscription ()

                if curState |> condition |> not then
                    observer.OnCompleted()
                else
                    let observable =
                        curState
                        |> resultSelector
                        |> ret
                        |> delay (curState |> timeSelector)

                    Interlocked.Exchange(
                        currentSubscription,
                        (ObserverFactory.Create(
                            (fun v ->
                                do observer.OnNext(v)
                                do generateInernal (curState |> iter)),
                            (fun e ->
                                do observer.OnError(e)
                                disposeCurrentSubscription ())
                         )
                         |> observable.Subscribe)
                    )
                    |> ignore

            do generateInernal initial

            Disposable.create (fun () -> disposeCurrentSubscription ())
        |> fromFun
        |> asObservable

    let interval period =
        generate2 0 (fun _ -> true) (fun i -> i + 1) id (fun _ -> period)

    let bind (mapper: 'T -> IObservable<'V>) (observable: IObservable<'T>) : IObservable<'V> =
        fun (observer: IObserver<'V>) ->
            let outerCompleted = ref 0
            let isStopped = ref 0
            let childObservalesCompleted = ref 0

            let childSubscriptions =
                new Collections.Concurrent.ConcurrentBag<IDisposable>()

            let stopAndDispose () =
                Interlocked.Exchange(isStopped, 1) |> ignore

                for c in childSubscriptions do
                    c.Dispose()

            let observerForwarding =
                ObserverFactory.Create(
                    (fun v ->
                        if isStopped.Value = 0 then
                            observer.OnNext(v)),
                    (fun e ->
                        stopAndDispose ()
                        observer.OnError(e)),
                    (fun () ->
                        Interlocked.Increment(childObservalesCompleted)
                        |> ignore

                        if outerCompleted.Value = 1
                           && isStopped.Value = 0
                           && childObservalesCompleted.Value = childSubscriptions.Count then
                            observer.OnCompleted()
                            stopAndDispose ())
                )

            let observerInternal =
                ObserverFactory.Create(
                    (fun value ->
                        if outerCompleted.Value = 0 && isStopped.Value = 0 then
                            let observableChild = (value |> mapper)
                            childSubscriptions.Add(observableChild.Subscribe(observerForwarding))),
                    (fun e ->
                        stopAndDispose ()
                        observer.OnError(e)),
                    (fun () -> Interlocked.Exchange(outerCompleted, 1) |> ignore)
                )

            [ observerInternal |> observable.Subscribe
              stopAndDispose |> Disposable.create ]
            |> Disposable.composite
        |> fromFun
        |> asObservable

    let map (mapper: 'T -> 'V) =
        bind (fun value -> value |> mapper |> ret)

    let join observables = observables |> bind id

    let takeWhile predicate (ovservable: IObservable<'T>) =
        (fun (observer: IObserver<'T>) ->
            let stopped = ref 0

            ovservable.Subscribe(
                ObserverFactory.CreateForwarding(
                    observer,
                    (fun value ->
                        if stopped.Value = 0 then
                            if value |> predicate then
                                value |> observer.OnNext
                            else
                                Interlocked.Exchange(stopped, 1) |> ignore
                                observer.OnCompleted()),
                    id
                )
            ))
        |> fromFun
        |> asObservable

    let take count (ovservable: IObservable<'T>) =
        (fun observer ->
            let curCount = ref 0

            (ovservable
             |> takeWhile (fun _ ->
                 let res = curCount.Value < count
                 Interlocked.Increment(curCount) |> ignore
                 res))
                .Subscribe(observer))
        |> fromFun
        |> asObservable

    let filter predicate observable =
        observable
        |> bind (fun v ->
            if v |> predicate then
                v |> ret
            else
                empty ())

    let mergeAll observables = observables |> fromSeq |> join
    let merge first second = [ first; second ] |> mergeAll

    let concat (first: IObservable<'T>) (second: IObservable<'T>) =
        fun (observer: IObserver<'T>) ->
            let innerSubs: IDisposable ref = ref null

            let subs1 =
                ObserverFactory.CreateForwarding(
                    observer,
                    (fun v -> v |> observer.OnNext),
                    (fun () ->
                        let subs =
                            observer
                            |> ObserverFactory.CreateForwarding
                            |> second.Subscribe

                        Interlocked.Exchange(innerSubs, subs) |> ignore)
                )
                |> first.Subscribe

            [ subs1
              Disposable.create (fun () ->
                  if innerSubs <> ref null then
                      do innerSubs.Value.Dispose()) ]
            |> Disposable.composite
        |> fromFun
        |> asObservable
