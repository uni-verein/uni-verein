using System;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services.LoginNotification;

public class LoginNotificationJob
{
    public Guid UserId { get; }
    public Guid TenantId { get; }
    public string IpAddress { get; }
    public string? UserAgent { get; }
    public DateTimeOffset OccurredAt { get; }
    public bool TwoFactorPending { get; }

    public LoginNotificationJob(Guid userId, string ipAddress, string? userAgent, DateTimeOffset occurredAt,
        bool twoFactorPending, Guid? tenantId = null)
    {
        UserId = userId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        OccurredAt = occurredAt;
        TwoFactorPending = twoFactorPending;
        TenantId = tenantId ?? TenantEntity.DefaultId;
    }
}
