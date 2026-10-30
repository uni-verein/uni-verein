using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Services;

public class ContributionService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public ContributionService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task GenerateDueContributions()
    {
        DateTimeOffset today = _timeProvider.GetUtcNow();
        if (today.Day != 1)
            return;

        List<MemberEntity> members = _db.Members
            .Include(m => m.ContributionPlan).Where(x => x.ContributionPlan != null && x.DeletedAt == null)
            .AsEnumerable()
            .Where(x => x.ExitDate == null || x.ExitDate > today)
            .ToList();

        SepaExportEntity? export = await _db.SepaExports.Where(x => x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        DateOnly createdAt = DateOnly.FromDateTime((export?.CreatedAt ?? DateTimeOffset.MinValue).DateTime);
        if (!members.Any() || createdAt == DateOnly.FromDateTime(today.DateTime))
            return;

        Guid exportId = Guid.NewGuid();
        int count = 0;
        decimal amount = 0;
        foreach (MemberEntity member in members)
        {
            if (member.ContributionPlan!.Interval == Interval.YEARLY && !(today.Day == 1 && today.Month == 1))
                continue;

            await _db.Contributions.AddAsync(new ContributionEntity
            {
                MemberId = member.Id,
                MemberEntity = member,
                Amount = member.ContributionPlan?.Amount ?? 0,
                DueDate = new DateTime(today.UtcDateTime.Year, today.UtcDateTime.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                ExportId = exportId
            });

            count++;
            amount += member.ContributionPlan?.Amount ?? 0;
        }

        if (count < 1)
            return;

        SepaExportEntity sepaExport = new()
        {
            Id = exportId,
            Name = $"SEPA_Export_{(today.ToString("yyyy-MM-dd-HH-mm"))}.xml",
            Amount = amount,
            Count = count
        };

        await _db.SepaExports.AddAsync(sepaExport);
        await _db.SaveChangesAsync();
        Log.Information("ContributionService: GenerateDueContributions -> {Count} contributions with total {Amount} generated for SEPA export {ExportId}.",
            count, amount, exportId);
    }
}