using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarRecurrenceTests : IntegrationTestBase
{
    // Wednesday 14.10.2026, 18:00 in Berlin (CEST, UTC+2). Daylight saving time ends on 25.10.2026.
    private static readonly DateTimeOffset WednesdayEvening = new(2026, 10, 14, 16, 0, 0, TimeSpan.Zero);

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CalendarRecurrenceTests(UniVereinWebApplicationFactory factory) : base(factory)
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

    private static CalendarEventRequest Weekly(string rule = "FREQ=WEEKLY;BYDAY=WE")
    {
        return new CalendarEventRequest
        {
            Title = "Training",
            Start = WednesdayEvening,
            End = WednesdayEvening.AddHours(2),
            RecurrenceRule = rule
        };
    }

    private async Task<CalendarEventResult> CreateAsync(HttpClient client, CalendarEventRequest request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json))!;
    }

    private async Task<List<CalendarEventOccurrenceResult>> RangeAsync(HttpClient client, string from, string to)
    {
        CalendarEventOccurrenceResults? result = await client.GetFromJsonAsync<CalendarEventOccurrenceResults>(
            $"/calendar/events?from={from}&to={to}", _json);
        return result!.Items;
    }

    [Fact]
    public async Task Weekly_KeepsLocalTimeAcrossDaylightSavingChange()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        await CreateAsync(client, Weekly());

        // Act
        List<CalendarEventOccurrenceResult> occurrences =
            await RangeAsync(client, "2026-10-12T00:00:00Z", "2026-11-02T00:00:00Z");

        // Assert
        occurrences.Select(o => o.Start.UtcDateTime).ShouldBe([
            new DateTime(2026, 10, 14, 16, 0, 0),
            new DateTime(2026, 10, 21, 16, 0, 0),
            new DateTime(2026, 10, 28, 17, 0, 0)
        ]);
        occurrences.ShouldAllBe(o => o.IsRecurring && o.End - o.Start == TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task Count_LimitsOccurrences_AndSeriesEndIsStored()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateAsync(client, Weekly("FREQ=WEEKLY;BYDAY=WE;COUNT=3"));

        // Act
        List<CalendarEventOccurrenceResult> occurrences =
            await RangeAsync(client, "2026-10-01T00:00:00Z", "2027-01-01T00:00:00Z");
        List<CalendarEventOccurrenceResult> afterSeriesEnd =
            await RangeAsync(client, "2026-11-01T00:00:00Z", "2026-12-01T00:00:00Z");

        // Assert
        occurrences.Count.ShouldBe(3);
        afterSeriesEnd.ShouldBeEmpty();
        await WithDbContext(async db =>
        {
            CalendarEventEntity entity = await db.CalendarEvents.SingleAsync(e => e.Id == created.Id);
            entity.SeriesEndAt.ShouldBe(new DateTimeOffset(2026, 10, 28, 19, 0, 0, TimeSpan.Zero));
        });
    }

    [Fact]
    public async Task EndlessSeries_IsFoundFarInTheFuture()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        await CreateAsync(client, Weekly());

        // Act
        List<CalendarEventOccurrenceResult> occurrences =
            await RangeAsync(client, "2030-01-01T00:00:00Z", "2030-02-01T00:00:00Z");

        // Assert
        occurrences.Count.ShouldBe(5);
    }

    [Fact]
    public async Task AllDayYearly_RepeatsOnSameDate()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        await CreateAsync(client, new CalendarEventRequest
        {
            Title = "Vereinsgeburtstag",
            Start = new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 5, 5, 0, 0, 0, TimeSpan.Zero),
            AllDay = true,
            RecurrenceRule = "RRULE:FREQ=YEARLY"
        });

        // Act
        List<CalendarEventOccurrenceResult> occurrences =
        [
            .. await RangeAsync(client, "2027-01-01T00:00:00Z", "2027-12-31T00:00:00Z"),
            .. await RangeAsync(client, "2028-01-01T00:00:00Z", "2028-12-31T00:00:00Z")
        ];

        // Assert
        occurrences.Select(o => o.Start.UtcDateTime).ShouldBe([
            new DateTime(2027, 5, 4), new DateTime(2028, 5, 4)
        ]);
        occurrences.ShouldAllBe(o => o.AllDay);
    }

    [Theory]
    [InlineData("FREQ=MINUTELY")]
    [InlineData("FREQ=HOURLY")]
    [InlineData("FREQ=BOGUS")]
    [InlineData("FREQ=DAILY;COUNT=5000")]
    [InlineData("FREQ=DAILY;BYHOUR=0,1,2,3")]
    [InlineData("FREQ=DAILY;BYMINUTE=0,15,30,45")]
    [InlineData("FREQ=DAILY;BYSECOND=0,30")]
    [InlineData("FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30")]
    [InlineData("FREQ=MONTHLY;BYMONTH=2;BYMONTHDAY=31;COUNT=3")]
    [InlineData("FREQ=MONTHLY;BYDAY=MO;BYSETPOS=9")]
    public async Task InvalidRule_ReturnsBadRequest(string rule)
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", Weekly(rule));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecurringEventStartingBeforeEarliestSeriesStart_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventRequest request = Weekly("FREQ=DAILY");
        request.Start = new DateTimeOffset(1900, 1, 1, 10, 0, 0, TimeSpan.Zero);
        request.End = request.Start.AddHours(1);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SingleEventStartingLongAgo_IsStillAllowed()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventRequest request = Weekly();
        request.RecurrenceRule = null;
        request.Start = new DateTimeOffset(1900, 1, 1, 10, 0, 0, TimeSpan.Zero);
        request.End = request.Start.AddHours(1);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/events", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateAsync_UnevaluableRule_ReturnsBadRequestAndKeepsSeries()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateAsync(client, Weekly());
        CalendarEventRequest update = Weekly("FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30");
        update.Version = created.Version;

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/calendar/events/{created.Id}", update);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        List<CalendarEventOccurrenceResult> occurrences =
            await RangeAsync(client, "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        occurrences.ShouldContain(o => o.EventId == created.Id);
    }

    [Fact]
    public async Task EventWithUnevaluableRuleInDatabase_DoesNotBreakTheCalendar()
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult valid = await CreateAsync(client, Weekly());
        await WithDbContext(async db =>
        {
            db.CalendarEvents.Add(new CalendarEventEntity
            {
                Title = "Broken",
                StartAt = WednesdayEvening,
                EndAt = WednesdayEvening.AddHours(2),
                OwnerUserId = userId,
                RecurrenceRule = "FREQ=YEARLY;BYMONTH=2;BYMONTHDAY=30",
                SeriesEndAt = CalendarRecurrence.EndlessSeriesEnd
            });
            await db.SaveChangesAsync();
        });

        // Act
        List<CalendarEventOccurrenceResult> occurrences =
            await RangeAsync(client, "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");

        // Assert
        occurrences.ShouldNotBeEmpty();
        occurrences.ShouldAllBe(o => o.EventId == valid.Id);
    }

    [Fact]
    public async Task Exception_RemovesSingleOccurrence()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateAsync(client, Weekly());
        DateTimeOffset second = new(2026, 10, 21, 16, 0, 0, TimeSpan.Zero);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/calendar/events/{created.Id}/exceptions",
            new CalendarEventExceptionRequest { OccurrenceStart = second });
        CalendarEventResult? updated = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);
        List<CalendarEventOccurrenceResult> occurrences =
            await RangeAsync(client, "2026-10-12T00:00:00Z", "2026-11-02T00:00:00Z");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated!.RecurrenceExceptions.ShouldBe([second]);
        updated.Version.ShouldBe(created.Version + 1);
        occurrences.Select(o => o.Start.UtcDateTime.Day).ShouldBe([14, 28]);
    }

    [Fact]
    public async Task Exception_ForNonOccurrence_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateAsync(client, Weekly());

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/calendar/events/{created.Id}/exceptions",
            new CalendarEventExceptionRequest { OccurrenceStart = WednesdayEvening.AddHours(1) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Exception_AttendeeCannotRemoveOccurrence()
    {
        // Arrange
        (HttpClient owner, _) = await CreateUserAndClientAsync(UserRole.USER, "owner");
        (HttpClient attendee, Guid attendeeId) = await CreateUserAndClientAsync(UserRole.USER, "attendee");
        CalendarEventRequest request = Weekly();
        request.Participants = [new CalendarEventParticipantRequest { UserId = attendeeId }];
        CalendarEventResult created = await CreateAsync(owner, request);

        // Act
        HttpResponseMessage response = await attendee.PostAsJsonAsync($"/calendar/events/{created.Id}/exceptions",
            new CalendarEventExceptionRequest { OccurrenceStart = WednesdayEvening });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MovingTheSeries_ClearsExceptions()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventResult created = await CreateAsync(client, Weekly());
        HttpResponseMessage exception = await client.PostAsJsonAsync($"/calendar/events/{created.Id}/exceptions",
            new CalendarEventExceptionRequest { OccurrenceStart = WednesdayEvening });
        CalendarEventResult withException = (await exception.Content.ReadFromJsonAsync<CalendarEventResult>(_json))!;

        CalendarEventRequest moved = Weekly();
        moved.Start = WednesdayEvening.AddHours(1);
        moved.End = WednesdayEvening.AddHours(3);
        moved.Version = withException.Version;

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/calendar/events/{created.Id}", moved);
        CalendarEventResult? updated = await response.Content.ReadFromJsonAsync<CalendarEventResult>(_json);

        // Assert
        updated!.RecurrenceExceptions.ShouldBeEmpty();
        updated.RecurrenceRule.ShouldBe("FREQ=WEEKLY;BYDAY=WE");
    }

    [Fact]
    public async Task StartNotMatchingRule_IsMovedToFirstOccurrence()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "anna");
        CalendarEventRequest request = Weekly("FREQ=WEEKLY;BYDAY=FR");

        // Act
        CalendarEventResult created = await CreateAsync(client, request);

        // Assert
        // Wednesday 14.10. 18:00 -> Friday 16.10. 18:00 (Berlin)
        created.Start.ShouldBe(new DateTimeOffset(2026, 10, 16, 16, 0, 0, TimeSpan.Zero));
        created.End.ShouldBe(new DateTimeOffset(2026, 10, 16, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Expand_SingleEvent_OnlyWhenOverlapping()
    {
        // Arrange
        CalendarEventEntity single = new()
        {
            Title = "Einmalig",
            StartAt = WednesdayEvening,
            EndAt = WednesdayEvening.AddHours(2)
        };

        // Act
        List<CalendarOccurrence> overlapping =
            CalendarRecurrence.Expand(single, WednesdayEvening.AddHours(1), WednesdayEvening.AddHours(5));
        List<CalendarOccurrence> afterEnd =
            CalendarRecurrence.Expand(single, WednesdayEvening.AddHours(2), WednesdayEvening.AddHours(5));

        // Assert
        overlapping.ShouldHaveSingleItem();
        afterEnd.ShouldBeEmpty();
    }
}
