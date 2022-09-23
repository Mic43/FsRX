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


module Functions =
    open System.Threading.Tasks

    let fromObserver<'T> (o: FsRX.Observer<'T>) =
        { new IObserver<'T> with
            member this.OnCompleted() : unit = Completed |> o.Notify
            member this.OnError(error: exn) : unit = error |> Error |> o.Notify
            member this.OnNext(value: 'T) : unit = value |> Next |> o.Notify }

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

            let mutable innerSubscription: IDisposable ref = ref null
            let mutable isDisposing = ref 0

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

            let mutable currentSubscription: IDisposable ref = ref null

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
                        (Observer.Create(
                            (fun v ->
                                do observer.OnNext(v)
                                do generateInernal (curState |> iter)),
                            (fun e ->
                                do observer.OnError(e)
                                disposeCurrentSubscription ())
                         )
                         |> fromObserver
                         |> observable.Subscribe)
                    )
                    |> ignore

            do generateInernal initial

            Disposable.create (fun () -> disposeCurrentSubscription ())
        |> fromFun
        |> asObservable
