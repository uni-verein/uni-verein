using System.Text;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace UniVerein.IntegrationTests.Infrastructure;

public class SqliteBackupService : BackupService
{
    private readonly AppDbContext _db;

    public SqliteBackupService(AppDbContext db, IConfiguration config) : base(config, db)
    {
        _db = db;
    }

    public override async Task<string> CreateBackupAsync()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"backup_{DateTime.Now:yyyyMMddHHmm}.sql");
        var sb = new StringBuilder();

        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine("DELETE FROM Members;");

        try
        {
            var members = await _db.Members.IgnoreQueryFilters().ToListAsync();

            foreach (var m in members)
            {
                sb.AppendLine(
                    $"INSERT INTO Members (id, created_at, deleted_at, mandate_id, member_number, gender, " +
                    $"first_name, middle_name, last_name, birthday, street, postal_code, city, country_code, " +
                    $"emailEncrypted, emailHash, phone, start_of_studies, end_of_studies, academic_degree, " +
                    $"course_of_study, task_within_the_club, member_category, iban_encrypted, iban_hash, " +
                    $"bic_encrypted, sepa_consent, entry_date, exit_date, contribution_plan_id) VALUES (" +
                    $"'{m.Id}', '{m.CreatedAt:O}', {Nullable(m.DeletedAt)}, {Str(m.MandateId)}, {m.MemberNumber}, " +
                    $"'{m.Gender}', {Str(m.FirstName)}, {Str(m.MiddleName)}, {Str(m.LastName)}, " +
                    $"{Str(m.BirthdayEncrypted)}, {Str(m.StreetEncrypted)}, {Str(m.PostalCode)}, {Str(m.City)}, " +
                    $"{Str(m.CountryCode)}, {Str(m.EmailEncrypted)}, {Str(m.EmailHash)}, {Str(m.PhoneEncrypted)}, " +
                    $"'{m.StartOfStudies:O}', {Nullable(m.EndOfStudies)}, {NullableStr(m.AcademicDegree)}, " +
                    $"{Str(m.CourseOfStudy)}, '{m.TaskWithinTheClub}', '{m.MemberCategory}', " +
                    $"{Str(m.IBAN_Encrypted)}, {Str(m.IBAN_Hash)}, {Str(m.Bic_Encrypted)}, " +
                    $"{Nullable(m.SepaConsent)}, '{m.EntryDate:O}', {Nullable(m.ExitDate)}, " +
                    $"{NullableGuid(m.ContributionPlanId)});"
                );
            }

            sb.AppendLine("COMMIT;");

            await File.WriteAllTextAsync(filePath, sb.ToString());
        }
        catch (Exception ex)
        {
            throw new Exception($"Backup failed at member export: {ex.Message}", ex);
        }

        return filePath;
    }

    public override async Task<bool> RestoreBackupAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        string sqlContent = await reader.ReadToEndAsync();

        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Members;");

        var statements = sqlContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase));

        foreach (var statement in statements)
        {
            await _db.Database.ExecuteSqlRawAsync(statement.TrimEnd(';'));
        }

        return true;
    }

    private static string Str(string? value) =>
        value == null ? "NULL" : $"'{value.Replace("'", "''")}'";

    private static string Nullable(DateTimeOffset? value) =>
        value == null ? "NULL" : $"'{value.Value:O}'";

    private static string NullableGuid(Guid? value) =>
        value == null ? "NULL" : $"'{value.Value}'";

    private static string NullableStr<T>(T? value) where T : struct =>
        value == null ? "NULL" : $"'{value.Value}'";
}