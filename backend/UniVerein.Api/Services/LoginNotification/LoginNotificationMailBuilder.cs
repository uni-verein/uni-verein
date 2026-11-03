using System;
using System.Globalization;
using System.Net;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Services.Calendar;

namespace UniVerein.Api.Services.LoginNotification;

public static class LoginNotificationMailBuilder
{
    private const int MaxUserAgentLength = 200;

    public static EmailRequest Build(string username, LoginNotificationJob job, string? appLink)
    {
        DeviceDescription device = UserAgentParser.Parse(job.UserAgent);
        string userHtml = Encode(username);
        string ipHtml = Encode(job.IpAddress);
        string timeHtml = Encode(job.OccurredAt.UtcDateTime.ToString("dd.MM.yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture));
        string deviceDe = Encode(DescribeDevice(device, job.UserAgent, "Unbekanntes Gerät"));
        string deviceEn = Encode(DescribeDevice(device, job.UserAgent, "Unknown device"));

        string pendingDe = job.TwoFactorPending
            ? "<p>Das Passwort wurde korrekt eingegeben, der zweite Faktor steht noch aus. Der Login ist noch nicht abgeschlossen.</p>"
            : string.Empty;
        string pendingEn = job.TwoFactorPending
            ? "<p>The password was entered correctly, the second factor is still missing. The login is not completed yet.</p>"
            : string.Empty;

        string body = CalendarMailBuilder.Wrap("🔐 Anmeldung auf neuem Gerät / Login from a new device", $"""
            <h3>🇩🇪 Deutsch</h3>
            <p>Für dein Konto <strong>{userHtml}</strong> wurde eine Anmeldung auf einem neuen Gerät festgestellt.</p>
            {pendingDe}
            <p><strong>Gerät:</strong> {deviceDe}</p>
            <p><strong>IP-Adresse:</strong> {ipHtml}</p>
            <p><strong>Zeitpunkt:</strong> {timeHtml}</p>
            <p><strong>Das warst nicht du?</strong> Ändere sofort dein Passwort (Profileinstellungen → Sicherheit). Dabei werden alle laufenden Anmeldungen beendet.</p>
            {CalendarMailBuilder.LinkHtml(appLink, "Zu UniVerein")}
            <hr/>
            <h3>🇬🇧 English</h3>
            <p>A login to your account <strong>{userHtml}</strong> from a new device was detected.</p>
            {pendingEn}
            <p><strong>Device:</strong> {deviceEn}</p>
            <p><strong>IP address:</strong> {ipHtml}</p>
            <p><strong>Time:</strong> {timeHtml}</p>
            <p><strong>Wasn't you?</strong> Change your password immediately (profile settings → security). This ends all running sessions.</p>
            {CalendarMailBuilder.LinkHtml(appLink, "Open UniVerein")}
            """);

        return new EmailRequest
        {
            Subject = "🔐 Anmeldung auf neuem Gerät / Login from a new device",
            HtmlBody = body
        };
    }

    private static string DescribeDevice(DeviceDescription device, string? rawUserAgent, string unknown)
    {
        if (device.Browser != null && device.OperatingSystem != null)
            return $"{device.Browser} ({device.OperatingSystem})";
        if (device.Browser != null)
            return device.Browser;
        if (device.OperatingSystem != null)
            return device.OperatingSystem;

        if (string.IsNullOrWhiteSpace(rawUserAgent))
            return unknown;

        string trimmed = rawUserAgent.Length > MaxUserAgentLength ? rawUserAgent[..MaxUserAgentLength] : rawUserAgent;
        return $"{unknown} ({trimmed})";
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value).Replace("{", string.Empty).Replace("}", string.Empty);
    }
}
