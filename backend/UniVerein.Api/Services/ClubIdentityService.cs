using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.Models;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services;

public class ClubIdentityService
{
    private readonly AppDbContext _db;

    public ClubIdentityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ClubIdentity> GetAsync()
    {
        CreditorConfigEntity? creditor = await _db.CreditorConfigs.Where(x => x.DeletedAt == null).FirstOrDefaultAsync();
        if (creditor != null)
            return new ClubIdentity(creditor.Name, BuildAddress(creditor.StreetNameAndNumber, creditor.PostCode, creditor.CityName), creditor.TaxNumber);

        WebPageConfigEntity? webPage = await _db.WebPageConfigs.Where(x => x.DeletedAt == null).FirstOrDefaultAsync();
        return new ClubIdentity(string.IsNullOrWhiteSpace(webPage?.PageName) ? "Verein" : webPage.PageName, null, null);
    }

    public static string? BuildAddress(string? street, string? postalCode, string? city)
    {
        string place = string.Join(" ", new[] { postalCode, city }.Where(x => !string.IsNullOrWhiteSpace(x)));
        string address = string.Join(", ", new[] { street, place }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return address.Length == 0 ? null : address;
    }
}
