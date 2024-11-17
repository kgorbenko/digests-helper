module DigestsMailHelper.EmlParser

open System.IO
open System.Text.RegularExpressions
open System.Threading.Tasks

open MimeKit

open Utils

type DigestInfo = {
    FromAddress: string
    IssueNumber: int option
    Headline: string
    IssueUrl: string
}

let private xNewsletterHeader = "x-newsletter"

let private numberGroupName = "number"
let private issueNumberRegex = Regex($"#(?<{numberGroupName}>\d+)")

let private urlGroupName = "url"
let private issueUrlBodyRegex = Regex($"View in browser\s?\(\s?(?<{urlGroupName}>\S*)\s?\)")

let private tryGetMatch (regex: Regex) (valueAccessor: Match -> string) (text: string) =
    match regex.Match(text) with
    | m when m.Success -> valueAccessor m |> Some
    | _ -> None

let tryParseIssueNumber (message: MimeMessage): int option =
    let regex = issueNumberRegex
    let valueAccessor (match': Match) = match'.Groups[numberGroupName].Value

    message.Subject
    |> tryGetMatch regex valueAccessor
    |> Option.orElseWith (fun () -> tryGetMatch regex valueAccessor message.TextBody)
    |> Option.bind tryParseInt

let tryParseIssueUrl (message: MimeMessage): string option =
    let regex = issueUrlBodyRegex
    let valueAccessor (match': Match) = match'.Groups[urlGroupName].Value

    message.Headers
    |> Seq.tryFind (fun x -> x.Field = xNewsletterHeader)
    |> Option.map _.Value
    |> Option.orElseWith (fun () -> tryGetMatch regex valueAccessor message.TextBody)

let tryParseDigestInfoAsync (message: MimeMessage) : DigestInfo option =
    message
    |> tryParseIssueUrl
    |> Option.map (fun url ->
        { Headline = message.Subject
          IssueNumber = tryParseIssueNumber message
          IssueUrl = url
          FromAddress = message.From |> Seq.exactlyOne |> _.Name }
    )

let tryParseDigestInfoFromMessageStreamAsync (stream: Stream) : Task<DigestInfo option> =
    task {
        use! message = MimeMessage.LoadAsync(stream)
        return tryParseDigestInfoAsync message
    }