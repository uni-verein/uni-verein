using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("Tenants")]
public class TenantEntity : BaseEntity
{
    public static readonly Guid DefaultId = new("0f1c2d3e-4a5b-4c6d-8e7f-9a0b1c2d3e4f");

    [Column("name")]
    public required string Name { get; set; }
}
