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

    use subs =
         (observable |> concat observable)
            .SubscribeWith(
                Observer.Create(
                    (fun v -> Console.WriteLine(v)),
                    (fun e -> Console.WriteLine("Error: " + e.ToString())),
                    (fun () -> Console.WriteLine("Completed"))
                )
            )

    Console.ReadKey() |> ignore
    0 // return an integer exit code
