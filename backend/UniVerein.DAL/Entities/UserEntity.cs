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
}