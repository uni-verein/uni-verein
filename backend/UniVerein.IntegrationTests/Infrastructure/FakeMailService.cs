using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using Microsoft.AspNetCore.SignalR;

namespace UniVerein.IntegrationTests.Infrastructure;

public class FakeMailService : MailService
{
    public List<(string To, string Subject, string Body)> SentMails { get; } = new();

    public FakeMailService(AppDbContext db, CryptoService crypto, IHubContext<EmailProgressHub> hubContext) : base(db,
        crypto, hubContext)
    {
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        SentMails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}