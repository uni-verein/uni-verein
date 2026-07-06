using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("AuditLogs")]
public class AuditLogEntity : BaseEntity
{
    [Column("user_id")] 
    public Guid UserId { get; set; }

    [ForeignKey("UserId")] 
    public required UserEntity User { get; set; }

    [Column("action")] 
    public string Action { get; set; } = string.Empty;

    [Column("entity")] 
    public string Entity { get; set; } = string.Empty;

    [Column("data")] 
    public string Data { get; set; } = string.Empty;
}