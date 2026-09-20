using System.Collections.Generic;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Models.MemberAudit;

public static class PendingSelfEnrollmentAuditDelta
{
    public static List<MemberAuditDeltaEntry> Compare(PendingSelfEnrollmentEntity before,
        PendingSelfEnrollmentEntity after, CryptoService crypto)
    {
        List<MemberAuditDeltaEntry> delta = new List<MemberAuditDeltaEntry>();

        Check(delta, nameof(after.Gender), before.Gender.ToString(), after.Gender.ToString());
        Check(delta, nameof(after.MemberCategory), before.MemberCategory?.Name, after.MemberCategory?.Name);
        Check(delta, nameof(after.AcademicDegree), before.AcademicDegree?.ToString(), after.AcademicDegree?.ToString());
        Check(delta, nameof(after.CourseOfStudy), before.CourseOfStudy, after.CourseOfStudy);
        Check(delta, nameof(after.StartOfStudies), before.StartOfStudies.ToString("O"),
            after.StartOfStudies.ToString("O"));
        Check(delta, nameof(after.EndOfStudies), before.EndOfStudies?.ToString("O"), after.EndOfStudies?.ToString("O"));
        Check(delta, nameof(after.BulkMail), before.BulkMail.ToString(), after.BulkMail.ToString());
        Check(delta, nameof(after.ContributionPlan), before.ContributionPlan?.Name, after.ContributionPlan?.Name);

        CheckSensitive(delta, nameof(after.FirstName), before.FirstName, after.FirstName);
        CheckSensitive(delta, nameof(after.MiddleName), before.MiddleName, after.MiddleName);
        CheckSensitive(delta, nameof(after.LastName), before.LastName, after.LastName);
        CheckSensitive(delta, nameof(after.EmailHash), before.EmailHash, after.EmailHash);
        CheckSensitive(delta, nameof(after.PhoneEncrypted), crypto.Decrypt(before.PhoneEncrypted),
            crypto.Decrypt(after.PhoneEncrypted));
        CheckSensitive(delta, nameof(after.StreetEncrypted), crypto.Decrypt(before.StreetEncrypted),
            crypto.Decrypt(after.StreetEncrypted));
        CheckSensitive(delta, nameof(after.BirthdayEncrypted), crypto.Decrypt(before.BirthdayEncrypted),
            crypto.Decrypt(after.BirthdayEncrypted));
        CheckSensitive(delta, nameof(after.IBAN_Encrypted), crypto.Decrypt(before.IBAN_Encrypted),
            crypto.Decrypt(after.IBAN_Encrypted));
        CheckSensitive(delta, nameof(after.PostalCode), before.PostalCode, after.PostalCode);
        CheckSensitive(delta, nameof(after.City), before.City, after.City);
        CheckSensitive(delta, nameof(after.MotivationEncrypted), crypto.Decrypt(before.MotivationEncrypted),
            crypto.Decrypt(after.MotivationEncrypted));

        return delta;
    }

    private static void Check(List<MemberAuditDeltaEntry> delta, string field, string? oldVal, string? newVal)
    {
        if (newVal == null || oldVal == newVal) return;
        delta.Add(new MemberAuditDeltaEntry { Field = field, OldValue = oldVal, NewValue = newVal });
    }

    private static void CheckSensitive(List<MemberAuditDeltaEntry> delta, string field, string? oldVal, string? newVal)
    {
        if (newVal == null || oldVal == newVal) return;
        delta.Add(new MemberAuditDeltaEntry { Field = field, OldValue = "@@", NewValue = "@@" });
    }
}
