using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("web_page_config")]
public class WebPageConfigEntity : BaseEntity
{
    [Column("page_name")]
    public string PageName { get; set; } = "";

    [Column("logo")]
    public string Logo { get; set; } = "";

    [Column("self_enrollment_enabled")]
    public bool SelfEnrollmentEnabled { get; set; }
}