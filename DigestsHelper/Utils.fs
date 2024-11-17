module DigestsHelper.Utils

open System
open System.Threading.Tasks

let tryParseInt (value: string) : int option =
    match Int32.TryParse value with
    | true, int -> Some int
    | _ -> None

let ignoreAsync (t: Task<'a>): Task =
    task {
        let! _ = t
        ()
    }