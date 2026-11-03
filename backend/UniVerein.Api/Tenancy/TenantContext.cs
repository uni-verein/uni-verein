using System;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Tenancy;

public class TenantContext : ITenantProvider
{
    public Guid TenantId { get; set; } = TenantEntity.DefaultId;
}
