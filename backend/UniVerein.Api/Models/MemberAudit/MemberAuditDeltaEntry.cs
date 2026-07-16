namespace UniVerein.Api.Models.MemberAudit;

public class MemberAuditDeltaEntry
{
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}