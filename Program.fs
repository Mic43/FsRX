// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module Test

open System
open FsRX
// Define a function to construct a message to print
let from whom = sprintf "from %s" whom

[<EntryPoint>]
let main argv =
    let observable =
        interval (TimeSpan.FromSeconds 1.0)
        |> map (fun v -> v + 1)
        |> take 5

    let observable2 =
        fromSeq { 1 .. 5 }
        |> bind (fun v -> interval (TimeSpan.FromSeconds(v |> float)))

    // let observer =
    //(fun e -> match e with | (Next v) -> printfn v |> ignore) |> Observer

    observable.SubscribeWith(
        Observer.Create(
            (fun v -> printfn "%d" v),
            (fun () -> printfn "%s" "Error"),
            (fun () -> printfn "%s" "Completed")
        )
    )

    Console.ReadKey() |> ignore
    0 // return an integer exit code
