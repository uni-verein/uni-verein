using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;

namespace UniVerein.Api.Services;

public class ImapClientWrapper : IImapClientWrapper
{
    private readonly ImapClient _client = new();

    public async Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto,
        CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(host, port, options);
    }

    public async Task AuthenticateAsync(string username, string password)
    {
        await _client.AuthenticateAsync(username, password);
    }

    public async Task AppendAsync(string folderName, MimeMessage message)
    {
        IMailFolder? sentFolder = _client.GetFolder(SpecialFolder.Sent);
        if (sentFolder == null)
            sentFolder = await _client.GetFolderAsync("Sent");

        await sentFolder.OpenAsync(FolderAccess.ReadWrite);
        await sentFolder.AppendAsync(message, MessageFlags.Seen);
        await sentFolder.CloseAsync();
    }

    public async Task DisconnectAsync()
    {
        await _client.DisconnectAsync(true);
    }
}