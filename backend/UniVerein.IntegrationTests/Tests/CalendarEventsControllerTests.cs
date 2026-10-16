using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarEventsControllerTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 18, 0, 0, TimeSpan.Zero);
    private const string RangeQuery = "/calendar/events?from=2026-11-01T00:00:00Z&to=2026-12-01T00:00:00Z";

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CalendarEventsControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await WithDbContext(async db =>
        {
            db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
            db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
            db.CalendarCategories.RemoveRange(db.CalendarCategories.IgnoreQueryFilters());
            db.AuditLogs.RemoveRange(db.AuditLogs.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();
        });
    }

    private static CalendarEventRequest NewRequest(string title = "Sommerfest",
        CalendarEventVisibility visibility = CalendarEventVisibility.CLUB,
        List<CalendarEventParticipantRequest>? participants = null)
    {
        return new CalendarEventRequest
        {
            Title = title,
            Description = "Grillen am See",
            Start = Start,
            End = Start.AddHours(3),
            LocationName = "Stadtpark",
            LocationAddress = "Parkstraße 1, 12345 Musterstadt",
            LocationUrl = "https://www.openstreetmap.org/",
            Visibility = visibility,
            Participants = participants ?? new List<CalendarEventParticipantRequest>()
        };
    }

    private async Task<CalendarEventResult> CreateEventAsync(HttpClient client, CalendarEventRequest request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        CalendarEventResult? result = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);
        result.ShouldNotBeNull();
        return result;
    }

    private async Task<List<CalendarEventOccurrenceResult>> GetRangeAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync(RangeQuery);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CalendarEventOccurrenceResults? result =
            await response.Content.ReadFromJsonAsync<CalendarEventOccurrenceResults>(_json);
        result.ShouldNotBeNull();
        return result.Items;
    }

    [Fact]
    public async Task GetAllAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(RangeQuery);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllAsync_InvalidRange_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        HttpResponseMessage response =
            await client.GetAsync("/calendar/events?from=2026-12-01T00:00:00Z&to=2026-11-01T00:00:00Z");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_StoresEventWithLocationAndOwner()
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        CalendarEventResult result = await CreateEventAsync(client, NewRequest());

        // Assert
        result.Title.ShouldBe("Sommerfest");
        result.LocationName.ShouldBe("Stadtpark");
        result.LocationAddress.ShouldBe("Parkstraße 1, 12345 Musterstadt");
        result.Owner.Id.ShouldBe(userId);
        result.Owner.Username.ShouldBe("anna");
        result.Version.ShouldBe(1);
        result.CanEdit.ShouldBeTrue();
        result.CanDelete.ShouldBeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org")]
    [InlineData("not a url")]
    public async Task CreateAsync_InvalidLocationUrl_ReturnsBadRequest(string url)
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventRequest request = NewRequest();
        request.LocationUrl = url;

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_EndBeforeStart_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventRequest request = NewRequest();
        request.End = request.Start.AddHours(-1);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_UndefinedVisibility_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events",
            NewRequest(visibility: (CalendarEventVisibility)99));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetRangeAsync(client)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_PrivateWithParticipants_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (_, Guid bob) = await CreateUserAndClientAsync(UserRole.USER, "bob");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events",
            NewRequest(visibility: CalendarEventVisibility.PRIVATE,
                participants: [new CalendarEventParticipantRequest { UserId = bob }]));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_DuplicateParticipant_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (_, Guid bob) = await CreateUserAndClientAsync(UserRole.USER, "bob");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events",
            NewRequest(participants:
            [
                new CalendarEventParticipantRequest { UserId = bob },
                new CalendarEventParticipantRequest { UserId = bob, Role = CalendarParticipantRole.EDITOR }
            ]));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateParticipant_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (_, Guid bob) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        CalendarEventResult created = await CreateEventAsync(client, NewRequest());
        CalendarEventRequest update = NewRequest(participants:
        [
            new CalendarEventParticipantRequest { UserId = bob },
            new CalendarEventParticipantRequest { UserId = bob }
        ]);
        update.Version = created.Version;

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/calendar/events/{created.Id}", update);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Visibility_ClubParticipantsPrivate_Matrix()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient participant, Guid participantId) = await CreateUserAndClientAsync(UserRole.USER, "participant");
        (HttpClient stranger, _) = await CreateUserAndClientAsync(UserRole.USER, "stranger");
        (HttpClient admin, _) = await CreateUserAndClientAsync(UserRole.ADMIN, "chef");

        await CreateEventAsync(owner, NewRequest("Club"));
        await CreateEventAsync(owner, NewRequest("Board", CalendarEventVisibility.PARTICIPANTS,
            [new CalendarEventParticipantRequest { UserId = participantId }]));
        CalendarEventResult privateEvent =
            await CreateEventAsync(owner, NewRequest("Dentist", CalendarEventVisibility.PRIVATE));

        // Act
        List<CalendarEventOccurrenceResult> ownerEvents = await GetRangeAsync(owner);
        List<CalendarEventOccurrenceResult> participantEvents = await GetRangeAsync(participant);
        List<CalendarEventOccurrenceResult> strangerEvents = await GetRangeAsync(stranger);
        List<CalendarEventOccurrenceResult> adminEvents = await GetRangeAsync(admin);
        HttpResponseMessage adminPrivateResponse = await admin.GetAsync($"/calendar/events/{privateEvent.Id}");
        HttpResponseMessage strangerPrivateResponse = await stranger.GetAsync($"/calendar/events/{privateEvent.Id}");

        // Assert
        ownerEvents.Select(e => e.Title).Order().ShouldBe(["Board", "Club", "Dentist"]);
        participantEvents.Select(e => e.Title).Order().ShouldBe(["Board", "Club"]);
        strangerEvents.Select(e => e.Title).ShouldBe(["Club"]);
        adminEvents.Select(e => e.Title).Order().ShouldBe(["Board", "Club"]);

        adminPrivateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        strangerPrivateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllAsync_MineOnly_ReturnsOwnAndParticipatingEvents()
    {
        // Arrange
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (HttpClient bob, _) = await CreateUserAndClientAsync(UserRole.USER, "bob");

        await CreateEventAsync(anna, NewRequest("Anna"));
        await CreateEventAsync(bob, NewRequest("Bob invites Anna", participants:
            [new CalendarEventParticipantRequest { UserId = annaId }]));
        await CreateEventAsync(bob, NewRequest("Bob alone"));

        // Act
        HttpResponseMessage response = await anna.GetAsync(RangeQuery + "&mineOnly=true");
        CalendarEventOccurrenceResults? result =
            await response.Content.ReadFromJsonAsync<CalendarEventOccurrenceResults>(_json);

        // Assert
        result!.Items.Select(e => e.Title).Order().ShouldBe(["Anna", "Bob invites Anna"]);
    }

    [Fact]
    public async Task UpdateAsync_EditorCanEdit_AttendeeAndStrangerCannot()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient editor, Guid editorId) = await CreateUserAndClientAsync(UserRole.USER, "editor");
        (HttpClient attendee, Guid attendeeId) = await CreateUserAndClientAsync(UserRole.USER, "attendee");
        (HttpClient stranger, _) = await CreateUserAndClientAsync(UserRole.USER, "stranger");

        List<CalendarEventParticipantRequest> participants =
        [
            new() { UserId = editorId, Role = CalendarParticipantRole.EDITOR },
            new() { UserId = attendeeId, Role = CalendarParticipantRole.ATTENDEE }
        ];
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants: participants));

        CalendarEventRequest update = NewRequest("Sommerfest 2.0", participants: participants);
        update.Version = created.Version;

        // Act
        HttpResponseMessage attendeeResponse = await attendee.PutAsJsonAsync($"/calendar/events/{created.Id}", update);
        HttpResponseMessage strangerResponse = await stranger.PutAsJsonAsync($"/calendar/events/{created.Id}", update);
        HttpResponseMessage response = await editor.PutAsJsonAsync($"/calendar/events/{created.Id}", update);
        CalendarEventResult? updated = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);

        // Assert
        attendeeResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        strangerResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated!.Title.ShouldBe("Sommerfest 2.0");
        updated.Version.ShouldBe(2);
        updated.LastModifiedBy!.Username.ShouldBe("editor");
        updated.CanDelete.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ReturnsConflict()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest());

        CalendarEventRequest first = NewRequest("First");
        first.Version = created.Version;
        (await owner.PutAsJsonAsync($"/calendar/events/{created.Id}", first)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        CalendarEventRequest second = NewRequest("Second");
        second.Version = created.Version;

        // Act
        HttpResponseMessage response = await owner.PutAsJsonAsync($"/calendar/events/{created.Id}", second);
        CalendarEventResult? current = await owner.GetFromJsonAsync<CalendarEventResult>(
            $"/calendar/events/{created.Id}", _json);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        current!.Title.ShouldBe("First");
    }

    [Fact]
    public async Task UpdateAsync_EditorCannotMakeEventPrivate()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient editor, Guid editorId) = await CreateUserAndClientAsync(UserRole.USER, "editor");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants:
            [new CalendarEventParticipantRequest { UserId = editorId, Role = CalendarParticipantRole.EDITOR }]));

        CalendarEventRequest update = NewRequest(visibility: CalendarEventVisibility.PRIVATE);
        update.Version = created.Version;

        // Act
        HttpResponseMessage response = await editor.PutAsJsonAsync($"/calendar/events/{created.Id}", update);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAsync_UndefinedVisibility_ReturnsBadRequestAndKeepsEventVisible()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient editor, Guid editorId) = await CreateUserAndClientAsync(UserRole.USER, "editor");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants:
            [new CalendarEventParticipantRequest { UserId = editorId, Role = CalendarParticipantRole.EDITOR }]));

        CalendarEventRequest update = NewRequest(visibility: (CalendarEventVisibility)99);
        update.Version = created.Version;

        // Act
        HttpResponseMessage response = await editor.PutAsJsonAsync($"/calendar/events/{created.Id}", update);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetRangeAsync(editor)).ShouldContain(e => e.EventId == created.Id);
    }

    [Fact]
    public async Task Admin_CannotEditOrDeleteForeignPrivateEvent()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient admin, _) = await CreateUserAndClientAsync(UserRole.ADMIN, "chef");
        CalendarEventResult created =
            await CreateEventAsync(owner, NewRequest("Dentist", CalendarEventVisibility.PRIVATE));
        CalendarEventRequest update = NewRequest("Changed", CalendarEventVisibility.PRIVATE);
        update.Version = created.Version;

        // Act
        HttpResponseMessage updateResponse = await admin.PutAsJsonAsync($"/calendar/events/{created.Id}", update);
        HttpResponseMessage deleteResponse = await admin.DeleteAsync($"/calendar/events/{created.Id}");
        List<CalendarEventOccurrenceResult> ownerEvents = await GetRangeAsync(owner);

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ownerEvents.Select(e => e.Title).ShouldBe(["Dentist"]);
    }

    [Fact]
    public async Task Admin_CanEditAndDeleteParticipantsEventWithoutInvitation()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (_, Guid attendeeId) = await CreateUserAndClientAsync(UserRole.USER, "attendee");
        (HttpClient admin, _) = await CreateUserAndClientAsync(UserRole.ADMIN, "chef");
        List<CalendarEventParticipantRequest> participants = [new() { UserId = attendeeId }];
        CalendarEventResult created = await CreateEventAsync(owner,
            NewRequest("Board", CalendarEventVisibility.PARTICIPANTS, participants));
        CalendarEventRequest update = NewRequest("Board moved", CalendarEventVisibility.PARTICIPANTS, participants);
        update.Version = created.Version;

        // Act
        HttpResponseMessage updateResponse = await admin.PutAsJsonAsync($"/calendar/events/{created.Id}", update);
        HttpResponseMessage deleteResponse = await admin.DeleteAsync($"/calendar/events/{created.Id}");

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetRangeAsync(owner)).ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_RemovesAndAddsParticipants()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (_, Guid anna) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (_, Guid bob) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        CalendarEventResult created = await CreateEventAsync(owner, NewRequest(participants:
            [new CalendarEventParticipantRequest { UserId = anna }]));

        CalendarEventRequest update = NewRequest(participants:
            [new CalendarEventParticipantRequest { UserId = bob, Role = CalendarParticipantRole.EDITOR }]);
        update.Version = created.Version;

        // Act
        HttpResponseMessage response = await owner.PutAsJsonAsync($"/calendar/events/{created.Id}", update);
        CalendarEventResult? updated = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);

        // Assert
        updated!.Participants.Select(p => (p.Username, p.Role))
            .ShouldBe([("bob", CalendarParticipantRole.EDITOR)]);
    }

    [Fact]
    public async Task DeleteAsync_OwnerAndAdminCanDelete_EditorCannot()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient editor, Guid editorId) = await CreateUserAndClientAsync(UserRole.USER, "editor");
        (HttpClient admin, _) = await CreateUserAndClientAsync(UserRole.ADMIN, "chef");
        List<CalendarEventParticipantRequest> participants =
            [new() { UserId = editorId, Role = CalendarParticipantRole.EDITOR }];

        CalendarEventResult first = await CreateEventAsync(owner, NewRequest("First", participants: participants));
        CalendarEventResult second = await CreateEventAsync(owner, NewRequest("Second"));

        // Act
        HttpResponseMessage editorResponse = await editor.DeleteAsync($"/calendar/events/{first.Id}");
        HttpResponseMessage ownerResponse = await owner.DeleteAsync($"/calendar/events/{first.Id}");
        HttpResponseMessage adminResponse = await admin.DeleteAsync($"/calendar/events/{second.Id}");
        List<CalendarEventOccurrenceResult> remaining = await GetRangeAsync(owner);

        // Assert
        editorResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ownerResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task AuditLog_DoesNotContainTitleOfPrivateEvents()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");

        // Act
        await CreateEventAsync(owner, NewRequest("Arzttermin", CalendarEventVisibility.PRIVATE));

        // Assert
        await WithDbContext(async db =>
        {
            List<AuditLogEntity> logs = await db.AuditLogs.Where(l => l.Entity == nameof(CalendarEventEntity))
                .ToListAsync();
            logs.ShouldHaveSingleItem();
            logs[0].Data.ShouldNotContain("Arzttermin");
        });
    }

    [Fact]
    public async Task CalendarUsers_ReturnsUsernamesForEveryRole()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        CalendarUserResults? result = await client.GetFromJsonAsync<CalendarUserResults>("/calendar/users", _json);

        // Assert
        result!.Items.Select(u => u.Username).ShouldContain("anna");
    }
}
