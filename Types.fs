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

    static member Create<'T>(onNext: 'T -> unit, onError, onCompleted) =
        (fun e ->
            match e with
            | Next value -> onNext value
            | Completed -> onCompleted ()
            | Error -> onError ())

        |> Observer

    static member Create<'T>(onNext: 'T -> unit) =
        Observer.Create(onNext, (fun _ -> ()), (fun _ -> ()))

    static member CreateForwarding<'T>(observerToForward: Observer<'T>) =
        Observer.Create(
            (fun v -> observerToForward.Notify(v |> Next)),
            (fun _ -> observerToForward.Notify(Error)),
            (fun _ -> observerToForward.Notify(Completed))
        )

type Observable<'T> =
    | Subscribe of (Observer<'T> -> unit)
    member this.SubscribeWith(observer) =
        let (Subscribe s) = this
        observer |> s

[<AutoOpen>]
module Functions =
    open System

    let private createObserver<'T> (onNext: 'T -> unit) = Observer.Create(onNext)

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


    let bind (mapper: 'T -> Observable<'V>) (observable: Observable<'T>) : Observable<'V> =
        let (Subscribe subscribe) = observable

        fun (observer: Observer<'V>) ->
            let observerInternal =
                Observer.Create(
                    (fun value ->
                        let observableChild = (value |> mapper)

                        let observerForwarding =
                            createObserver (fun v -> observer.Notify(v |> Next))

                        observableChild.SubscribeWith(observerForwarding)),
                    (fun _ -> observer.Notify(Error)),
                    fun _ -> observer.Notify(Completed)
                )

            observable.SubscribeWith(observerInternal)
            ()
        |> Subscribe

    let map (mapper: 'T -> 'V) =
        bind (fun value -> value |> mapper |> ret)

    let take count (ovservable: Observable<'T>) =

        (fun (observer: Observer<'T>) ->
            let mutable curCount = 0

            ovservable.SubscribeWith(
                (fun value ->
                    if curCount < count then
                        observer.Notify(value |> Next)
                        curCount <- curCount + 1

                    if curCount = count then
                        observer.Notify(Completed)
                        curCount <- curCount + 1
                 )
                |> createObserver
            ))
        |> Subscribe
