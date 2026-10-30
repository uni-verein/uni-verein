using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog;
using UniVerein.Api.Extensions;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Models.MemberAudit;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Demo;

// Wipes the database and the receipt files and fills them with generated sample data. Only runs in demo mode,
// everything is generated from the current date so charts and due dates look current on every reset.
public class DemoDataService
{
    public const string AdminUsername = "Admin";
    public const string AdminPassword = "admin12345678";
    public const string UserUsername = "User";
    public const string UserPassword = "user12345678";
    public const string FinanceUsername = "Finance";
    public const string FinancePassword = "finance1234";

    public const int MemberCount = 40;
    public const int ReceiptCount = 25;
    public const int IncomeCount = 25;

    public const string ClubName = "Uni-Verein e.V.";

    private const string LogoResourceName = "demo-logo.png";
    private const string MailpitHost = "mailpit";
    private const int MailpitSmtpPort = 1025;

    private static readonly SemaphoreSlim ResetLock = new(1, 1);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static readonly JsonSerializerOptions AuditJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] FemaleNames =
        ["Anna", "Laura", "Marie", "Sophie", "Lena", "Julia", "Hannah", "Lea", "Emma", "Mia", "Clara", "Nele"];

    private static readonly string[] MaleNames =
        ["Lukas", "Jonas", "Felix", "Paul", "Leon", "Max", "Tim", "Ben", "Jan", "Finn", "Noah", "Moritz"];

    private static readonly string[] LastNames =
    [
        "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner", "Becker", "Schulz", "Hoffmann",
        "Koch", "Richter", "Klein", "Wolf", "Schröder", "Neumann", "Braun", "Krüger", "Lange", "Zimmermann"
    ];

    private static readonly string[] Streets =
    [
        "Hauptstraße 1", "Bahnhofstraße 2", "Gartenweg 3", "Schillerstraße 4", "Lindenallee 5", "Uferweg 6",
        "Universitätsstraße 7", "Mozartstraße 8"
    ];

    private static readonly (string PostalCode, string City)[] Cities =
    [
        ("10115", "Berlin"), ("80331", "München"), ("20095", "Hamburg"), ("50667", "Köln"),
        ("60311", "Frankfurt am Main"), ("70173", "Stuttgart"), ("04109", "Leipzig"), ("01067", "Dresden")
    ];

    private static readonly string[] Courses =
    [
        "Informatik", "Maschinenbau", "Wirtschaftswissenschaften", "Psychologie", "Physik", "Medizin", "Jura",
        "Architektur"
    ];

    private static readonly AcademicDegree[] Degrees =
        [AcademicDegree.BSC, AcademicDegree.BA, AcademicDegree.BENG, AcademicDegree.MSC, AcademicDegree.MA];

    private static readonly TaskWithinTheClub[] BoardTasks =
    [
        TaskWithinTheClub.CHAIRMAN, TaskWithinTheClub.SECOND_CHAIRMAN, TaskWithinTheClub.CHIEF_FINANCE_OFFICER,
        TaskWithinTheClub.WEBSITE_MANAGER, TaskWithinTheClub.JUNIOR_BOARD_MEMBER
    ];

    private static readonly string[] ReceiptCategoryNames =
        ["Material", "Reisekosten", "Veranstaltungen", "Bewirtung", "Software & Hosting"];

    private static readonly (string Category, string Vendor, string Description, int Min, int Max)[] ReceiptTemplates =
    [
        ("Material", "Büro-Discount", "Druckerpapier und Toner", 20, 80),
        ("Material", "Baumarkt Müller", "Material für den Infostand", 15, 120),
        ("Reisekosten", "Deutsche Bahn", "Fahrtkosten zur Mitgliederversammlung", 25, 140),
        ("Veranstaltungen", "Getränke Schulz", "Getränke für das Sommerfest", 40, 250),
        ("Veranstaltungen", "Druckerei Weber", "Flyer und Plakate", 30, 180),
        ("Bewirtung", "Pizzeria Roma", "Essen nach der Vorstandssitzung", 35, 120),
        ("Software & Hosting", "Hetzner Online GmbH", "Server und Domain", 5, 60)
    ];

    private static readonly string[] IncomeCategoryNames =
        ["Spenden", "Sponsoring", "Veranstaltungen"];

    private static readonly (string Category, string Payer, string Description, int Min, int Max)[] IncomeTemplates =
    [
        ("Spenden", "Anna Schmidt", "Spende", 20, 100),
        ("Spenden", "Förderverein Alumni e.V.", "Spende für Stipendien", 100, 600),
        ("Sponsoring", "Autohaus Berger GmbH", "Sponsoring Sommerfest", 300, 1500),
        ("Sponsoring", "Elektro Weber", "Sponsoring Vereinsfahrzeug", 200, 800),
        ("Veranstaltungen", "Studierendenwerk", "Einnahmen Infostand", 50, 300),
        ("Veranstaltungen", "Lukas Becker", "Teilnahmegebühr Workshop", 12, 60)
    ];

    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly MemberService _memberService;
    private readonly ReceiptService _receiptService;
    private readonly BookingNumberService _bookingNumberService;
    private readonly IConfiguration _config;
    private readonly TimeProvider _time;

    static DemoDataService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DemoDataService(AppDbContext db, CryptoService crypto, MemberService memberService,
        ReceiptService receiptService, BookingNumberService bookingNumberService, IConfiguration config, TimeProvider time)
    {
        _bookingNumberService = bookingNumberService;
        _db = db;
        _crypto = crypto;
        _memberService = memberService;
        _receiptService = receiptService;
        _config = config;
        _time = time;
    }

    public async Task ResetAsync()
    {
        if (!DemoMode.IsResetEnabled(_config))
            throw new InvalidOperationException("The demo data can only be generated when the demo mode is explicitly enabled.");

        await ResetLock.WaitAsync();
        try
        {
            Log.Information("DemoDataService: Resetting demo data.");
            await WipeDatabaseAsync();
            WipeReceiptFiles();
            _db.ChangeTracker.Clear();

            DefaultDataSeeder.Seed(_db, AdminPassword);

            Random random = new(42);
            DateTimeOffset now = _time.GetUtcNow();

            List<UserEntity> users = await CreateUsersAsync();
            List<ContributionPlanEntity> plans = await CreatePlansAsync();
            await CreateMembersAsync(random, now, plans);
            await CreateContributionsAsync(random, now);
            await CreateReceiptsAsync(random, now, users);
            await CreateIncomesAsync(random, now);
            await _bookingNumberService.AssignMissingAsync();
            await CreateConfigurationAsync();
            await CreateCalendarEventsAsync(now, users[0]);
            await CreateAuditLogsAsync(random, now, users);

            _db.ChangeTracker.Clear();
            Log.Information("DemoDataService: Demo data created.");
        }
        finally
        {
            ResetLock.Release();
        }
    }

    private async Task WipeDatabaseAsync()
    {
        List<string> tables = _db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .OfType<string>()
            .Distinct()
            .ToList();

        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            string list = string.Join(", ", tables.Select(t => $"\"{t}\""));
#pragma warning disable EF1002 // identifiers cannot be SQL parameters, the names come from the EF model
            await _db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {list} RESTART IDENTITY CASCADE");
#pragma warning restore EF1002
        }
        else
        {
            // Foreign keys are switched off per connection, so keep one connection open for all statements.
            await _db.Database.OpenConnectionAsync();
            try
            {
                await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
                foreach (string table in tables)
#pragma warning disable EF1002 // identifiers cannot be SQL parameters, the names come from the EF model
                    await _db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\"");
#pragma warning restore EF1002
                await _db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }
    }

    private void WipeReceiptFiles()
    {
        string path = _receiptService.StoragePath;
        Directory.CreateDirectory(path);

        // Clear the contents only, the directory itself may be a mount point.
        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.Delete(file);
        foreach (string directory in Directory.GetDirectories(path))
            Directory.Delete(directory, recursive: true);
    }

    private async Task<List<UserEntity>> CreateUsersAsync()
    {
        UserEntity admin = await _db.Users.SingleAsync(u => u.Id == Guid.Parse(Program.AdminUserId));
        admin.Email = _crypto.Encrypt("admin@example.org");

        UserEntity user = new()
        {
            Id = Guid.NewGuid(),
            Username = UserUsername,
            Email = _crypto.Encrypt("user@example.org"),
            PasswordHash = CryptoService.HashPassword(UserPassword),
            Role = UserRole.USER
        };
        UserEntity finance = new()
        {
            Id = Guid.NewGuid(),
            Username = FinanceUsername,
            Email = _crypto.Encrypt("finance@example.org"),
            PasswordHash = CryptoService.HashPassword(FinancePassword),
            Role = UserRole.FINANCIAL_MANAGER
        };

        await _db.Users.AddRangeAsync(user, finance);
        await _db.SaveChangesAsync();

        return [admin, user, finance];
    }

    private async Task<List<ContributionPlanEntity>> CreatePlansAsync()
    {
        ContributionPlanEntity defaultPlan = await _db.ContributionPlans.SingleAsync(p =>
            p.Id == Guid.Parse(Program.ContributionPlanDefaultId));
        ContributionPlanEntity monthly = new() { Name = "Monatsbeitrag", Amount = 2.5m, Interval = Interval.MONTHLY };
        ContributionPlanEntity reduced = new() { Name = "Ermäßigt", Amount = 8, Interval = Interval.YEARLY };

        await _db.ContributionPlans.AddRangeAsync(monthly, reduced);
        await _db.SaveChangesAsync();

        return [defaultPlan, monthly, reduced];
    }

    private async Task CreateMembersAsync(Random random, DateTimeOffset now, List<ContributionPlanEntity> plans)
    {
        for (int i = 0; i < MemberCount; i++)
        {
            bool female = random.Next(2) == 0;
            string firstName = female ? FemaleNames[random.Next(FemaleNames.Length)] : MaleNames[random.Next(MaleNames.Length)];
            string lastName = LastNames[random.Next(LastNames.Length)];
            (string postalCode, string city) = Cities[random.Next(Cities.Length)];

            bool board = i < BoardTasks.Length;
            bool alumni = !board && i % 5 == 0;
            bool other = !board && !alumni && i % 11 == 0;
            bool exited = i >= MemberCount - 3;

            DateTimeOffset entry = exited
                ? now.AddDays(-random.Next(450, 1400))
                : now.AddDays(-random.Next(30, 1400));
            DateTimeOffset startOfStudies = entry.AddMonths(-random.Next(0, 18));
            bool hasIban = random.NextDouble() < 0.75;

            MemberCreationResult result = await _memberService.CreateMemberAsync(new MemberCreationInput
            {
                Gender = female ? Gender.FEMALE : Gender.MALE,
                FirstName = firstName,
                LastName = lastName,
                Birthday = now.AddYears(-random.Next(19, 31)).AddDays(-random.Next(0, 365)),
                Street = $"{Streets[random.Next(Streets.Length)]} {random.Next(1, 120)}",
                PostalCode = postalCode,
                City = city,
                CountryCode = "DE",
                Email = $"{Ascii(firstName)}.{Ascii(lastName)}{i}@example.org".ToLowerInvariant(),
                Phone = $"+49 151 {random.Next(1000000, 9999999)}",
                BulkMail = i % 9 == 0 ? BulkMail.NOT_ALLOWED : BulkMail.ALLOWED,
                StartOfStudies = startOfStudies,
                EndOfStudies = alumni ? now.AddMonths(-random.Next(2, 30)) : null,
                AcademicDegree = Degrees[random.Next(Degrees.Length)],
                CourseOfStudy = Courses[random.Next(Courses.Length)],
                TaskWithinTheClub = board ? BoardTasks[i] : TaskWithinTheClub.MEMBER,
                MembershipStatus = i % 13 == 7 ? MembershipStatus.SUPPORTING_MEMBER : MembershipStatus.MEMBER,
                MemberCategoryId = Guid.Parse(board
                    ? Program.MemberCategoriesBoardOfDirectors
                    : alumni
                        ? Program.MemberCategoriesAlumni
                        : other
                            ? Program.MemberCategoriesOther
                            : Program.MemberCategoriesStudent),
                IBAN = hasIban ? BuildIban(i) : string.Empty,
                Bic = hasIban ? "COBADEFFXXX" : string.Empty,
                SepaConsent = hasIban ? entry : null,
                EntryDate = entry,
                ExitDate = exited ? now.AddDays(-random.Next(10, 200)) : null,
                ContributionPlanId = random.NextDouble() < 0.1 ? null : plans[random.Next(plans.Count)].Id
            });

            if (result.Member == null)
                throw new InvalidOperationException($"Demo member {i} could not be created: {result.Status}.");
        }
    }

    private async Task CreateContributionsAsync(Random random, DateTimeOffset now)
    {
        List<MemberEntity> members = await _db.Members.Include(m => m.ContributionPlan)
            .Where(m => m.ContributionPlan != null).ToListAsync();
        DateTime currentMonth = new(now.UtcDateTime.Year, now.UtcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _db.SuppressAutoTimestamps = true;
        try
        {
            for (int monthsBack = 11; monthsBack >= 0; monthsBack--)
            {
                DateTime due = currentMonth.AddMonths(-monthsBack);
                Guid exportId = Guid.NewGuid();
                List<ContributionEntity> contributions = [];

                foreach (MemberEntity member in members)
                {
                    if (member.EntryDate > due || (member.ExitDate != null && member.ExitDate <= due))
                        continue;
                    if (member.ContributionPlan!.Interval == Interval.YEARLY && due.Month != 1)
                        continue;

                    // The newest month is still open, older ones are mostly paid.
                    bool paid = monthsBack > 0 && random.NextDouble() < 0.9;
                    DateTimeOffset? paidAt = paid ? new DateTimeOffset(due.AddDays(random.Next(3, 10)), TimeSpan.Zero) : null;
                    contributions.Add(new ContributionEntity
                    {
                        Id = Guid.NewGuid(),
                        MemberId = member.Id,
                        MemberEntity = member,
                        Amount = member.ContributionPlan.Amount,
                        DueDate = due,
                        Paid = paidAt,
                        PaymentDate = paidAt == null ? null : IncomeService.ToUtcDate(paidAt.Value.UtcDateTime),
                        ExportId = exportId,
                        CreatedAt = due
                    });
                }

                if (contributions.Count == 0)
                    continue;

                await _db.SepaExports.AddAsync(new SepaExportEntity
                {
                    Id = exportId,
                    Name = $"SEPA_Export_{due:yyyy-MM-dd}-03-00.xml",
                    Amount = contributions.Sum(c => c.Amount),
                    Count = contributions.Count,
                    CreatedAt = due
                });
                await _db.Contributions.AddRangeAsync(contributions);
            }

            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.SuppressAutoTimestamps = false;
        }
    }

    private async Task CreateReceiptsAsync(Random random, DateTimeOffset now, List<UserEntity> users)
    {
        List<ReceiptCategoryEntity> categories = ReceiptCategoryNames
            .Select(name => new ReceiptCategoryEntity { Name = name }).ToList();
        await _db.ReceiptCategories.AddRangeAsync(categories);
        await _db.SaveChangesAsync();

        ReceiptPaymentMethod[] methods =
        [
            ReceiptPaymentMethod.CARD, ReceiptPaymentMethod.BANK_TRANSFER, ReceiptPaymentMethod.CASH,
            ReceiptPaymentMethod.PAYPAL
        ];

        _db.SuppressAutoTimestamps = true;
        try
        {
            for (int i = 0; i < ReceiptCount; i++)
            {
                (string category, string vendor, string description, int min, int max) =
                    ReceiptTemplates[random.Next(ReceiptTemplates.Length)];
                DateTimeOffset date = now.AddDays(-random.Next(2, 180));
                bool paid = random.NextDouble() < 0.6;
                DateTimeOffset? paidAt = paid ? Min(date.AddDays(random.Next(2, 14)), now) : null;

                ReceiptEntity receipt = new()
                {
                    Id = Guid.NewGuid(),
                    UserId = users[random.Next(users.Count)].Id,
                    Amount = Math.Round(min + (decimal)random.NextDouble() * (max - min), 2),
                    ReceiptDate = IncomeService.ToUtcDate(date.UtcDateTime),
                    CategoryId = categories.Single(c => c.Name == category).Id,
                    Vendor = vendor,
                    Description = description,
                    PaymentMethod = methods[random.Next(methods.Length)],
                    Paid = paidAt,
                    PaymentDate = paidAt == null ? null : IncomeService.ToUtcDate(paidAt.Value.UtcDateTime),
                    CreatedAt = date
                };

                ReceiptFileEntity file = new()
                {
                    Id = Guid.NewGuid(),
                    ReceiptId = receipt.Id,
                    Receipt = receipt,
                    ContentType = "application/pdf",
                    Position = 0,
                    CreatedAt = date
                };
                receipt.Files.Add(file);

                await File.WriteAllBytesAsync(_receiptService.GetFilePath(file), RenderReceiptPdf(receipt));
                await _db.Receipts.AddAsync(receipt);
            }

            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.SuppressAutoTimestamps = false;
        }
    }

    private async Task CreateIncomesAsync(Random random, DateTimeOffset now)
    {
        List<IncomeCategoryEntity> categories = IncomeCategoryNames
            .Select(name => new IncomeCategoryEntity { Name = name }).ToList();
        await _db.IncomeCategories.AddRangeAsync(categories);
        await _db.SaveChangesAsync();

        _db.SuppressAutoTimestamps = true;
        try
        {
            for (int i = 0; i < IncomeCount; i++)
            {
                (string category, string payer, string description, int min, int max) =
                    IncomeTemplates[random.Next(IncomeTemplates.Length)];
                DateTimeOffset date = now.AddDays(-random.Next(2, 365));
                bool paid = random.NextDouble() < 0.7;

                await _db.Incomes.AddAsync(new IncomeEntity
                {
                    Id = Guid.NewGuid(),
                    CategoryId = categories.Single(c => c.Name == category).Id,
                    Payer = payer,
                    Description = description,
                    Amount = Math.Round(min + (decimal)random.NextDouble() * (max - min), 2),
                    IncomeDate = IncomeService.ToUtcDate(date.UtcDateTime),
                    Paid = paid ? date : null,
                    PaymentDate = paid ? IncomeService.ToUtcDate(date.UtcDateTime) : null,
                    CreatedAt = date
                });
            }

            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.SuppressAutoTimestamps = false;
        }
    }

    private async Task CreateConfigurationAsync()
    {
        await _db.CreditorConfigs.AddAsync(new CreditorConfigEntity
        {
            Name = ClubName,
            Iban_Encrypted = _crypto.Encrypt(BuildIban(9999)),
            Bic_Encrypted = _crypto.Encrypt("COBADEFFXXX"),
            CreditorId = "DE98ZZZ09999999999",
            StreetNameAndNumber = "Musterstraße 1",
            PostCode = "10115",
            CityName = "Berlin",
            CountryCode = "DE"
        });

        await _db.WebPageConfigs.AddAsync(new WebPageConfigEntity
        {
            PageName = ClubName,
            Logo = LoadLogo(),
            SelfEnrollmentEnabled = false
        });

        await _db.LinkSettings.AddAsync(new LinkSettingsEntity
        {
            Name = "Webseite",
            Link = "https://uni-verein.de",
            Icon = "Language"
        });

        await _db.MailSettings.AddAsync(new MailSettingsEntity
        {
            SmtpServer = MailpitHost,
            Port = MailpitSmtpPort,
            Username = "demo",
            Password = _crypto.Encrypt("demo"),
            FromMail = "demo@uni-verein.de",
            FromName = ClubName,
            EnableSsl = false,
            ImapServer = MailpitHost
        });

        await _db.SaveChangesAsync();
    }

    private async Task CreateCalendarEventsAsync(DateTimeOffset now, UserEntity owner)
    {
        (string title, string location, int daysAhead, int hour)[] events =
        [
            ("Vorstandssitzung", "Vereinsheim", 3, 18),
            ("Mitgliederversammlung", "Hörsaal 3", 21, 19),
            ("Sommerfest", "Campus-Wiese", 45, 15),
            ("Stammtisch", "Pizzeria Roma", 10, 20)
        ];

        foreach ((string title, string location, int daysAhead, int hour) in events)
        {
            DateTimeOffset start = new DateTimeOffset(now.UtcDateTime.Date.AddDays(daysAhead).AddHours(hour),
                TimeSpan.Zero);
            await _db.CalendarEvents.AddAsync(new CalendarEventEntity
            {
                Title = title,
                StartAt = start,
                EndAt = start.AddHours(2),
                SeriesEndAt = start.AddHours(2),
                LocationName = location,
                OwnerUserId = owner.Id,
                UpdatedAt = now,
                LastModifiedByUserId = owner.Id
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task CreateAuditLogsAsync(Random random, DateTimeOffset now, List<UserEntity> users)
    {
        UserEntity admin = users[0];
        UserEntity user = users[1];
        UserEntity finance = users[2];
        List<AuditLogEntity> logs = [];

        void AddLog(UserEntity actor, AuditLogActions action, string entity, object data, DateTimeOffset at)
        {
            logs.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = actor.Id,
                User = actor,
                Action = action.ToString(),
                Entity = entity,
                Data = JsonSerializer.Serialize(data, AuditJson),
                CreatedAt = Min(at, now)
            });
        }

        foreach (UserEntity created in new[] { user, finance })
            AddLog(admin, AuditLogActions.CREATE, nameof(UserEntity),
                new { UserId = created.Id, created.Username, created.Role }, now.AddDays(-120));

        ContributionPlanEntity[] plans = await _db.ContributionPlans
            .Where(p => p.Id != Guid.Parse(Program.ContributionPlanDefaultId)).ToArrayAsync();
        foreach (ContributionPlanEntity plan in plans)
            AddLog(admin, AuditLogActions.CREATE, nameof(ContributionPlanEntity),
                new { ContributionPlanId = plan.Id, plan.Name, plan.Amount, plan.Interval }, now.AddDays(-110));

        MailSettingsEntity mail = await _db.MailSettings.SingleAsync();
        AddLog(admin, AuditLogActions.UPDATE, nameof(MailSettingsEntity),
            new
            {
                MailSettingsId = mail.Id,
                mail.SmtpServer,
                mail.Port,
                mail.ImapServer,
                mail.ImapPort,
                mail.Username,
                mail.FromMail,
                mail.FromName
            }, now.AddDays(-100));

        List<MemberEntity> members = await _db.Members.Include(m => m.MemberCategory)
            .OrderBy(m => m.MemberNumber).ToListAsync();
        foreach (MemberEntity member in members)
        {
            // IBAN and BIC are only given together with the SEPA consent.
            bool hasIban = member.SepaConsent.HasValue;
            AddLog(random.Next(3) == 0 ? user : admin, AuditLogActions.CREATE, nameof(MemberEntity), new MemberAudit
            {
                MemberId = member.Id,
                MemberNumber = member.MemberNumber,
                MandateId = member.MandateId,
                Gender = member.Gender,
                MemberCategory = member.MemberCategory?.Name ?? string.Empty,
                MembershipStatus = member.MembershipStatus,
                TaskWithinTheClub = member.TaskWithinTheClub,
                AcademicDegree = member.AcademicDegree,
                CourseOfStudy = member.CourseOfStudy,
                StartOfStudies = member.StartOfStudies,
                EndOfStudies = member.EndOfStudies,
                EntryDate = member.EntryDate,
                ExitDate = member.ExitDate,
                BulkMail = member.BulkMail,
                ContributionPlanId = member.ContributionPlanId,
                HasIban = hasIban,
                HasBic = hasIban,
                HasSepaConsent = hasIban
            }, member.EntryDate);
        }

        foreach (ReceiptEntity receipt in await _db.Receipts.ToListAsync())
            AddLog(users.Single(u => u.Id == receipt.UserId), AuditLogActions.CREATE, nameof(ReceiptEntity),
                new { ReceiptId = receipt.Id, receipt.Amount, receipt.ReceiptDate }, receipt.CreatedAt);

        foreach (IncomeEntity income in await _db.Incomes.ToListAsync())
            AddLog(random.Next(2) == 0 ? finance : admin, AuditLogActions.CREATE, nameof(IncomeEntity),
                new { IncomeId = income.Id, income.CategoryId, income.Amount, Paid = income.Paid.HasValue },
                income.CreatedAt);

        foreach (CalendarEventEntity calendarEvent in await _db.CalendarEvents.ToListAsync())
            AddLog(admin, AuditLogActions.CREATE, nameof(CalendarEventEntity),
                new { CalendarEventId = calendarEvent.Id, calendarEvent.Title, calendarEvent.Visibility },
                now.AddDays(-random.Next(1, 20)));

        _db.SuppressAutoTimestamps = true;
        try
        {
            await _db.AuditLogs.AddRangeAsync(logs);
            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.SuppressAutoTimestamps = false;
        }
    }

    private static string LoadLogo()
    {
        using Stream stream = typeof(DemoDataService).Assembly.GetManifestResourceStream(LogoResourceName)
                              ?? throw new InvalidOperationException($"Resource {LogoResourceName} is missing.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);

        return "data:image/png;base64," + Convert.ToBase64String(buffer.ToArray());
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b)
    {
        return a < b ? a : b;
    }

    private static string Ascii(string value)
    {
        return value.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")
            .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");
    }

    internal static string BuildIban(int accountSeed)
    {
        string bban = "37040044" + (532013000L + accountSeed).ToString("D10", CultureInfo.InvariantCulture);
        BigInteger number = BigInteger.Parse(bban + "131400", CultureInfo.InvariantCulture); // "DE00" as digits
        int check = 98 - (int)(number % 97);

        return $"DE{check:D2}{bban}";
    }

    private static byte[] RenderReceiptPdf(ReceiptEntity receipt)
    {
        string amount = receipt.Amount.ToString("C", German);
        string date = receipt.ReceiptDate.ToString("dd.MM.yyyy", German);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(35);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Text(receipt.Vendor ?? string.Empty).FontSize(18).SemiBold();
                page.Content().PaddingTop(25).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("Beleg").FontSize(22).Bold();
                    column.Item().Text($"Datum: {date}");
                    column.Item().Text(receipt.Description ?? string.Empty);
                    column.Item().PaddingTop(10).Text($"Gesamtbetrag: {amount}").FontSize(14).Bold();
                    column.Item().Text("Mehrwertsteuer enthalten.").FontColor(Colors.Grey.Darken1);
                });
                page.Footer().Text("Beispielbeleg der Demo, keine echte Rechnung.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }
}
