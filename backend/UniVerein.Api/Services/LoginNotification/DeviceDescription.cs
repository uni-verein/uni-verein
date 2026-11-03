namespace UniVerein.Api.Services.LoginNotification;

public class DeviceDescription
{
    public string? Browser { get; }
    public string? OperatingSystem { get; }

    public DeviceDescription(string? browser, string? operatingSystem)
    {
        Browser = browser;
        OperatingSystem = operatingSystem;
    }
}
