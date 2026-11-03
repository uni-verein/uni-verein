using System;
using System.Linq;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Serilog;

namespace UniVerein.Api.Services;

public static class DefaultDataSeeder
{
    public static void EnsureDefaultTenant(AppDbContext db)
    {
        if (db.Tenants.Any(t => t.Id == TenantEntity.DefaultId))
            return;

        db.Tenants.Add(new TenantEntity
        {
            Id = TenantEntity.DefaultId,
            Name = "Default",
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        Log.Information("Seeding: Default tenant {TenantId} created.", TenantEntity.DefaultId);
    }

    public static void Seed(AppDbContext db, string? adminPassword = null)
    {
        EnsureDefaultTenant(db);

        if (adminPassword != null && !db.Users.IgnoreQueryFilters(QueryFilter.SOFT_DELETE).Any())
        {
            Guid id = Guid.Parse(Program.AdminUserId);
            db.Users.Add(new UserEntity
            {
                Id = id,
                PasswordHash = CryptoService.HashPassword(adminPassword),
                Username = "Admin",
                Role = UserRole.ADMIN
            });
            db.SaveChanges();
            Log.Information("Seeding: Admin user {UserId} created.", id);
        }

        bool contributionPlanExists = db.ContributionPlans.Any();
        if (!contributionPlanExists)
        {
            db.ContributionPlans.Add(new ContributionPlanEntity()
            {
                Id = Guid.Parse(Program.ContributionPlanDefaultId),
                Name = "Default",
                Amount = 12,
                Interval = Interval.YEARLY,
            });
            db.SaveChanges();
            Log.Information("Seeding: Default contribution plan created.");
        }

        foreach ((string id, string name) category in Program.DefaultMemberCategories)
        {
            if (!db.MemberCategories.IgnoreQueryFilters(QueryFilter.SOFT_DELETE).Any(x => x.Id == Guid.Parse(category.id)))
            {
                db.MemberCategories.Add(new MemberCategoryEntity()
                {
                    Id = Guid.Parse(category.id),
                    Category = category.name,
                    Name = category.name
                });
                db.SaveChanges();
                Log.Information("Seeding: Default member category {CategoryName} created.", category.name);
            }
        }
    }
}
