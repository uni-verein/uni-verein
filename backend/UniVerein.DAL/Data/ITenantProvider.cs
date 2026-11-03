using System;

namespace UniVerein.DAL.Data;

public interface ITenantProvider
{
    Guid TenantId { get; }
}
