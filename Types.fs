namespace FsRX

[<Struct>]
type Event<'T> =
    | Next of 'T
    | Completed
    | Error

type Observer<'T> =
    | Observer of (Event<'T> -> unit)
    member this.Notify(e) =
        let (Observer o) = this
        o e

type Observable<'T> =
    | Subscribe of (Observer<'T> -> unit)
    member this.SubscribeWith(observer) =
        let (Subscribe s) = this
        observer |> s

module Functions =
    open System

    let fromSeq seq =
        (fun (observer: Observer<'T>) ->
            seq
            |> Seq.iter (fun v -> observer.Notify(v |> Next))

            observer.Notify(Completed))
        |> Subscribe

    let ret value = fromSeq (Seq.singleton value)
    let empty () = fromSeq Seq.empty
    let never () = (fun _ -> ()) |> Subscribe

    let throw () =
        (fun (observer: Observer<'T>) ->
            observer.Notify(Error)
            ())
        |> Subscribe

    let generate (initial: 'TState) condition iter resultSelector =
        Seq.unfold
            (fun s ->
                if condition s then
                    (s |> resultSelector, s |> iter) |> Some
                else
                    None)
            initial
        |> fromSeq
   
    let generate2 (initial: 'TState) condition iter resultSelector (timeSelector: 'TState -> TimeSpan) =
        fun (observer: Observer<'T>) ->
            let rec generateInernal (curState) =
                if curState |> condition |> not then
                    observer.Notify(Completed)
                else
                    let timer = new Timers.Timer()
                    timer.Interval <- (curState |> timeSelector).TotalMilliseconds

                    timer.Elapsed.Add (fun _ ->
                        observer.Notify(curState |> resultSelector |> Next)
                        timer.Dispose()
                        generateInernal (curState |> iter))

                    timer.AutoReset <- false
                    timer.Start()

            generateInernal initial
        |> Subscribe

    let interval period =
        generate2 0 (fun _ -> true) (fun i -> i + 1) id (fun _ -> period)
    
    let range min max =
        generate min (fun cur -> cur < max) (fun cur -> cur + 1) id

    let private createObserver (onNext: 'T -> unit) =
        (fun e ->
            match e with
            | Next value -> onNext value
            | _ -> ())

        |> Observer

    let bind (mapper: 'T -> Observable<'V>) (observable: Observable<'T>) =
        let (Subscribe subscribe) = observable

        fun (observer: Observer<'V>) ->
            let observerInternal (e: Event<'T>) =
                match e with
                | Next value ->
                    let observableChild = (value |> mapper)

                    let observerForwarding =
                        createObserver (fun v -> observer.Notify(v |> Next))

                    observableChild.SubscribeWith(observerForwarding)
                | Completed -> observer.Notify(Completed)
                | Error -> observer.Notify(Error)

            observable.SubscribeWith(observerInternal |> Observer)
            ()
        |> Subscribe

    let map (mapper: 'T -> 'V) =
        bind (fun value -> value |> mapper |> ret)
// let (Subscribe subscribe) = observable

// fun (observer: Observer<'V>) ->
//     let observerInternal (e: Event<'T>) =
//         observer.Notify(
//             match e with
//             | Next value -> (value |> mapper) |> Next
//             | Completed -> Completed
//             | Error -> (Error)
//         )

//     observable.SubscribeWith(observerInternal |> Observer)
//     ()
// |> Subscribe
