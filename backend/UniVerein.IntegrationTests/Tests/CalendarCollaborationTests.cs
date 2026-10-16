using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarCollaborationTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CalendarCollaborationTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await WithDbContext(async db =>
        {
            db.Notifications.RemoveRange(db.Notifications.IgnoreQueryFilters());
            db.CalendarEventSubscriptions.RemoveRange(db.CalendarEventSubscriptions.IgnoreQueryFilters());
            db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
            db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();
        });
    }

    private static CalendarEventRequest NewRequest(string title = "Sommerfest",
        CalendarEventVisibility visibility = CalendarEventVisibility.CLUB,
        params CalendarEventParticipantRequest[] participants)
    {
        return new CalendarEventRequest
        {
            Title = title,
            Start = Start,
            End = Start.AddHours(3),
            Visibility = visibility,
            Participants = participants.ToList()
        };
    }

    private static CalendarEventParticipantRequest Invite(Guid userId,
        CalendarParticipantRole role = CalendarParticipantRole.ATTENDEE)
    {
        return new CalendarEventParticipantRequest { UserId = userId, Role = role };
    }

    private async Task<CalendarEventResult> CreateEventAsync(HttpClient client, CalendarEventRequest request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json))!;
    }

    private async Task<CalendarEventResult> UpdateEventAsync(HttpClient client, CalendarEventResult current,
        CalendarEventRequest request)
    {
        request.Version = current.Version;
        HttpResponseMessage response = await client.PutAsJsonAsync($"/calendar/events/{current.Id}", request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json))!;
    }

    private async Task<NotificationResults> GetNotificationsAsync(HttpClient client)
    {
        return (await client.GetFromJsonAsync<NotificationResults>("/notifications", _json))!;
    }

    [Fact]
    public async Task Create_InvitesParticipants_AndSubscribesOwnerAndParticipants()
    {
        // Arrange
        (HttpClient owner, Guid ownerId) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants: Invite(annaId)));
        NotificationResults annaInbox = await GetNotificationsAsync(anna);
        NotificationResults ownerInbox = await GetNotificationsAsync(owner);

        // Assert
        created.IsSubscribed.ShouldBeTrue();
        created.Participants.Single().RsvpStatus.ShouldBe(CalendarRsvpStatus.PENDING);

        annaInbox.Unread.ShouldBe(1);
        NotificationResult invitation = annaInbox.Items.Single();
        invitation.Type.ShouldBe(NotificationType.CALENDAR_INVITATION);
        invitation.EventId.ShouldBe(created.Id);
        invitation.EventTitle.ShouldBe("Sommerfest");
        invitation.ActorUsername.ShouldBe("owner");

        ownerInbox.Total.ShouldBe(0);

        await WithDbContext(async db =>
        {
            List<Guid> subscribers = await db.CalendarEventSubscriptions
                .Where(s => s.EventId == created.Id).Select(s => s.UserId).ToListAsync();
            subscribers.Order().ShouldBe(new[] { ownerId, annaId }.Order());
        });
    }

    [Fact]
    public async Task Update_NotifiesSubscribersExceptActor_WithChangedFields()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient editor, Guid editorId) = await CreateUserAndClientAsync(UserRole.USER, "editor");
        (HttpClient follower, _) = await CreateUserAndClientAsync(UserRole.USER, "follower");

        CalendarEventResult created = await CreateEventAsync(owner,
            NewRequest(participants: Invite(editorId, CalendarParticipantRole.EDITOR)));
        (await follower.PostAsync($"/calendar/events/{created.Id}/subscription", null)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        CalendarEventRequest update = NewRequest("Sommerfest im Park",
            participants: Invite(editorId, CalendarParticipantRole.EDITOR));
        update.Start = Start.AddDays(1);
        update.End = Start.AddDays(1).AddHours(3);

        // Act
        await UpdateEventAsync(editor, created, update);
        NotificationResults ownerInbox = await GetNotificationsAsync(owner);
        NotificationResults followerInbox = await GetNotificationsAsync(follower);
        NotificationResults editorInbox = await GetNotificationsAsync(editor);

        // Assert
        NotificationResult ownerNotification = ownerInbox.Items.Single();
        ownerNotification.Type.ShouldBe(NotificationType.CALENDAR_EVENT_CHANGED);
        ownerNotification.ActorUsername.ShouldBe("editor");
        ownerNotification.Details.Split(',').ShouldBe(["title", "time"], ignoreOrder: true);

        followerInbox.Items.Single().Type.ShouldBe(NotificationType.CALENDAR_EVENT_CHANGED);
        editorInbox.Items.ShouldAllBe(n => n.Type == NotificationType.CALENDAR_INVITATION);
    }

    [Fact]
    public async Task Update_WithoutRelevantChanges_DoesNotNotify()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient follower, _) = await CreateUserAndClientAsync(UserRole.USER, "follower");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest());
        await follower.PostAsync($"/calendar/events/{created.Id}/subscription", null);

        // Act
        await UpdateEventAsync(owner, created, NewRequest());
        NotificationResults followerInbox = await GetNotificationsAsync(follower);

        // Assert
        followerInbox.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Update_NewParticipantGetsInvitationOnly_RemovedParticipantIsUnsubscribed()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (HttpClient bob, Guid bobId) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants: Invite(annaId)));

        // Act
        await UpdateEventAsync(owner, created, NewRequest("Neuer Titel", participants: Invite(bobId)));
        NotificationResults bobInbox = await GetNotificationsAsync(bob);
        NotificationResults annaInbox = await GetNotificationsAsync(anna);
        CalendarEventResult annaView = (await anna.GetFromJsonAsync<CalendarEventResult>(
            $"/calendar/events/{created.Id}", _json))!;

        // Assert
        bobInbox.Items.Select(n => n.Type).ShouldBe([NotificationType.CALENDAR_INVITATION]);
        annaInbox.Items.Select(n => n.Type).ShouldBe([NotificationType.CALENDAR_INVITATION]);
        annaView.IsSubscribed.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_VisibilityRestricted_SubscribersWhoLoseAccessAreNotNotified()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient follower, _) = await CreateUserAndClientAsync(UserRole.USER, "follower");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest());
        await follower.PostAsync($"/calendar/events/{created.Id}/subscription", null);

        // Act
        await UpdateEventAsync(owner, created, NewRequest("Geheim", CalendarEventVisibility.PARTICIPANTS));
        NotificationResults followerInbox = await GetNotificationsAsync(follower);

        // Assert
        followerInbox.Total.ShouldBe(0);
    }

    [Fact]
    public async Task Delete_NotifiesSubscribers()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants: Invite(annaId)));

        // Act
        HttpResponseMessage response = await owner.DeleteAsync($"/calendar/events/{created.Id}");
        NotificationResults annaInbox = await GetNotificationsAsync(anna);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        annaInbox.Items.Select(n => n.Type).ShouldBe(
            [NotificationType.CALENDAR_EVENT_DELETED, NotificationType.CALENDAR_INVITATION], ignoreOrder: true);
    }

    [Fact]
    public async Task Rsvp_ParticipantCanReply_OwnerIsNotified()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants: Invite(annaId)));

        // Act
        HttpResponseMessage response = await anna.PutAsJsonAsync($"/calendar/events/{created.Id}/rsvp",
            new CalendarRsvpRequest { Status = CalendarRsvpStatus.ACCEPTED });
        CalendarEventResult? result = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);
        NotificationResults ownerInbox = await GetNotificationsAsync(owner);
        CalendarEventOccurrenceResults? range = await anna.GetFromJsonAsync<CalendarEventOccurrenceResults>(
            "/calendar/events?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z", _json);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.MyRsvp.ShouldBe(CalendarRsvpStatus.ACCEPTED);
        result.Participants.Single().RsvpStatus.ShouldBe(CalendarRsvpStatus.ACCEPTED);

        NotificationResult reply = ownerInbox.Items.Single();
        reply.Type.ShouldBe(NotificationType.CALENDAR_RSVP);
        reply.Details.ShouldBe("ACCEPTED");
        reply.ActorUsername.ShouldBe("anna");

        range!.Items.Single().MyRsvp.ShouldBe(CalendarRsvpStatus.ACCEPTED);
    }

    [Fact]
    public async Task Rsvp_NotInvited_ReturnsForbidden()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient stranger, _) = await CreateUserAndClientAsync(UserRole.USER, "stranger");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest());

        // Act
        HttpResponseMessage response = await stranger.PutAsJsonAsync($"/calendar/events/{created.Id}/rsvp",
            new CalendarRsvpRequest { Status = CalendarRsvpStatus.ACCEPTED });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Subscription_CannotSubscribeToInvisibleEvent()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient stranger, _) = await CreateUserAndClientAsync(UserRole.USER, "stranger");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(visibility: CalendarEventVisibility.PRIVATE));

        // Act
        HttpResponseMessage response = await stranger.PostAsync($"/calendar/events/{created.Id}/subscription", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Subscription_UnsubscribeStopsNotifications()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants: Invite(annaId)));

        // Act
        HttpResponseMessage response = await anna.DeleteAsync($"/calendar/events/{created.Id}/subscription");
        CalendarEventResult? result = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);
        await UpdateEventAsync(owner, created, NewRequest("Anderer Titel", participants: Invite(annaId)));
        NotificationResults annaInbox = await GetNotificationsAsync(anna);

        // Assert
        result!.IsSubscribed.ShouldBeFalse();
        annaInbox.Items.Select(n => n.Type).ShouldBe([NotificationType.CALENDAR_INVITATION]);
    }

    [Fact]
    public async Task Notifications_MarkReadAndReadAll_OnlyAffectOwnNotifications()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (HttpClient bob, Guid bobId) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        await CreateEventAsync(owner, NewRequest("Eins", participants: [Invite(annaId), Invite(bobId)]));
        await CreateEventAsync(owner, NewRequest("Zwei", participants: Invite(annaId)));
        NotificationResults annaInbox = await GetNotificationsAsync(anna);

        // Act
        HttpResponseMessage bobMarkRead = await bob.PostAsync($"/notifications/{annaInbox.Items[0].Id}/read", null);
        HttpResponseMessage annaMarkRead = await anna.PostAsync($"/notifications/{annaInbox.Items[0].Id}/read", null);
        NotificationUnreadCountResult? unreadCount =
            await anna.GetFromJsonAsync<NotificationUnreadCountResult>("/notifications/unread-count", _json);
        HttpResponseMessage readAll = await anna.PostAsync("/notifications/read-all", null);
        NotificationResults annaInboxAfterReadAll = await GetNotificationsAsync(anna);
        NotificationResults bobInbox = await GetNotificationsAsync(bob);

        // Assert
        annaInbox.Unread.ShouldBe(2);
        bobMarkRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        annaMarkRead.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        unreadCount!.Unread.ShouldBe(1);
        readAll.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        annaInboxAfterReadAll.Unread.ShouldBe(0);
        bobInbox.Unread.ShouldBe(1);
    }

    [Fact]
    public async Task Notifications_Delete_OnlyAffectsOwnNotifications()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (HttpClient bob, Guid bobId) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        await CreateEventAsync(owner, NewRequest("Eins", participants: [Invite(annaId), Invite(bobId)]));
        await CreateEventAsync(owner, NewRequest("Zwei", participants: Invite(annaId)));
        NotificationResults annaInbox = await GetNotificationsAsync(anna);
        Guid deletedId = annaInbox.Items[0].Id;

        // Act
        HttpResponseMessage bobDelete = await bob.DeleteAsync($"/notifications/{deletedId}");
        HttpResponseMessage annaDelete = await anna.DeleteAsync($"/notifications/{deletedId}");
        HttpResponseMessage annaDeleteAgain = await anna.DeleteAsync($"/notifications/{deletedId}");
        NotificationResults annaInboxAfter = await GetNotificationsAsync(anna);
        NotificationResults bobInbox = await GetNotificationsAsync(bob);

        // Assert
        bobDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        annaDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        annaDeleteAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        annaInboxAfter.Items.Select(n => n.Id).ShouldNotContain(deletedId);
        annaInboxAfter.Total.ShouldBe(1);
        annaInboxAfter.Unread.ShouldBe(1);
        bobInbox.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Notifications_DeleteRead_RemovesOnlyOwnReadNotifications()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (HttpClient bob, Guid bobId) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        await CreateEventAsync(owner, NewRequest("Eins", participants: [Invite(annaId), Invite(bobId)]));
        await CreateEventAsync(owner, NewRequest("Zwei", participants: Invite(annaId)));
        NotificationResults annaInbox = await GetNotificationsAsync(anna);
        await anna.PostAsync($"/notifications/{annaInbox.Items[0].Id}/read", null);
        await bob.PostAsync("/notifications/read-all", null);

        // Act
        HttpResponseMessage response = await anna.DeleteAsync("/notifications/read");
        NotificationResults annaInboxAfter = await GetNotificationsAsync(anna);
        NotificationResults bobInbox = await GetNotificationsAsync(bob);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        annaInboxAfter.Items.Select(n => n.Id).ShouldBe([annaInbox.Items[1].Id]);
        annaInboxAfter.Unread.ShouldBe(1);
        bobInbox.Total.ShouldBe(1);
    }

    [Fact]
    public async Task Notifications_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications");
        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/notifications/{Guid.NewGuid()}");
        HttpResponseMessage deleteReadResponse = await client.DeleteAsync("/notifications/read");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        deleteReadResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FirmwareUpdate_StillAdminOnly()
    {
        // Arrange
        (HttpClient user, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        HttpResponseMessage response = await user.GetAsync("/notifications/firmware-update");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
