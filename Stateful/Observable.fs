namespace FsRX.Stateful

open System
open System.Collections.Concurrent
open FsRX

[<AbstractClass>]
type BasicObservable<'T>() =
    //inherit IObservable<'T>
    let subscriptions = new ConcurrentBag<IObserver<'T>>()
    member internal this.Subscriptions = subscriptions

    interface IObservable<'T> with
        member this.Subscribe(observer: IObserver<'T>) : IDisposable = this.Subscribe(observer)

    abstract Subscribe : IObserver<'T> -> IDisposable


module Functions =
    let fromSeq<'T> seq =
        { new BasicObservable<'T>() with
            override this.Subscribe(observer) =
                this.Subscriptions.Add(observer)
                seq |> Seq.iter (fun v -> v |> observer.OnNext)
           

               
                do observer.OnCompleted()
                Disposable.empty () }

//let ret value = fromSeq (Seq.singleton value)
//let empty () = fromSeq Seq.empty

//let never () =
//    (fun _ -> Disposable.empty ()) |> Subscribe

//let throw e =
//    (fun (observer: Observer<'T>) ->
//        observer.Notify(e |> Error)
//        Disposable.empty ())
//    |> Subscribe
