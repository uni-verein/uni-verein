using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using MimeKit;

namespace UniVerein.Api.Interfaces;

public interface IImapClientWrapper
{
    Task ConnectAsync(string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto,
        CancellationToken cancellationToken = default);

    Task AuthenticateAsync(string username, string password);
    Task AppendAsync(string folderName, MimeMessage message);
    Task DisconnectAsync();
}