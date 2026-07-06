using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("Contributions")]
public class ContributionEntity : BaseEntity
{
    [Column("member_id")] 
    public Guid MemberId { get; set; }

    [ForeignKey("MemberId")] 
    public required MemberEntity MemberEntity { get; set; }

    [Column("amount")] 
    public decimal Amount { get; set; }

    [Column("due_date")]
    public DateTime DueDate { get; set; }

    [Column("paid")] 
    public DateTimeOffset? Paid { get; set; }

    [Column("exportId")] 
    public Guid ExportId { get; set; }
}