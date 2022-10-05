// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module Test

open System
open System.Threading
open FsRX.Stateful.Functions
open FsRX.Stateful


[<EntryPoint>]
let main argv =
    //let observable =
    //    interval (TimeSpan.FromSeconds 1.0)
    //    |> map (fun v -> v + 1)
    //    |> take 3

    //let observable2 =
    //    observable
    //    |> bind (fun v -> TimeSpan.FromSeconds(1.0) |> interval |> take 3)

    //// let observer =
    ////(fun e -> match e with | (Next v) -> printfn v |> ignore) |> Observer

    //let obs =
    //    (generate2 1 (fun i -> i < 5) (fun i -> i + 1) (fun i -> i) (fun i -> TimeSpan.FromSeconds(i |> float)))
    //    |> take 2

    //use subs =
    //    //  (observable |> (TimeSpan.FromSeconds(1.0) |> delay) |> merge observable)
    //    obs.SubscribeWith(
    //        Observer.Create(
    //            (fun v -> Console.WriteLine(v)),
    //            (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //            (fun () -> Console.WriteLine("Completed"))
    //        )
    //    )

    //Console.ReadKey() |> ignore
    //0 // return an integer exit code



    // let obs = FsRX.Stateful.Functions.ret 5
    //  let dealyed = obs |>  FsRX.Stateful.Functions.delay (TimeSpan.FromSeconds(10))

    // let subs = dealyed.Subscribe(
    //      Observer.Create(
    //                 (fun (v:int) -> Console.WriteLine(v)),
    //                 (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //                 (fun () -> Console.WriteLine("Completed"))) |>  FsRX.Stateful.Functions.fromObserver)
    ////
    // Console.ReadKey() |> ignore
    // subs.Dispose()

    //let s2 = ((range 1 5) |> delay (TimeSpan.FromSeconds(5))).Subscribe(
    //     Observer.Create(
    //                (fun (v:int) -> Console.WriteLine(v)),
    //                (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //                (fun () -> Console.WriteLine("Completed"))) |>  FsRX.Stateful.Functions.fromObserver)
    //Console.ReadKey() |> ignore
    //s2.Dispose()




    //let obs =
    //    interval (1 |> TimeSpan.FromSeconds) |> concat (seq {1;2;3;6} |> withInterval (0.5 |> TimeSpan.FromSeconds))

    //let subs =
    //    Observer.Create(
    //        (fun (v: int) -> Console.WriteLine(v)),
    //        (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //        (fun () -> Console.WriteLine("Completed"))
    //    )
    //    |> fromObserver |> obs.Subscribe

    //Console.ReadKey() |> ignore
    //let subs2 =
    //    Observer.Create(
    //        (fun (v: int) -> Console.WriteLine(v)),
    //        (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //        (fun () -> Console.WriteLine("Completed"))
    //    )
    //    |> fromObserver |> obs.Subscribe
    //Console.ReadKey() |> ignore

    //let observable2 =
    //    obs
    //    |> bind (fun v -> TimeSpan.FromSeconds(1.0) |> interval |> map (fun v2 -> v*10 + v2)  |> take 3)

    // let subs = Observers.createDiagnostic() |>  obs.Subscribe

    let outerCount = 4
    let innerCount = 3

    let observableOfObservables =
        range 1 10
        |> take outerCount
        |> map (fun i ->
           range 1 10
            |> map (fun j -> i * 10 + j)
            |> take innerCount)


    let subs =
        (observableOfObservables |> switch )
            .Subscribe(Observers.createDiagnostic ())
    //   Console.ReadKey() |> ignore
    //subs.Dispose()



    //let obs =
    //let subs =
    //    Observer.Create(
    //        (fun ((p:int),(v:IObservable<int>)) ->
    //           // if p = 1 then
    //                Console.WriteLine(p)
    //                Observer.Create(
    //                           (fun (v: int) -> Console.WriteLine((p |> string)+ " " + (v |> string))),
    //                           (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //                           (fun () -> Console.WriteLine("Completed " + string p ))
    //                ) |> fromObserver |>  v.Subscribe |> ignore

    //        ),
    //        (fun e -> Console.WriteLine("Error: " + e.ToString())),
    //        (fun () -> Console.WriteLine("Completed global"))
    //    )
    //    |> fromObserver
    //    |> (obs |> groupBy (fun v -> v % 2)).Subscribe

    Console.ReadKey() |> ignore
    subs.Dispose()
    Console.ReadKey() |> ignore
    0
