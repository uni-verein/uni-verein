using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

public abstract class TenantScopedEntity : BaseEntity
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }
}
