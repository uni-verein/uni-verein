using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("Users")]
public class UserEntity : BaseEntity
{
    [MaxLength(50)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(100)]
    [Column("password_hash")]
    public required string PasswordHash { get; set; } = string.Empty;

    [Column("role")]
    public UserRole Role { get; set; } = UserRole.ADMIN;

    [Column("failed_attempts")]
    public int FailedAttempts { get; set; } = 0;

    [Column("blocking_login_timeout")]
    public DateTimeOffset? BlockingLoginTimeout { get; set; }

    [Column("last_failed_login_attempt")]
    public DateTimeOffset? LastFailedLoginAttempt { get; set; }

    [Column("totp_secret")]
    public string? TotpSecret { get; set; }

    [Column("totp_enabled")]
    public bool TotpEnabled { get; set; }

    [Column("totp_last_used_step")]
    public long? TotpLastUsedStep { get; set; }

    [Column("session_version")]
    public int SessionVersion { get; set; }
}