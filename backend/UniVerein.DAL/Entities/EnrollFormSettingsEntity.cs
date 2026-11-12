using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("enroll_form_settings")]
public class EnrollFormSettingsEntity : TenantScopedEntity
{
    [Column("self_enrollment_enabled")]
    public bool SelfEnrollmentEnabled { get; set; }

    [Column("legal_text")]
    public string? LegalText { get; set; }

    [Column("embed_allowed_origins")]
    public string? EmbedAllowedOrigins { get; set; }
}
