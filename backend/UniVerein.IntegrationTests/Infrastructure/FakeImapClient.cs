using UniVerein.Api.Interfaces;
using MailKit.Security;
using MimeKit;

namespace UniVerein.IntegrationTests.Infrastructure;

public class FakeImapClient : IImapClientWrapper
{
    public List<(string Folder, MimeMessage Message)> AppendedMessages { get; set; } = new();

    public Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }


    public Task AuthenticateAsync(string username, string password)
    {
        return Task.CompletedTask;
    }

    public Task AppendAsync(string folderName, MimeMessage message)
    {
        AppendedMessages.Add((folderName, message));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        return Task.CompletedTask;
    }
}