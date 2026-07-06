using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

public abstract class BaseEntity
{
    [Key] [Column("id")]
    public Guid Id { get; set; }

    [Column("created_at")] 
    public DateTimeOffset CreatedAt { get; set; }

    [Column("deleted_at")] 
    public DateTimeOffset? DeletedAt { get; set; }
}