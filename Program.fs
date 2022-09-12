// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module Test

open System
open FsRX
// Define a function to construct a message to print
let from whom = sprintf "from %s" whom

[<EntryPoint>]
let main argv =
    let observable = Functions.interval (TimeSpan.FromSeconds 1.0) |> Functions.map (fun v -> v + 1)
    let observable2= Functions.fromSeq {1..5} |> Functions.bind (fun v -> Functions.fromSeq {v..v+5} )

    // let observer =
    //(fun e -> match e with | (Next v) -> printfn v |> ignore) |> Observer

    observable.SubscribeWith(
        (fun e ->
            match e with
            | (Next v) -> printfn "%d" v
            | Completed ->  printfn "%s" "Completed"
            | Error ->  printfn "%s" "Error")
        |> Observer
    )
    Console.ReadKey() |> ignore
    0 // return an integer exit code
