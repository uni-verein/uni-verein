using System;
using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Query;

public class IncomeQuery : QueryBase
{
    public IncomeKind? Kind { get; set; }
    public Guid? CategoryId { get; set; }
    public MembershipStatus? MembershipStatus { get; set; }
    public int? MemberNumber { get; set; }
    public string? Name { get; set; }
    public bool? Unpaid { get; set; }
    public bool? Deleted { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
