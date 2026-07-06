using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class EmailRequest
{
    [JsonPropertyName("subject")] 
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("htmlBody")] 
    public string HtmlBody { get; set; } = string.Empty;

    [JsonPropertyName("attachments")] 
    public List<EmailAttachmentRequest> Attachments { get; set; } = new();
}