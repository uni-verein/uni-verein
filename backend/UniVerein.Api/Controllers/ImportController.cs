using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Converter;
using UniVerein.Api.Models;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.Api.Validators;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize(Roles = nameof(UserRole.ADMIN))]
[ApiController]
[Route("[controller]")]
public class ImportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;

    private readonly Dictionary<string, AcademicDegree> _academicDegreeMapping = new()
    {
        ["b.a."] = AcademicDegree.BA,
        ["b.sc."] = AcademicDegree.BSC,
        ["b.eng."] = AcademicDegree.BENG,
        ["ll.b."] = AcademicDegree.LLB,
        ["b.ed."] = AcademicDegree.BED,
        ["bba"] = AcademicDegree.BBA,
        ["b.f.a."] = AcademicDegree.BFA,
        ["b.mus."] = AcademicDegree.BMUS,
        ["b.arch."] = AcademicDegree.BARCH,
        ["b.n."] = AcademicDegree.BN,
        ["b.s.w."] = AcademicDegree.BSW,
        ["b.th."] = AcademicDegree.BTH,
        ["b.phil."] = AcademicDegree.BPHIL,
        ["b.c.s."] = AcademicDegree.BCS,
        ["b.ec."] = AcademicDegree.BEC,

        ["m.a."] = AcademicDegree.MA,
        ["m.sc."] = AcademicDegree.MSC,
        ["m.eng."] = AcademicDegree.MENG,
        ["ll.m."] = AcademicDegree.LLM,
        ["m.ed."] = AcademicDegree.MED,
        ["mba"] = AcademicDegree.MBA,
        ["m.f.a."] = AcademicDegree.MFA,
        ["m.mus."] = AcademicDegree.MMUS,
        ["m.arch."] = AcademicDegree.MARCH,
        ["mph"] = AcademicDegree.MPH,
        ["m.s.w."] = AcademicDegree.MSW,
        ["mpa"] = AcademicDegree.MPA,
        ["m.phil."] = AcademicDegree.MPHIL,
        ["m.th."] = AcademicDegree.MTH,
        ["m.c.s."] = AcademicDegree.MCS,
        ["m.ec."] = AcademicDegree.MEC,
        ["m.fin."] = AcademicDegree.MFIN,
        ["m.i.r."] = AcademicDegree.MIR,
        ["m.res."] = AcademicDegree.MRES,

        ["ph.d."] = AcademicDegree.PHD,
        ["m.d."] = AcademicDegree.MD,
        ["ll.d."] = AcademicDegree.LLD,
        ["d.sc."] = AcademicDegree.DSC,
        ["d.eng."] = AcademicDegree.DENG,
        ["ed.d."] = AcademicDegree.EDD,
        ["dba"] = AcademicDegree.DBA,
        ["d.th."] = AcademicDegree.DTH,
        ["d.f.a."] = AcademicDegree.DFA,
        ["d.mus."] = AcademicDegree.DMUS,
        ["dr.p.h."] = AcademicDegree.DRPH,
        ["psy.d."] = AcademicDegree.PSYD,
        ["d.arch."] = AcademicDegree.DARCH,
        ["dnp"] = AcademicDegree.DNP,
        ["d.s.w."] = AcademicDegree.DSW,
        ["j.d."] = AcademicDegree.JD,
        ["dr."] = AcademicDegree.DR,

        ["habil."] = AcademicDegree.HABIL,
        ["dr. habil."] = AcademicDegree.DRHABIL,

        ["dr. h.c."] = AcademicDegree.DRHC,
        ["dr. h.c. mult."] = AcademicDegree.DRHCMULT,

        ["diplom"] = AcademicDegree.DIPLOM,
        ["magister"] = AcademicDegree.MAGISTER,
        ["staatsexamen"] = AcademicDegree.STAATSEXAMEN,
        ["licence"] = AcademicDegree.LICENCE,
        ["maîtrise"] = AcademicDegree.MAITRISE,
        ["ingénieur"] = AcademicDegree.INGENIEUR,
        ["laurea"] = AcademicDegree.LAUREA,
        ["laurea magistrale"] = AcademicDegree.LAUREAMAGISTRALE,
        ["licenciatura"] = AcademicDegree.LICENCIATURA,
        ["título de grado"] = AcademicDegree.TITULODEGRADO,
        ["kandidát věd"] = AcademicDegree.KANDIDATVIED,
        ["docent"] = AcademicDegree.DOCENT,
    };

    public ImportController(AppDbContext db, CryptoService crypto)
    {
        _db = db;
        _crypto = crypto;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<int>> UploadCsvAsync(IFormFile file)
    {
        Log.Information("ImportController: UploadCsvAsync -> Try to upload csv file: {FileName}", file.FileName);

        if (await _db.Members.AnyAsync(x => x.DeletedAt == null))
        {
            Log.Error("ImportController: UploadCsvAsync -> This endpoint can only be used on empty member list");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "This endpoint can only be used on empty member list."));
        }

        if (file.Length == 0)
        {
            Log.Error("ImportController: UploadCsvAsync -> CSV file is not valid because file is empty");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Please upload a valid CSV-File."));
        }

        List<ErrorResultTranslation> importErrors = new();
        CsvConfiguration config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            PrepareHeaderForMatch = args => args.Header.ToLower(),
            MissingFieldFound = null,
            HeaderValidated = null,
            ReadingExceptionOccurred = args => false
        };

        using StreamReader reader = new(file.OpenReadStream());
        using CsvReader csv = new(reader, config);
        csv.Context.TypeConverterCache.AddConverter<DateTimeOffset?>(new GermanDateConverter());
        csv.Context.TypeConverterCache.AddConverter<DateTimeOffset>(new GermanDateConverter());
        await csv.ReadAsync();
        csv.ReadHeader();

        List<(ImportCsvRowData Row, int RowNumber)> records = new();
        while (await csv.ReadAsync())
        {
            int rowNumber = csv.Context?.Parser?.Row ?? 0;
            try
            {
                ImportCsvRowData? record = csv.GetRecord<ImportCsvRowData>();

                if (record is null)
                {
                    importErrors.Add(new()
                        { TranslationKey = "validator.import.invalid", Values = [rowNumber.ToString()] });
                    continue;
                }

                records.Add((record, rowNumber));
            }
            catch
            {
                // Error caught by ReadingExceptionOccurred
            }
        }

        CsvRowValidator validator = new(_academicDegreeMapping);
        foreach ((ImportCsvRowData row, int rowNumber) in records)
        {
            List<ErrorResultTranslation> rowErrors = validator.Validate(row, rowNumber);
            importErrors.AddRange(rowErrors);
        }

        if (importErrors.Any())
        {
            Log.Error(
                $"ImportController: UploadCsvAsync -> {importErrors.Count} Validation errors:\n{string.Join("\n", importErrors)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(errorMessage: "Please upload a valid CSV-File.",
                moreInfo: "The CSV file contains errors. No data was imported.", errorTranslation: importErrors));
        }

        await ImportMembers(records);

        Log.Information($"ImportController: UploadCsvAsync -> {records.Count} Members successfully imported.");
        return Ok(records.Count);
    }

    private async Task ImportMembers(List<(ImportCsvRowData row, int rowNumber)> records)
    {
        List<ContributionPlanEntity> contributionPlanEntities = await _db.ContributionPlans.ToListAsync();
        List<MemberCategoryEntity> memberCategories = await _db.MemberCategories.ToListAsync();
        foreach ((ImportCsvRowData record, _) in records)
        {
            ContributionPlanEntity? contributionPlan =
                await GetContributionPlanEntity(contributionPlanEntities, record);
            MemberCategoryEntity? memberCategory = await GetMemberCategoryEntity(memberCategories, record);

            TaskWithinTheClub taskWithinTheClub =
                Enum.TryParse(record.TaskWithinTheClub, ignoreCase: true, out TaskWithinTheClub parsedTask)
                    ? parsedTask
                    : TaskWithinTheClub.MEMBER;

            MemberEntity member = new()
            {
                MandateId = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{record.MemberNumber}",
                MemberNumber = record.MemberNumber ?? 0,
                Gender = record.Gender?.ToUpper() switch
                {
                    "MALE" => Gender.MALE,
                    "FEMALE" => Gender.FEMALE,
                    _ => Gender.DIVERSE
                },
                FirstName = record.FirstName ?? string.Empty,
                MiddleName = record.MiddleName ?? string.Empty,
                LastName = record.Name ?? string.Empty,
                BirthdayEncrypted = _crypto.Encrypt(record.Birthday ?? DateTimeOffset.MinValue),
                StreetEncrypted = _crypto.Encrypt(record.StreetAndNumber ?? string.Empty),
                PostalCode = record.Zip ?? string.Empty,
                City = record.City ?? string.Empty,
                CountryCode = record.CountryCode ?? string.Empty,
                EmailEncrypted = _crypto.Encrypt(record.EMail ?? string.Empty),
                EmailHash = _crypto.Hash(record.EMail ?? string.Empty),
                PhoneEncrypted = _crypto.Encrypt(record.PhoneNummer ?? string.Empty),
                BulkMail = record.BulkMail!.ToUpper() == "ALLOWED" ? BulkMail.ALLOWED : BulkMail.NOT_ALLOWED,
                StartOfStudies = record.StudyStart ?? DateTimeOffset.MinValue,
                EndOfStudies = record.StudyEnd,
                AcademicDegree = string.IsNullOrWhiteSpace(record.AcademicDegree)
                    ? null
                    : _academicDegreeMapping[record.AcademicDegree.ToLower()],
                CourseOfStudy = record.CourseOfStudy ?? string.Empty,
                TaskWithinTheClub = taskWithinTheClub,
                MemberCategoryId = memberCategory?.Id,
                MemberCategory = memberCategory,
                IBAN_Encrypted = _crypto.Encrypt(record.Iban?.Replace(" ", "") ?? string.Empty),
                IBAN_Hash = _crypto.Hash(record.Iban?.Replace(" ", "") ?? string.Empty),
                Bic_Encrypted = _crypto.Encrypt(record.Bic ?? string.Empty),
                SepaConsent = record.SepaConsentDate,
                EntryDate = record.EntryDate ?? DateTimeOffset.UtcNow,
                ExitDate = null,
                ContributionPlanId = contributionPlan?.Id,
                ContributionPlan = contributionPlan
            };

            await _db.Members.AddAsync(member);
        }

        await _db.SaveChangesAsync();
    }

    [HttpGet("example")]
    public async Task<IActionResult> ExampleCsvAsync()
    {
        Log.Information("ImportController: ExportCsvAsync -> Try to generate member data example");

        ImportCsvRowData data = new ImportCsvRowData()
        {
            MemberNumber = 1,
            Gender = $"{Gender.MALE}",
            Name = "Mustermann",
            FirstName = "Max",
            MiddleName = "",
            Birthday = DateTimeOffset.ParseExact("01.01.2000", "dd.MM.yyyy", null),
            PhoneNummer = "+49 172 12345678",
            EMail = "max.mustermann@gmail.com",
            BulkMail = "ALLOWED",
            StreetAndNumber = "Musterstraße 1",
            Zip = "12345",
            City = "Musterstadt",
            CountryCode = "DE",
            StudyStart = DateTimeOffset.ParseExact("01.10.2015", "dd.MM.yyyy", null),
            StudyEnd = null,
            AcademicDegree = "b.sc.",
            CourseOfStudy = "Informatics",
            TaskWithinTheClub = $"{TaskWithinTheClub.MEMBER}",
            MemberCategory = "STUDENT",
            EntryDate = DateTimeOffset.ParseExact("10.10.2015", "dd.MM.yyyy", null),
            ExitDate = null,
            Iban = "DE89370400440532013000",
            Bic = "INGDDEFFXXX",
            SepaConsentDate = DateTimeOffset.ParseExact("10.10.2015", "dd.MM.yyyy", null),
            ContributionAmount = 12
        };

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            Encoding = Encoding.UTF8
        };

        using MemoryStream memoryStream = new();
        await using StreamWriter streamWriter =
            new(memoryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await using CsvWriter csvWriter = new(streamWriter, config);
        csvWriter.Context.TypeConverterOptionsCache.GetOptions<DateTimeOffset>().Formats = ["dd.MM.yyyy"];
        csvWriter.Context.TypeConverterOptionsCache.GetOptions<DateTimeOffset?>().Formats = ["dd.MM.yyyy"];
        await csvWriter.WriteRecordsAsync([data]);
        await streamWriter.FlushAsync();

        Log.Information("ImportController: ExampleCsvAsync -> Example data successfully exported.");
        return File(memoryStream.ToArray(), "text/csv; charset=utf-8",
            $"example_{DateTime.UtcNow:ddMMyyyy_HHmmss}.csv");
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportCsvAsync()
    {
        Log.Information("ImportController: ExportCsvAsync -> Try to export member data");

        List<MemberEntity> members = await _db.Members
            .Include(x => x.ContributionPlan)
            .Include(x => x.MemberCategory)
            .ToListAsync();
        List<ImportCsvRowData> data = members.Select(x => new ImportCsvRowData()
        {
            MemberNumber = x.MemberNumber,
            Gender = x.Gender.ToString(),
            Name = x.LastName,
            FirstName = x.FirstName,
            MiddleName = x.MiddleName,
            Birthday = _crypto.DecryptDate(x.BirthdayEncrypted),
            PhoneNummer = _crypto.Decrypt(x.PhoneEncrypted),
            EMail = _crypto.Decrypt(x.EmailEncrypted),
            BulkMail = x.BulkMail.ToString(),
            StreetAndNumber = _crypto.Decrypt(x.StreetEncrypted),
            Zip = x.PostalCode,
            City = x.City,
            CountryCode = x.CountryCode,
            StudyStart = x.StartOfStudies,
            StudyEnd = x.EndOfStudies,
            AcademicDegree = x.AcademicDegree?.GetDisplayName(),
            CourseOfStudy = x.CourseOfStudy,
            TaskWithinTheClub = x.TaskWithinTheClub.ToString(),
            MemberCategory = x.MemberCategory?.Name ?? string.Empty,
            EntryDate = x.EntryDate,
            ExitDate = x.ExitDate,
            Iban = _crypto.Decrypt(x.IBAN_Encrypted),
            Bic = _crypto.Decrypt(x.Bic_Encrypted),
            SepaConsentDate = x.SepaConsent,
            ContributionAmount = (int?)x.ContributionPlan?.Amount
        }).ToList();

        Log.Information($"ImportController: ExportCsvAsync -> {data.Count} Member are found. Create csv file.");

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            Encoding = Encoding.UTF8
        };

        using MemoryStream memoryStream = new();
        await using StreamWriter streamWriter =
            new(memoryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await using CsvWriter csvWriter = new(streamWriter, config);
        var converter = new GermanDateConverter();
        csvWriter.Context.TypeConverterCache.AddConverter<DateTimeOffset>(converter);
        csvWriter.Context.TypeConverterCache.AddConverter<DateTimeOffset?>(converter);
        await csvWriter.WriteRecordsAsync(data);
        await streamWriter.FlushAsync();

        Log.Information($"ImportController: ExportCsvAsync -> Member data successfully exported.");
        return File(memoryStream.ToArray(), "text/csv; charset=utf-8", $"export_{DateTime.UtcNow:ddMMyyyy_HHmmss}.csv");
    }

    private async Task<ContributionPlanEntity?> GetContributionPlanEntity(
        List<ContributionPlanEntity> contributionPlans, ImportCsvRowData csvRowData)
    {
        if (csvRowData.ContributionAmount != null)
        {
            decimal contributionAmount = csvRowData.ContributionAmount ?? 0;
            ContributionPlanEntity? contributionPlan =
                contributionPlans.FirstOrDefault(x => x.Amount == contributionAmount);
            if (contributionPlan == null)
            {
                contributionPlan = new()
                {
                    Amount = contributionAmount,
                    Interval = Interval.YEARLY,
                    Name = contributionAmount.ToString(CultureInfo.CreateSpecificCulture("de-DE"))
                };
                await _db.ContributionPlans.AddAsync(contributionPlan);
                await _db.SaveChangesAsync();
                contributionPlans.Add(contributionPlan);
            }

            return contributionPlan;
        }

        return await _db.ContributionPlans.FirstOrDefaultAsync(x =>
            x.Id == Guid.Parse(Program.ContributionPlanDefaultId));
    }

    private async Task<MemberCategoryEntity?> GetMemberCategoryEntity(List<MemberCategoryEntity> memberCategories,
        ImportCsvRowData csvRowData)
    {
        if (!string.IsNullOrWhiteSpace(csvRowData.MemberCategory))
        {
            string memberCategoryName = csvRowData.MemberCategory ?? string.Empty;
            memberCategoryName = memberCategoryName.Trim().Replace(" ", "_").ToUpper();
            MemberCategoryEntity? memberCategory =
                memberCategories.FirstOrDefault(x => x.Category == memberCategoryName);
            if (memberCategory == null)
            {
                memberCategory = new()
                {
                    Category = memberCategoryName,
                    Name = memberCategoryName
                };
                await _db.MemberCategories.AddAsync(memberCategory);
                await _db.SaveChangesAsync();
                memberCategories.Add(memberCategory);
            }

            return memberCategory;
        }

        return await _db.MemberCategories.FirstOrDefaultAsync(x => x.Id == Guid.Parse(Program.MemberCategoriesStudent));
    }
}