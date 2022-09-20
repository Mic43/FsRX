namespace FsRX.Stateful

open System
open System.Collections.Concurrent
open FsRX

[<AbstractClass>]
type BasicObservable<'T>() =
    let subscriptions = new ConcurrentBag<IObserver<'T>>()
    member internal this.Subscriptions = subscriptions

    interface IObservable<'T> with
        member this.Subscribe(observer: IObserver<'T>) : IDisposable = this.Subscribe(observer)

    abstract Subscribe : IObserver<'T> -> IDisposable

    default this.Subscribe(observer: IObserver<'T>) : IDisposable =
        this.Subscriptions.Add(observer)

        Disposable.create (fun () ->
            this.Subscriptions.TryTake(ref observer) |> ignore
            ())

module Functions =
    let fromSeq<'T> seq : IObservable<'T> =
        { new BasicObservable<'T>() with
            override this.Subscribe(observer) =
                let subs = base.Subscribe(observer)

                seq |> Seq.iter (fun v -> v |> observer.OnNext)
                observer.OnCompleted()

                subs }

    let ret value = fromSeq (Seq.singleton value)
    let empty () = fromSeq Seq.empty

    let never () =
        { new BasicObservable<'T>() with
            override _.Subscribe(_) = Disposable.empty () }

    let throw e =
        { new BasicObservable<'T>() with
            override _.Subscribe(observer) =
                let subs = base.Subscribe(observer)
                e |> observer.OnError

                subs }
