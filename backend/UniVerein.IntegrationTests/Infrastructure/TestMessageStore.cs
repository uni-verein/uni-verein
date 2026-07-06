using System.Buffers;
using MimeKit;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace UniVerein.IntegrationTests.Infrastructure;

public class TestMessageStore : MessageStore
{
    private readonly List<MimeMessage> _messages;

    public TestMessageStore(List<MimeMessage> messages)
    {
        _messages = messages;
    }

    public override async Task<SmtpResponse> SaveAsync(
        ISessionContext context,
        IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(buffer.ToArray());
        var message = await MimeMessage.LoadAsync(stream, cancellationToken);
        _messages.Add(message);
        return SmtpResponse.Ok;
    }
}