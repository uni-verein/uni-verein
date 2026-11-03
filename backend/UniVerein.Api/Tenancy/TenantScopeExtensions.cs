using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniVerein.DAL.Data;

namespace UniVerein.Api.Tenancy;

public static class TenantScopeExtensions
{
    public static async Task ForEachTenantAsync(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, Task> action,
        CancellationToken cancellationToken = default)
    {
        List<Guid> tenantIds;
        using (IServiceScope scope = scopeFactory.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantIds = await db.Tenants
                .Where(t => t.DeletedAt == null)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (Guid tenantId in tenantIds)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = tenantId;
            await action(scope.ServiceProvider);
        }
    }

    public static IServiceScope CreateTenantScope(this IServiceScopeFactory scopeFactory, Guid tenantId)
    {
        IServiceScope scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = tenantId;
        return scope;
    }
}
