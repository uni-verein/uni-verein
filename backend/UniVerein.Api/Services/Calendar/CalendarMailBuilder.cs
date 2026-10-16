using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using UniVerein.Api.ApiRequests;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services.Calendar;

public static class CalendarMailBuilder
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private static readonly Dictionary<string, (string De, string En)> FieldNames = new()
    {
        ["title"] = ("Titel", "Title"),
        ["description"] = ("Beschreibung", "Description"),
        ["time"] = ("Zeit", "Time"),
        ["location"] = ("Ort", "Location"),
        ["visibility"] = ("Sichtbarkeit", "Visibility"),
        ["category"] = ("Kategorie", "Category"),
        ["recurrence"] = ("Wiederholung", "Recurrence")
    };

    public static EmailRequest BuildInvitation(CalendarEventEntity calendarEvent, string actor, string? link,
        List<EmailAttachmentRequest>? attachments = null)
    {
        string title = WebUtility.HtmlEncode(calendarEvent.Title);
        string actorHtml = WebUtility.HtmlEncode(actor);

        string body = Wrap($"📅 Einladung: {title}", $"""
            <h3>🇩🇪 Deutsch</h3>
            <p><strong>{actorHtml}</strong> hat dich zum Termin <strong>{title}</strong> eingeladen.</p>
            {Details(calendarEvent, German, "Wann", "Wo")}
            {LinkHtml(link, "Termin öffnen und zu- oder absagen")}
            <hr/>
            <h3>🇬🇧 English</h3>
            <p><strong>{actorHtml}</strong> invited you to the event <strong>{title}</strong>.</p>
            {Details(calendarEvent, English, "When", "Where")}
            {LinkHtml(link, "Open the event and reply")}
            """);

        return new EmailRequest
        {
            Subject = $"📅 Einladung / Invitation: {calendarEvent.Title}",
            HtmlBody = body,
            Attachments = attachments ?? []
        };
    }

    public static EmailRequest BuildChange(CalendarEventEntity calendarEvent, string actor,
        IReadOnlyCollection<string> changedFields, string? link)
    {
        string title = WebUtility.HtmlEncode(calendarEvent.Title);
        string actorHtml = WebUtility.HtmlEncode(actor);
        string fieldsDe = string.Join(", ", changedFields.Select(f => FieldNames.GetValueOrDefault(f, (De: f, En: f)).De));
        string fieldsEn = string.Join(", ", changedFields.Select(f => FieldNames.GetValueOrDefault(f, (De: f, En: f)).En));

        string body = Wrap($"✏️ Termin geändert: {title}", $"""
            <h3>🇩🇪 Deutsch</h3>
            <p><strong>{actorHtml}</strong> hat den Termin <strong>{title}</strong> geändert.</p>
            <p><strong>Geändert:</strong> {fieldsDe}</p>
            {Details(calendarEvent, German, "Wann", "Wo")}
            {LinkHtml(link, "Termin öffnen")}
            <hr/>
            <h3>🇬🇧 English</h3>
            <p><strong>{actorHtml}</strong> changed the event <strong>{title}</strong>.</p>
            <p><strong>Changed:</strong> {fieldsEn}</p>
            {Details(calendarEvent, English, "When", "Where")}
            {LinkHtml(link, "Open the event")}
            """);

        return new EmailRequest
        {
            Subject = $"✏️ Termin geändert / Event changed: {calendarEvent.Title}",
            HtmlBody = body,
            Attachments = []
        };
    }

    public static EmailRequest BuildDeletion(CalendarEventEntity calendarEvent, string actor)
    {
        string title = WebUtility.HtmlEncode(calendarEvent.Title);
        string actorHtml = WebUtility.HtmlEncode(actor);

        string body = Wrap($"🗑️ Termin abgesagt: {title}", $"""
            <h3>🇩🇪 Deutsch</h3>
            <p><strong>{actorHtml}</strong> hat den Termin <strong>{title}</strong> gelöscht.</p>
            {Details(calendarEvent, German, "Wann", "Wo")}
            <hr/>
            <h3>🇬🇧 English</h3>
            <p><strong>{actorHtml}</strong> deleted the event <strong>{title}</strong>.</p>
            {Details(calendarEvent, English, "When", "Where")}
            """);

        return new EmailRequest
        {
            Subject = $"🗑️ Termin abgesagt / Event cancelled: {calendarEvent.Title}",
            HtmlBody = body,
            Attachments = []
        };
    }

    public static EmailRequest BuildReminder(CalendarEventEntity calendarEvent, DateTimeOffset occurrenceStart,
        string? link)
    {
        string title = WebUtility.HtmlEncode(calendarEvent.Title);

        string body = Wrap($"⏰ Erinnerung: {title}", $"""
            <h3>🇩🇪 Deutsch</h3>
            <p>Erinnerung an den Termin <strong>{title}</strong>.</p>
            {Details(calendarEvent, German, "Wann", "Wo", occurrenceStart)}
            {LinkHtml(link, "Termin öffnen")}
            <hr/>
            <h3>🇬🇧 English</h3>
            <p>Reminder for the event <strong>{title}</strong>.</p>
            {Details(calendarEvent, English, "When", "Where", occurrenceStart)}
            {LinkHtml(link, "Open the event")}
            """);

        return new EmailRequest
        {
            Subject = $"⏰ Erinnerung / Reminder: {calendarEvent.Title}",
            HtmlBody = body,
            Attachments = []
        };
    }

    public static string FormatRange(CalendarEventEntity calendarEvent, DateTimeOffset start, DateTimeOffset end,
        CultureInfo culture)
    {
        string datePattern = culture.TwoLetterISOLanguageName == "de" ? "ddd, d. MMMM yyyy" : "ddd, d MMMM yyyy";

        if (calendarEvent.AllDay)
        {
            DateTime first = start.UtcDateTime.Date;
            DateTime last = end.UtcDateTime.Date.AddDays(-1);
            string from = first.ToString(datePattern, culture);
            return last > first ? $"{from} – {last.ToString(datePattern, culture)}" : from;
        }

        DateTime localStart = CalendarTime.ToLocal(start, calendarEvent.TimeZoneId);
        DateTime localEnd = CalendarTime.ToLocal(end, calendarEvent.TimeZoneId);
        string startText = localStart.ToString($"{datePattern}, HH:mm", culture);
        string endText = localEnd.Date == localStart.Date
            ? localEnd.ToString("HH:mm", culture)
            : localEnd.ToString($"{datePattern}, HH:mm", culture);
        return $"{startText} – {endText}";
    }

    public static string Details(CalendarEventEntity calendarEvent, CultureInfo culture, string whenLabel,
        string whereLabel, DateTimeOffset? occurrenceStart = null)
    {
        DateTimeOffset start = occurrenceStart ?? calendarEvent.StartAt;
        DateTimeOffset end = start + (calendarEvent.EndAt - calendarEvent.StartAt);
        string when = WebUtility.HtmlEncode(FormatRange(calendarEvent, start, end, culture));
        string location = string.Join(", ", new[] { calendarEvent.LocationName, calendarEvent.LocationAddress }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        string html = $"<p><strong>{whenLabel}:</strong> {when}</p>";
        if (!string.IsNullOrWhiteSpace(location))
            html += $"<p><strong>{whereLabel}:</strong> {WebUtility.HtmlEncode(location)}</p>";
        return html;
    }

    public static string LinkHtml(string? link, string label)
    {
        return string.IsNullOrWhiteSpace(link)
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{label}</a></p>";
    }

    public static string Wrap(string heading, string content)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>{heading}</title>
            </head>
            <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
                <h2>{heading}</h2>
                <hr/>
                {content}
            </body>
            </html>
            """;
    }
}
