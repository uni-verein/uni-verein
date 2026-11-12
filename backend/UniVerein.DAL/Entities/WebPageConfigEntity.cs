using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("web_page_config")]
public class WebPageConfigEntity : TenantScopedEntity
{
    [Column("page_name")]
    public string PageName { get; set; } = "";

    [Column("logo")]
    public string Logo { get; set; } = "";

    [Column("public_base_url")]
    public string? PublicBaseUrl { get; set; }
}