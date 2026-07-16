using System;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models.MemberAudit;

public class MemberAudit
{
    public Guid MemberId { get; set; }
    public int MemberNumber { get; set; }
    public string MandateId { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public BulkMail BulkMail { get; set; }
    public string MemberCategory { get; set; } = string.Empty;
    public TaskWithinTheClub TaskWithinTheClub { get; set; }
    public AcademicDegree? AcademicDegree { get; set; }
    public string? CourseOfStudy { get; set; }
    public DateTimeOffset StartOfStudies { get; set; }
    public DateTimeOffset? EndOfStudies { get; set; }
    public DateTimeOffset EntryDate { get; set; }
    public DateTimeOffset? ExitDate { get; set; }
    public Guid? ContributionPlanId { get; set; }
    public bool HasIban { get; set; }
    public bool HasBic { get; set; }
    public bool HasSepaConsent { get; set; }
}