// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module Test

open System
open FsRX
open System.Threading
// Define a function to construct a message to print
let from whom = sprintf "from %s" whom

[<EntryPoint>]
let main argv =
    let observable =
        interval (TimeSpan.FromSeconds 1.0)
        |> map (fun v -> v + 1)
        |> take 3

    let observable2 =
        observable
        |> bind (fun v -> TimeSpan.FromSeconds(1.0) |> interval |> take 3)

    // let observer =
    //(fun e -> match e with | (Next v) -> printfn v |> ignore) |> Observer

    let obs =
        (generate2 1 (fun i -> i < 5) (fun i -> i + 1) (fun i -> i) (fun i -> TimeSpan.FromSeconds(i |> float)))
        |> take 2

    use subs =
        //  (observable |> (TimeSpan.FromSeconds(1.0) |> delay) |> merge observable)
        obs.SubscribeWith(
            Observer.Create(
                (fun v -> Console.WriteLine(v)),
                (fun e -> Console.WriteLine("Error: " + e.ToString())),
                (fun () -> Console.WriteLine("Completed"))
            )
        )

    Console.ReadKey() |> ignore
    0 // return an integer exit code
