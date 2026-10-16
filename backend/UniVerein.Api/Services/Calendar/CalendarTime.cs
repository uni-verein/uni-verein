using System;

namespace UniVerein.Api.Services.Calendar;

public static class CalendarTime
{
    public static DateTime ToLocal(DateTimeOffset value, string timeZoneId)
    {
        try
        {
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(value, zone).DateTime;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return value.UtcDateTime;
        }
    }

    public static DateTimeOffset AllDayStart(DateTimeOffset storedStart, string timeZoneId)
    {
        try
        {
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            DateTime localMidnight = DateTime.SpecifyKind(storedStart.UtcDateTime.Date, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(localMidnight))
                localMidnight = localMidnight.AddHours(1);

            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, zone), TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return storedStart;
        }
    }
}
