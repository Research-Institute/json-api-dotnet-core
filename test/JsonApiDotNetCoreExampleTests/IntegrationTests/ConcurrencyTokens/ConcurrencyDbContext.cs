using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    public sealed class ConcurrencyDbContext : DbContext
    {
        public DbSet<Disk> Disks { get; set; }
        public DbSet<Partition> Partitions { get; set; }

        public ConcurrencyDbContext(DbContextOptions<ConcurrencyDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // https://www.npgsql.org/efcore/modeling/concurrency.html

            builder.Entity<Disk>()
                .UseXminAsConcurrencyToken();

            builder.Entity<Partition>()
                .UseXminAsConcurrencyToken();
        }
    }
}
