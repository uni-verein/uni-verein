using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class MailSendRequest
{
    [JsonPropertyName("categoryId")] 
    public Guid? CategoryId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = string.Empty;

    [JsonPropertyName("emailData")]
    public EmailRequest EmailData { get; set; } = new();

    [JsonPropertyName("selectedEmails")] 
    public List<string>? SelectedEmails { get; set; }
}