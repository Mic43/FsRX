// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module Test

open System
open System.Threading
open FsRX
open FsRX.Stateful.Functions

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


    let obs =
        (generate2 1 (fun i -> i < 5) (fun i -> i + 1) (fun i -> i) (fun i -> 1.0 |> TimeSpan.FromSeconds))

    let subs =
        Observer.Create(
            (fun (v: int) -> Console.WriteLine(v)),
            (fun e -> Console.WriteLine("Error: " + e.ToString())),
            (fun () -> Console.WriteLine("Completed"))
        )
        |> FsRX.Stateful.Functions.fromObserver
        |> obs.Subscribe

    Console.ReadKey() |> ignore
    subs.Dispose()
    Console.ReadKey() |> ignore
    0
