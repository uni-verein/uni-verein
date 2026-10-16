using Shouldly;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid EditorId = Guid.NewGuid();
    private static readonly Guid AttendeeId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static CalendarEventEntity NewEvent(CalendarEventVisibility visibility)
    {
        return new CalendarEventEntity
        {
            Title = "Event",
            OwnerUserId = OwnerId,
            Visibility = visibility,
            Participants =
            [
                new CalendarEventParticipantEntity { UserId = EditorId, Role = CalendarParticipantRole.EDITOR },
                new CalendarEventParticipantEntity { UserId = AttendeeId, Role = CalendarParticipantRole.ATTENDEE }
            ]
        };
    }

    private static (Guid Id, bool IsAdmin) Actor(string name)
    {
        return name switch
        {
            "owner" => (OwnerId, false),
            "editor" => (EditorId, false),
            "attendee" => (AttendeeId, false),
            "admin" => (AdminId, true),
            _ => (StrangerId, false)
        };
    }

    [Theory]
    [InlineData("owner", CalendarEventVisibility.CLUB, true, true, true)]
    [InlineData("owner", CalendarEventVisibility.PARTICIPANTS, true, true, true)]
    [InlineData("owner", CalendarEventVisibility.PRIVATE, true, true, true)]
    [InlineData("editor", CalendarEventVisibility.CLUB, true, true, false)]
    [InlineData("editor", CalendarEventVisibility.PARTICIPANTS, true, true, false)]
    [InlineData("editor", CalendarEventVisibility.PRIVATE, false, false, false)]
    [InlineData("attendee", CalendarEventVisibility.CLUB, true, false, false)]
    [InlineData("attendee", CalendarEventVisibility.PARTICIPANTS, true, false, false)]
    [InlineData("attendee", CalendarEventVisibility.PRIVATE, false, false, false)]
    [InlineData("admin", CalendarEventVisibility.CLUB, true, true, true)]
    [InlineData("admin", CalendarEventVisibility.PARTICIPANTS, true, true, true)]
    [InlineData("admin", CalendarEventVisibility.PRIVATE, false, false, false)]
    [InlineData("stranger", CalendarEventVisibility.CLUB, true, false, false)]
    [InlineData("stranger", CalendarEventVisibility.PARTICIPANTS, false, false, false)]
    [InlineData("stranger", CalendarEventVisibility.PRIVATE, false, false, false)]
    public void Permissions_FollowTheVisibilityRules(string actor, CalendarEventVisibility visibility, bool canView,
        bool canEdit, bool canDelete)
    {
        // Arrange
        CalendarEventEntity calendarEvent = NewEvent(visibility);
        (Guid userId, bool isAdmin) = Actor(actor);

        // Act & Assert
        CalendarService.CanView(calendarEvent, userId, isAdmin).ShouldBe(canView);
        CalendarService.CanEdit(calendarEvent, userId, isAdmin).ShouldBe(canEdit);
        CalendarService.CanDelete(calendarEvent, userId, isAdmin).ShouldBe(canDelete);
    }

    [Theory]
    [InlineData("owner", CalendarEventVisibility.PRIVATE, true)]
    [InlineData("admin", CalendarEventVisibility.PRIVATE, false)]
    [InlineData("stranger", CalendarEventVisibility.PRIVATE, false)]
    [InlineData("admin", CalendarEventVisibility.PARTICIPANTS, true)]
    [InlineData("stranger", CalendarEventVisibility.PARTICIPANTS, false)]
    [InlineData("stranger", CalendarEventVisibility.CLUB, true)]
    public void VisibleTo_MatchesCanView(string actor, CalendarEventVisibility visibility, bool visible)
    {
        // Arrange
        CalendarEventEntity calendarEvent = NewEvent(visibility);
        (Guid userId, bool isAdmin) = Actor(actor);

        // Act
        List<CalendarEventEntity> result = CalendarService
            .VisibleTo(new[] { calendarEvent }.AsQueryable(), userId, isAdmin).ToList();

        // Assert
        (result.Count == 1).ShouldBe(visible);
        CalendarService.CanView(calendarEvent, userId, isAdmin).ShouldBe(visible);
    }
}
