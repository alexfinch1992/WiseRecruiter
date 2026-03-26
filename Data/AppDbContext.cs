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

        public DbSet<Candidate> Candidates { get; set; } = null!;

        public DbSet<AdminUser> AdminUsers { get; set; } = null!;

        public DbSet<JobStage> JobStages { get; set; } = null!;

        public DbSet<Document> Documents { get; set; } = null!;

        public DbSet<Scorecard> Scorecards { get; set; } = null!;

        public DbSet<ScorecardResponse> ScorecardResponses { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Scorecard>()
                .HasMany(s => s.Responses)
                .WithOne(r => r.Scorecard)
                .HasForeignKey(r => r.ScorecardId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Scorecard>()
                .HasOne(s => s.Candidate)
                .WithMany(c => c.Scorecards)
                .HasForeignKey(s => s.CandidateId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Application>()
                .HasOne(a => a.Candidate)
                .WithMany(c => c.Applications)
                .HasForeignKey(a => a.CandidateId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        }
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