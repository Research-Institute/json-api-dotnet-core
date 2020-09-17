using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ModelStateValidation
{
    public sealed class ModelStateDbContext : DbContext
    {
        public DbSet<Enterprise> Enterprises { get; set; }
        public DbSet<EnterprisePartner> EnterprisePartners { get; set; }
        public DbSet<PostalAddress> PostalAddresses { get; set; }

        public ModelStateDbContext(DbContextOptions<ModelStateDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Enterprise>()
                .HasOne(enterprise => enterprise.Self)
                .WithOne();

            modelBuilder.Entity<Enterprise>()
                .HasOne(enterprise => enterprise.AlsoSelf)
                .WithOne();
        }
    }
}
