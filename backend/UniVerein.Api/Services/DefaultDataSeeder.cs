using System;
using System.Linq;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Services;

public static class DefaultDataSeeder
{
    public const string DefaultAdminPassword = "admin123";

    public static void Seed(AppDbContext db, string adminPassword = DefaultAdminPassword)
    {
        Guid id = Guid.Parse(Program.AdminUserId);
        if (!db.Users.IgnoreQueryFilters().Any())
        {
            db.Users.Add(new UserEntity
            {
                Id = id,
                PasswordHash = CryptoService.HashPassword(adminPassword),
                Username = "Admin",
                Role = UserRole.ADMIN
            });
            db.SaveChanges();
            Log.Information("Seeding: Default admin user {UserId} created. Change its password after the first login.", id);
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
            if (!db.MemberCategories.IgnoreQueryFilters().Any(x => x.Id == Guid.Parse(category.id)))
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
