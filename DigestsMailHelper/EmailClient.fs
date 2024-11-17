module DigestsMailHelper.EmailClient

open FSharp.Control
open System.Threading
open System.Threading.Tasks

open MailKit
open MailKit.Net.Imap
open MailKit.Search
open MimeKit

open Utils

type ImapConfiguration = {
    Server: string
    Port: int
    Username: string
    Password: string
}

let makeImapClientAsync (configuration: ImapConfiguration)
                        (cancellationToken: CancellationToken)
                        : ImapClient Task =
    task {
        let client = new ImapClient()
        do! client.ConnectAsync(
            configuration.Server,
            configuration.Port,
            useSsl = true,
            cancellationToken = cancellationToken
        )

        do! client.AuthenticateAsync(configuration.Username, configuration.Password, cancellationToken)
        return client
    }

let ensureInboxOpenedReadOnlyAsync (client: IImapClient)
                                   (cancellationToken: CancellationToken)
                                   : Task<unit> =
    task {
        if not client.Inbox.IsOpen then
            do!
                client.Inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken)
                |> ignoreAsync
    }

let searchMessagesByFromAddressAsync (client: IImapClient)
                                     (cancellationToken: CancellationToken)
                                     (fromAddress: string)
                                     : UniqueId list Task =
    task {
        let! uniqueIds = client.Inbox.SearchAsync(
            SearchQuery.FromContains(fromAddress),
            cancellationToken
        )
        return uniqueIds |> List.ofSeq
    }

let getMessagesByIdsAsync (client: IImapClient)
                          (cancellationToken: CancellationToken)
                          (ids: UniqueId list)
                          : TaskSeq<MimeMessage> =
    ids
    |> TaskSeq.ofSeq
    |> TaskSeq.mapAsync (fun id ->
        task { return! client.Inbox.GetMessageAsync(id, cancellationToken) }
    )