using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("CreditorConfigs")]
public class CreditorConfigEntity : BaseEntity
{
    [Column("name")] 
    public required string Name { get; set; }

    [Column("iban_encrypted")] 
    public required string Iban_Encrypted { get; set; }

    [Column("bic_encrypted")] 
    public required string Bic_Encrypted { get; set; }

    [Column("creditor_id")] 
    public required string CreditorId { get; set; }

    [Column("street_name_and_number")] 
    public string? StreetNameAndNumber { get; set; }

    [Column("post_code")] 
    public string? PostCode { get; set; }

    [Column("city_name")] 
    public required string CityName { get; set; }

    [Column("country_code")] 
    public required string CountryCode { get; set; }
}