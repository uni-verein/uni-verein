using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarFeedAndPublicCalendarTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow.Date.AddDays(10).AddHours(16);
    private static readonly string Range =
        $"from={Start.AddDays(-5):yyyy-MM-ddTHH:mm:ssZ}&to={Start.AddDays(30):yyyy-MM-ddTHH:mm:ssZ}";

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CalendarFeedAndPublicCalendarTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
        await WithDbContext(async db =>
        {
            db.CalendarFeedTokens.RemoveRange(db.CalendarFeedTokens.IgnoreQueryFilters());
            db.CalendarSettings.RemoveRange(db.CalendarSettings.IgnoreQueryFilters());
            db.Notifications.RemoveRange(db.Notifications.IgnoreQueryFilters());
            db.CalendarEventSubscriptions.RemoveRange(db.CalendarEventSubscriptions.IgnoreQueryFilters());
            db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
            db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
            db.CalendarCategories.RemoveRange(db.CalendarCategories.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();
        });
    }

    private static CalendarEventRequest NewRequest(string title,
        CalendarEventVisibility visibility = CalendarEventVisibility.CLUB, bool isPublic = false,
        Guid? categoryId = null, List<CalendarEventParticipantRequest>? participants = null)
    {
        return new CalendarEventRequest
        {
            Title = title,
            Description = "Beschreibung",
            LocationName = "Stadtpark",
            Start = Start,
            End = Start.AddHours(2),
            Visibility = visibility,
            IsPublic = isPublic,
            CategoryId = categoryId,
            Participants = participants ?? []
        };
    }

    private async Task<CalendarEventResult> CreateEventAsync(HttpClient client, CalendarEventRequest request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json))!;
    }

    private async Task<Guid> CreateCategoryAsync(string name, bool isPublic)
    {
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = name, Color = "#16a34a", IsPublic = isPublic });
        return (await response.Content.ReadFromJsonAsync<CalendarCategoryResult>(_json))!.Id;
    }

    private async Task<CalendarSettingsResult> SetPublicCalendarAsync(bool enabled, bool requireKey = false,
        bool regenerateKey = false)
    {
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).PutAsJsonAsync("/calendar/settings",
            new CalendarSettingsRequest
            {
                PublicCalendarEnabled = enabled,
                PublicCalendarTitle = "Vereinskalender",
                RequireKey = requireKey,
                RegenerateKey = regenerateKey
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CalendarSettingsResult>(_json))!;
    }

    // ---------------------------------------------------------------
    // Personal feed
    // ---------------------------------------------------------------

    [Fact]
    public async Task Feed_ContainsAllEventsTheUserCanSee_IncludingOwnPrivateOnes()
    {
        // Arrange
        (HttpClient anna, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        (HttpClient bob, _) = await CreateUserAndClientAsync(UserRole.USER, "bob");
        await CreateEventAsync(anna, NewRequest("Annas Arzttermin", CalendarEventVisibility.PRIVATE));
        await CreateEventAsync(bob, NewRequest("Bobs Geheimnis", CalendarEventVisibility.PRIVATE));
        CalendarEventRequest weekly = NewRequest("Training");
        weekly.RecurrenceRule = "FREQ=WEEKLY";
        CalendarEventResult training = await CreateEventAsync(bob, weekly);
        // Take the real second occurrence: the series keeps local time, so Start + 7 days is wrong across DST changes
        CalendarEventOccurrenceResults? occurrences =
            await bob.GetFromJsonAsync<CalendarEventOccurrenceResults>($"/calendar/events?{Range}", _json);
        DateTimeOffset secondOccurrence = occurrences!.Items.Where(i => i.Title == "Training")
            .Select(i => i.Start).Order().Skip(1).First();
        (await bob.PostAsJsonAsync($"/calendar/events/{training.Id}/exceptions",
                new CalendarEventExceptionRequest { OccurrenceStart = secondOccurrence }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage tokenResponse = await anna.PostAsync("/calendar/feed/token", null);
        CalendarFeedTokenResult? token = await tokenResponse.Content.ReadFromJsonAsync<CalendarFeedTokenResult>(_json);

        // Act
        HttpResponseMessage feed = await CreateClient().GetAsync(token!.Path);
        string ics = await feed.Content.ReadAsStringAsync();
        CalendarFeedStatusResult? status =
            await anna.GetFromJsonAsync<CalendarFeedStatusResult>("/calendar/feed/token", _json);

        // Assert
        feed.StatusCode.ShouldBe(HttpStatusCode.OK);
        feed.Content.Headers.ContentType!.MediaType.ShouldBe("text/calendar");
        ics.ShouldStartWith("BEGIN:VCALENDAR");
        ics.ShouldContain("SUMMARY:Annas Arzttermin");
        ics.ShouldContain("SUMMARY:Training");
        ics.ShouldContain("RRULE:FREQ=WEEKLY");
        ics.ShouldContain("EXDATE");
        ics.ShouldContain($"UID:{training.Id}@uni-verein");
        ics.ShouldNotContain("Bobs Geheimnis");

        status!.Exists.ShouldBeTrue();
        status.LastUsedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Feed_RevokedOrReplacedToken_ReturnsNotFound()
    {
        // Arrange
        (HttpClient anna, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        HttpClient anonymous = CreateClient();
        CalendarFeedTokenResult? first = await (await anna.PostAsync("/calendar/feed/token", null))
            .Content.ReadFromJsonAsync<CalendarFeedTokenResult>(_json);
        CalendarFeedTokenResult? second = await (await anna.PostAsync("/calendar/feed/token", null))
            .Content.ReadFromJsonAsync<CalendarFeedTokenResult>(_json);

        // Act
        HttpResponseMessage replacedFeed = await anonymous.GetAsync(first!.Path);
        HttpResponseMessage currentFeed = await anonymous.GetAsync(second!.Path);
        HttpResponseMessage revokeResponse = await anna.DeleteAsync("/calendar/feed/token");
        HttpResponseMessage revokedFeed = await anonymous.GetAsync(second.Path);
        HttpResponseMessage unknownFeed = await anonymous.GetAsync("/calendar/feed/not-a-token.ics");

        // Assert
        replacedFeed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        currentFeed.StatusCode.ShouldBe(HttpStatusCode.OK);
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        revokedFeed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownFeed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feed_TokenIsStoredOnlyAsHash()
    {
        // Arrange
        (HttpClient anna, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        CalendarFeedTokenResult? token = await (await anna.PostAsync("/calendar/feed/token", null))
            .Content.ReadFromJsonAsync<CalendarFeedTokenResult>(_json);

        // Assert
        await WithDbContext(async db =>
        {
            CalendarFeedTokenEntity stored = await db.CalendarFeedTokens.SingleAsync(t => t.UserId == annaId);
            stored.TokenHash.ShouldNotBe(token!.Token);
            stored.TokenHash.Length.ShouldBe(64);
        });
    }

    // ---------------------------------------------------------------
    // Public calendar
    // ---------------------------------------------------------------

    [Fact]
    public async Task PublicCalendar_Disabled_ReturnsNotFound()
    {
        // Arrange
        HttpClient anonymous = CreateClient();

        // Act
        HttpResponseMessage infoResponse = await anonymous.GetAsync("/public/calendar/info");
        HttpResponseMessage eventsResponse = await anonymous.GetAsync($"/public/calendar/events?{Range}");
        HttpResponseMessage feedResponse = await anonymous.GetAsync("/public/calendar/feed.ics");

        // Assert
        infoResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        eventsResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        feedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicCalendar_ShowsOnlyPublicClubEvents_WithoutPersonalData()
    {
        // Arrange
        await SetPublicCalendarAsync(enabled: true);
        Guid publicCategory = await CreateCategoryAsync("Öffentlich", isPublic: true);
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (_, Guid annaId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        HttpClient anonymous = CreateClient();

        await CreateEventAsync(owner, NewRequest("Tag der offenen Tür", isPublic: true,
            participants: [new CalendarEventParticipantRequest { UserId = annaId }]));
        await CreateEventAsync(owner, NewRequest("Konzert", categoryId: publicCategory));
        await CreateEventAsync(owner, NewRequest("Internes Treffen"));
        await CreateEventAsync(owner, NewRequest("Vorstand", CalendarEventVisibility.PARTICIPANTS,
            categoryId: publicCategory));
        await CreateEventAsync(owner, NewRequest("Privat", CalendarEventVisibility.PRIVATE,
            categoryId: publicCategory));

        // Act
        HttpResponseMessage response = await anonymous.GetAsync($"/public/calendar/events?{Range}");
        string raw = await response.Content.ReadAsStringAsync();
        PublicCalendarOccurrenceResults? result = JsonSerializer.Deserialize<PublicCalendarOccurrenceResults>(raw, _json);
        PublicCalendarInfoResult? info =
            await anonymous.GetFromJsonAsync<PublicCalendarInfoResult>("/public/calendar/info", _json);
        string ics = await anonymous.GetStringAsync("/public/calendar/feed.ics");
        string categoryIcs = await anonymous.GetStringAsync($"/public/calendar/feed.ics?categoryId={publicCategory}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.Items.Select(i => i.Title).Order().ShouldBe(["Konzert", "Tag der offenen Tür"]);
        raw.ShouldNotContain("anna");
        raw.ShouldNotContain("owner");
        raw.ShouldNotContain("participants");

        info!.Title.ShouldBe("Vereinskalender");
        info.Categories.Select(c => c.Name).ShouldBe(["Öffentlich"]);

        ics.ShouldContain("SUMMARY:Konzert");
        ics.ShouldContain("SUMMARY:Tag der offenen Tür");
        ics.ShouldNotContain("Internes Treffen");
        ics.ShouldNotContain("Vorstand");

        categoryIcs.ShouldContain("SUMMARY:Konzert");
        categoryIcs.ShouldNotContain("Tag der offenen Tür");
    }

    [Fact]
    public async Task PublicCalendar_WithKey_RequiresTheRightKey()
    {
        // Arrange
        CalendarSettingsResult settings = await SetPublicCalendarAsync(enabled: true, requireKey: true);
        HttpClient anonymous = CreateClient();

        // Act
        HttpResponseMessage withoutKey = await anonymous.GetAsync("/public/calendar/info");
        HttpResponseMessage wrongKey = await anonymous.GetAsync("/public/calendar/info?key=wrong");
        HttpResponseMessage rightKey = await anonymous.GetAsync($"/public/calendar/info?key={settings.PublicCalendarKey}");
        CalendarSettingsResult regenerated =
            await SetPublicCalendarAsync(enabled: true, requireKey: true, regenerateKey: true);
        HttpResponseMessage oldKey = await anonymous.GetAsync($"/public/calendar/feed.ics?key={settings.PublicCalendarKey}");

        // Assert
        settings.PublicCalendarKey.ShouldNotBeNullOrWhiteSpace();
        withoutKey.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        wrongKey.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        rightKey.StatusCode.ShouldBe(HttpStatusCode.OK);
        regenerated.PublicCalendarKey.ShouldNotBe(settings.PublicCalendarKey);
        oldKey.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Settings_ReadableByUsers_WritableByAdminsOnly()
    {
        // Arrange
        (HttpClient user, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        HttpResponseMessage getResponse = await user.GetAsync("/calendar/settings");
        HttpResponseMessage putResponse = await user.PutAsJsonAsync("/calendar/settings",
            new CalendarSettingsRequest { PublicCalendarEnabled = true });

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicFlag_OnlyAllowedForClubEvents()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");

        // Act
        HttpResponseMessage response = await owner.PostAsJsonAsync("/calendar/events",
            NewRequest("Vorstand", CalendarEventVisibility.PARTICIPANTS, isPublic: true));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Occurrences_ReportWhetherEventIsPublic()
    {
        // Arrange
        Guid publicCategory = await CreateCategoryAsync("Öffentlich", isPublic: true);
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        await CreateEventAsync(owner, NewRequest("Konzert", categoryId: publicCategory));
        await CreateEventAsync(owner, NewRequest("Intern"));

        // Act
        CalendarEventOccurrenceResults? result =
            await owner.GetFromJsonAsync<CalendarEventOccurrenceResults>($"/calendar/events?{Range}", _json);

        // Assert
        result!.Items.Single(i => i.Title == "Konzert").IsPublic.ShouldBeTrue();
        result.Items.Single(i => i.Title == "Intern").IsPublic.ShouldBeFalse();
    }
}
