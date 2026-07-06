using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class MailSettingsResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("smtpServer")] 
    public string SmtpServer { get; set; } = "";

    [JsonPropertyName("port")] 
    public int Port { get; set; }

    [JsonPropertyName("imapServer")] 
    public string ImapServer { get; set; } = "";

    [JsonPropertyName("imapPort")] 
    public int ImapPort { get; set; }

    [JsonPropertyName("username")] 
    public string Username { get; set; } = "";

    [JsonPropertyName("password")] 
    public string Password { get; set; } = "";

    [JsonPropertyName("fromMail")] 
    public string FromMail { get; set; } = "";

    [JsonPropertyName("enableSsl")] 
    public bool EnableSsl { get; set; } = true;
}