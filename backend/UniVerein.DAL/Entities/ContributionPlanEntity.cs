using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("ContributionPlans")]
public class ContributionPlanEntity : BaseEntity
{
    [Column("name")] 
    public string Name { get; set; } = "";

    [Column("amount")] 
    public decimal Amount { get; set; }

    [Column("interval")] 
    public Interval Interval { get; set; } = Interval.YEARLY;
}