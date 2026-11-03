using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.DAL.Data;

public static class QueryFilterExtensions
{
    public static IQueryable<T> IgnoreQueryFilters<T>(this IQueryable<T> query, QueryFilter filter) where T : class
    {
        return query.IgnoreQueryFilters([filter.ToString()]);
    }
}
