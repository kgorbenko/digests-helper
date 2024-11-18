open System

open FSharp.Control
open Microsoft.Extensions.Configuration
open System.Threading
open System.Threading.Tasks

open DigestsHelper.EmailClient
open DigestsHelper.EmlParser
open MimeKit

type DigestDescriptor = {
    FromAddress: string
    Name: string
}

let tryParseMessage
    (digest: DigestDescriptor)
    (message: MimeMessage)
    : DigestInfo option
    =
    use message = message

    printfn $"MessageId: {message.MessageId}"
    printfn $"Subject: {message.Subject}"

    let digestInfoOption = tryParseDigestInfoAsync message

    let logMessage =
        match digestInfoOption with
        | Some di ->
            [ digest.Name
              match di.IssueNumber with
              | Some number -> $"#{number}"
              | None -> () ]
            |> String.concat " - "
        | None -> "not a digest"

    printfn $"{logMessage}{Environment.NewLine}"

    digestInfoOption

let chooseDigestsAsync
    (configuration: ImapConfiguration)
    (cancellationToken: CancellationToken)
    (digest: DigestDescriptor)
    : Task<DigestInfo list>
    =
    task {
        use! imapClient = makeImapClientAsync configuration cancellationToken
        do! ensureInboxOpenedReadOnlyAsync imapClient cancellationToken

        let! messageIds = searchMessagesByFromAddressAsync imapClient cancellationToken digest.FromAddress
        let messages = getMessagesByIdsAsync imapClient cancellationToken messageIds

        return!
            messages
            |> TaskSeq.choose (tryParseMessage digest)
            |> TaskSeq.toListAsync
    }

let configurationRoot =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build()

let imapConfiguration =
    configurationRoot
        .GetRequiredSection("ImapConfiguration")
        .Get<ImapConfiguration>()

let digests =
    configurationRoot
        .GetRequiredSection("Digests")
        .Get<DigestDescriptor array>()

digests
|> Seq.iter (fun digest ->
    printfn "---------------------------------------------"
    printfn $"{digest.Name}:{Environment.NewLine}"

    let messages =
        chooseDigestsAsync imapConfiguration CancellationToken.None digest
        |> _.GetAwaiter().GetResult()

    printfn $"{digest.Name} Markdown:{Environment.NewLine}"

    messages
    |> Seq.iter (fun x ->
        let issueNumberClause =
            x.IssueNumber
            |> Option.map (fun x -> $"(#{x})")

        let linkText =
            [
                match issueNumberClause with
                | Some x -> x
                | None -> ()

                x.Headline
            ]
            |> String.concat " - "

        printfn $"- [ ] [{linkText}]({x.IssueUrl})"
    )
)