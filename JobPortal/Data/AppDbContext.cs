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

        public DbSet<ScorecardFacet> ScorecardFacets { get; set; } = null!;  // Legacy — table preserved

        public DbSet<ScorecardTemplate> ScorecardTemplates { get; set; } = null!;

        public DbSet<ScorecardTemplateFacet> ScorecardTemplateFacets { get; set; } = null!;

        public DbSet<Category> Categories { get; set; } = null!;

        public DbSet<Facet> Facets { get; set; } = null!;

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

            // Legacy entity — unique name index kept for DB integrity
            modelBuilder.Entity<ScorecardFacet>()
                .HasIndex(f => f.Name)
                .IsUnique();

            // Legacy: ScorecardFacet → Category FK (read-only; no longer managed via services)
            modelBuilder.Entity<ScorecardFacet>()
                .HasOne(f => f.Category)
                .WithMany()
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ScorecardFacetId is kept as a plain column (no FK managed by EF)
            modelBuilder.Entity<ScorecardTemplateFacet>()
                .Property(tf => tf.ScorecardFacetId)
                .HasColumnName("ScorecardFacetId");

            modelBuilder.Entity<ScorecardTemplate>()
                .HasMany(t => t.TemplateFacets)
                .WithOne(tf => tf.ScorecardTemplate)
                .HasForeignKey(tf => tf.ScorecardTemplateId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScorecardTemplateFacet>()
                .HasOne(tf => tf.Facet)
                .WithMany()
                .HasForeignKey(tf => tf.FacetId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ScorecardTemplateFacet>()
                .HasIndex(tf => new { tf.ScorecardTemplateId, tf.FacetId })
                .IsUnique();

            modelBuilder.Entity<Job>()
                .HasOne(j => j.ScorecardTemplate)
                .WithMany(t => t.Jobs)
                .HasForeignKey(j => j.ScorecardTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Facet>()
                .HasOne(f => f.Category)
                .WithMany()
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Facet>()
                .HasIndex(f => f.Name)
                .IsUnique();

            // Seed initial categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Technical" },
                new Category { Id = 2, Name = "Soft Skills" },
                new Category { Id = 3, Name = "Leadership" }
            );
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