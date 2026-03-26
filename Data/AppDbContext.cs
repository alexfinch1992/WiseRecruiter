using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using JobPortal.Models;

namespace JobPortal.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Job> Jobs { get; set; } = null!;

        public DbSet<Application> Applications { get; set; } = null!;

        public DbSet<AdminUser> AdminUsers { get; set; } = null!;

        public DbSet<JobStage> JobStages { get; set; } = null!;

        public DbSet<Document> Documents { get; set; } = null!;
    }

    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite("Data Source=jobportal.db");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}