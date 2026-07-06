namespace UniVerein.Api.Data.Mail;

public class PreparedAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string ContentId { get; set; } = string.Empty;
    public bool IsInline { get; set; }
    public byte[] Bytes { get; set; } = [];
}