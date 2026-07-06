using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class EmailAttachmentRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
    
    [JsonPropertyName("base64Content")]
    public string Base64Content { get; set; } = string.Empty;
    
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;
    
    [JsonPropertyName("isInline")]
    public bool IsInline { get; set; } = false;
    
    [JsonPropertyName("contentId")]
    public string ContentId { get; set; } = string.Empty;
}