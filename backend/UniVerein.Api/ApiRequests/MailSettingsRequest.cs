using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class MailSettingsRequest
{
    [Required]
    [JsonPropertyName("smtpServer")]
    public required string SmtpServer { get; set; }

    [Required] 
    [JsonPropertyName("port")] 
    public int Port { get; set; }

    [Required]
    [JsonPropertyName("imapServer")]
    public required string ImapServer { get; set; }

    [Required]
    [JsonPropertyName("imapPort")]
    public int ImapPort { get; set; }

    [Required]
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [Required(AllowEmptyStrings = true)]
    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [Required]
    [JsonPropertyName("fromMail")]
    public required string FromMail { get; set; }

    [JsonPropertyName("enableSsl")] 
    public bool? EnableSsl { get; set; } = true;
}