using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Shouldly;
using SmtpServer;
using SmtpServer.Storage;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class CalendarMailTests : IntegrationTestBase
{
    private const int SmtpPort = 2527;
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);

    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private CryptoService _crypto = null!;

    public CalendarMailTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _crypto = GetService<CryptoService>();

        await WithDbContext(async db =>
        {
            db.UserSettings.RemoveRange(db.UserSettings.AsQueryable());
            db.Notifications.RemoveRange(db.Notifications.IgnoreQueryFilters());
            db.CalendarEventSubscriptions.RemoveRange(db.CalendarEventSubscriptions.IgnoreQueryFilters());
            db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
            db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();

            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "127.0.0.1",
                Port = SmtpPort,
                Username = "test",
                Password = _crypto.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();
        });

        ISmtpServerOptions options = new SmtpServerOptionsBuilder().ServerName("localhost").Port(SmtpPort).Build();
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageStore>(new TestMessageStore(_receivedMails))
            .BuildServiceProvider();

        _smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
        _cts = new CancellationTokenSource();
        Task startTask = _smtpServer.StartAsync(_cts.Token);
        await WaitForSmtpPortAsync(startTask);
    }

    // A fixed delay is not enough on slow CI runners: a mail sent before the port is open is lost, there is no retry.
    private static async Task WaitForSmtpPortAsync(Task startTask)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            if (startTask.IsFaulted)
                throw new Exception("SMTP-Server not started", startTask.Exception);

            try
            {
                using System.Net.Sockets.TcpClient probe = new();
                await probe.ConnectAsync("127.0.0.1", SmtpPort);
                return;
            }
            catch (System.Net.Sockets.SocketException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }
    }

    public override async Task DisposeAsync()
    {
        _cts.Cancel();
        await base.DisposeAsync();
    }

    private async Task<(HttpClient Client, Guid Id)> CreateUserWithMailAsync(string username, string email)
    {
        (HttpClient client, Guid id) = await CreateUserAndClientAsync(UserRole.USER, username);
        await WithDbContext(async db =>
        {
            UserEntity user = await db.Users.SingleAsync(u => u.Id == id);
            user.Email = _crypto.Encrypt(email);
            await db.SaveChangesAsync();
        });
        return (client, id);
    }

    private static CalendarEventRequest NewRequest(string title, params Guid[] participants)
    {
        return new CalendarEventRequest
        {
            Title = title,
            LocationName = "Stadtpark",
            Start = Start,
            End = Start.AddHours(2),
            Participants = participants.Select(id => new CalendarEventParticipantRequest { UserId = id }).ToList()
        };
    }

    [Fact]
    public async Task Invitation_SendsMailToInvitee_WithEncodedTitle()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserWithMailAsync("owner", "owner@test.de");
        (_, Guid annaId) = await CreateUserWithMailAsync("anna", "anna@test.de");

        // Act
        HttpResponseMessage response =
            await owner.PostAsJsonAsync("/calendar/events", NewRequest("Grillen <b>am</b> See", annaId));
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        await Task.Delay(300);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        _receivedMails.Count.ShouldBe(1);
        MimeMessage mail = _receivedMails[0];
        mail.To.ToString().ShouldContain("anna@test.de");
        mail.Subject.ShouldNotBeNull().ShouldContain("Grillen <b>am</b> See");
        string htmlBody = mail.HtmlBody.ShouldNotBeNull();
        htmlBody.ShouldContain("Grillen &lt;b&gt;am&lt;/b&gt; See");
        htmlBody.ShouldContain("Stadtpark");

        MimePart ics = mail.Attachments.OfType<MimePart>().Single(a => a.FileName == "termin.ics");
        ics.ContentType.MimeType.ShouldBe("text/calendar");
        using MemoryStream stream = new();
        await ics.Content.ShouldNotBeNull().DecodeToAsync(stream);
        System.Text.Encoding.UTF8.GetString(stream.ToArray()).ShouldContain("SUMMARY:Grillen <b>am</b> See");
    }

    [Fact]
    public async Task Change_AfterMailLimit_SendsNoMail_ButStillCreatesInAppNotification()
    {
        // Arrange
        (HttpClient owner, Guid ownerId) = await CreateUserWithMailAsync("owner", "owner@test.de");
        (_, Guid annaId) = await CreateUserWithMailAsync("anna", "anna@test.de");
        System.Text.Json.JsonSerializerOptions json = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        HttpResponseMessage createResponse =
            await owner.PostAsJsonAsync("/calendar/events", NewRequest("Training", annaId));
        CalendarEventResult current = (await createResponse.Content.ReadFromJsonAsync<CalendarEventResult>(json))!;
        await WaitForConditionAsync(() => _receivedMails.Count >= 1, timeoutMs: 30000);

        // The invitation used one slot, the rest is used up directly in the queue. The jobs have no real recipients,
        // so no mail is sent. The mail limit itself is covered by CalendarMailQueueTests.
        CalendarMailQueue queue = GetService<CalendarMailQueue>();
        for (int i = 1; i < CalendarMailQueue.MaxJobsPerActor; i++)
        {
            queue.Enqueue(new CalendarMailJob([Guid.NewGuid()], UserSettingType.CALENDAR_CHANGE_EMAIL,
                new EmailRequest(), ownerId)).ShouldBeTrue();
        }

        // Let the mail service finish these jobs, so it does not read the database while the request runs.
        await Task.Delay(1000);
        _receivedMails.Clear();

        CalendarEventRequest update = NewRequest("Training verschoben", annaId);
        update.Version = current.Version;

        // Act
        HttpResponseMessage response = await owner.PutAsJsonAsync($"/calendar/events/{current.Id}", update);
        await Task.Delay(1000);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _receivedMails.ShouldBeEmpty();
        await WithDbContext(async db =>
        {
            (await db.Notifications.CountAsync(n =>
                n.UserId == annaId && n.Type == NotificationType.CALENDAR_EVENT_CHANGED)).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Change_OptedOutUserGetsNoMail_ButInAppNotification()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserWithMailAsync("owner", "owner@test.de");
        (HttpClient anna, Guid annaId) = await CreateUserWithMailAsync("anna", "anna@test.de");
        (_, Guid bobId) = await CreateUserWithMailAsync("bob", "bob@test.de");

        HttpResponseMessage created =
            await owner.PostAsJsonAsync("/calendar/events", NewRequest("Training", annaId, bobId));
        CalendarEventResult? calendarEvent = await created.Content.ReadFromJsonAsync<CalendarEventResult>(
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });
        await WaitForConditionAsync(() => _receivedMails.Count >= 2);

        (await anna.PutAsJsonAsync($"/users/account/settings/{UserSettingType.CALENDAR_CHANGE_EMAIL}",
            new UpdateUserSettingRequest { Enabled = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        _receivedMails.Clear();

        CalendarEventRequest update = NewRequest("Training verschoben", annaId, bobId);
        update.Version = calendarEvent!.Version;

        // Act
        HttpResponseMessage response = await owner.PutAsJsonAsync($"/calendar/events/{calendarEvent.Id}", update);
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        await Task.Delay(500);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _receivedMails.Count.ShouldBe(1);
        _receivedMails[0].To.ToString().ShouldContain("bob@test.de");
        _receivedMails[0].Subject.ShouldNotBeNull().ShouldContain("Training verschoben");

        await WithDbContext(async db =>
        {
            (await db.Notifications.CountAsync(n =>
                n.UserId == annaId && n.Type == NotificationType.CALENDAR_EVENT_CHANGED)).ShouldBe(1);
        });
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int intervalMs = 100)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException($"Condition not met within {timeoutMs}ms.");
            await Task.Delay(intervalMs);
        }
    }
}
