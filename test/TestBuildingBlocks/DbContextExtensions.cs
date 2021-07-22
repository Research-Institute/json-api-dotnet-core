using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace TestBuildingBlocks
{
    public static class DbContextExtensions
    {
        public static void AddInRange(this DbContext dbContext, params object[] entities)
        {
            dbContext.AddRange(entities);
        }

        public static async Task ClearTableAsync<TEntity>(this DbContext dbContext)
            where TEntity : class
        {
            await ClearTablesAsync(dbContext, typeof(TEntity));
        }

        public static async Task ClearTablesAsync<TEntity1, TEntity2>(this DbContext dbContext)
            where TEntity1 : class
            where TEntity2 : class
        {
            await ClearTablesAsync(dbContext, typeof(TEntity1), typeof(TEntity2));
        }

        public static async Task ClearTablesAsync<TEntity1, TEntity2, TEntity3>(this DbContext dbContext)
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class
        {
            await ClearTablesAsync(dbContext, typeof(TEntity1), typeof(TEntity2), typeof(TEntity3));
        }

        public static async Task ClearTablesAsync<TEntity1, TEntity2, TEntity3, TEntity4>(this DbContext dbContext)
            where TEntity1 : class
            where TEntity2 : class
            where TEntity3 : class
            where TEntity4 : class
        {
            await ClearTablesAsync(dbContext, typeof(TEntity1), typeof(TEntity2), typeof(TEntity3), typeof(TEntity4));
        }

        private static async Task ClearTablesAsync(this DbContext dbContext, params Type[] models)
        {
            foreach (Type model in models)
            {
                IEntityType entityType = dbContext.Model.FindEntityType(model);

                if (entityType == null)
                {
                    throw new InvalidOperationException($"Table for '{model.Name}' not found.");
                }

                string tableName = entityType.GetTableName();

                // PERF: We first try to clear the table, which is fast and usually succeeds, unless foreign key constraints are violated.
                // In that case, we recursively delete all related data, which is slow.
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync("delete from \"" + tableName + "\"");
                }
                catch (PostgresException)
                {
                    await dbContext.Database.ExecuteSqlRawAsync("truncate table \"" + tableName + "\" cascade");
                }
            }
        }
    }
}
