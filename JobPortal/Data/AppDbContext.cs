using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using JobPortal.Models;

namespace JobPortal.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
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

        public DbSet<ScorecardTemplate> ScorecardTemplates { get; set; } = null!;

        public DbSet<ScorecardTemplateFacet> ScorecardTemplateFacets { get; set; } = null!;

        public DbSet<Category> Categories { get; set; } = null!;

        public DbSet<Facet> Facets { get; set; } = null!;

        public DbSet<Interview> Interviews { get; set; } = null!;

        public DbSet<InterviewInterviewer> InterviewInterviewers { get; set; } = null!;
        public DbSet<CandidateRecommendation> CandidateRecommendations { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<JobAssignment> JobAssignments { get; set; } = null!;
        public DbSet<EmailTemplate> EmailTemplates { get; set; } = null!;
        public DbSet<JobUser> JobUsers { get; set; } = null!;
        public DbSet<JobAlertSubscription> JobAlertSubscriptions { get; set; } = null!;
        public DbSet<Alert> Alerts { get; set; } = null!;
        public DbSet<HiringRequest> HiringRequests { get; set; } = null!;
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

            modelBuilder.Entity<Application>()
                .HasIndex(a => a.City);

            modelBuilder.Entity<Application>()
                .HasIndex(a => a.JobId);

            modelBuilder.Entity<Application>()
                .HasIndex(a => a.Stage);

            modelBuilder.Entity<Application>()
                .HasIndex(a => a.AppliedDate);

            modelBuilder.Entity<Application>()
                .HasIndex(a => new { a.JobId, a.Stage });

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

            modelBuilder.Entity<Job>()
                .HasOne(j => j.OwnerUser)
                .WithMany()
                .HasForeignKey(j => j.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Facet>()
                .HasOne(f => f.Category)
                .WithMany()
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Facet>()
                .HasIndex(f => f.Name)
                .IsUnique();

            // Seed email templates
            modelBuilder.Entity<EmailTemplate>().HasData(
                new EmailTemplate
                {
                    Id = 1,
                    Name = "Screening Invite",
                    Subject = "You're Invited to a Screening Call — {{FirstName}}",
                    BodyContent = "Hi {{FirstName}},\n\nThank you for applying. We'd like to invite you to a brief screening call to discuss your application.\n\nPlease reply with your availability and we'll get something booked in.\n\nBest regards,\nThe WiseTech Recruiting Team",
                    LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new EmailTemplate
                {
                    Id = 2,
                    Name = "Offer Letter",
                    Subject = "Congratulations {{FirstName}} — Your Offer from WiseTech Global",
                    BodyContent = "Dear {{FirstName}},\n\nWe are delighted to extend this formal offer of employment at WiseTech Global. Please review the attached details and feel free to reach out if you have any questions.\n\nWe look forward to welcoming you to the team!\n\nBest regards,\nThe WiseTech Recruiting Team",
                    LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            // Seed initial categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Technical" },
                new Category { Id = 2, Name = "Soft Skills" },
                new Category { Id = 3, Name = "Leadership" }
            );

            // Interview relationships
            modelBuilder.Entity<Interview>()
                .HasOne(i => i.Candidate)
                .WithMany()
                .HasForeignKey(i => i.CandidateId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Interview>()
                .HasOne(i => i.Application)
                .WithMany()
                .HasForeignKey(i => i.ApplicationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Interview>()
                .HasOne(i => i.JobStage)
                .WithMany()
                .HasForeignKey(i => i.JobStageId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InterviewInterviewer>()
                .HasOne(ii => ii.Interview)
                .WithMany(i => i.InterviewInterviewers)
                .HasForeignKey(ii => ii.InterviewId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InterviewInterviewer>()
                .HasOne(ii => ii.AdminUser)
                .WithMany()
                .HasForeignKey(ii => ii.AdminUserId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Scorecard>()
                .HasOne(s => s.Interview)
                .WithMany(i => i.Scorecards)
                .HasForeignKey(s => s.InterviewId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CandidateRecommendation>()
                .HasOne(r => r.Application)
                .WithMany()
                .HasForeignKey(r => r.ApplicationId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobUser>()
                .HasOne(ju => ju.Job)
                .WithMany(j => j.JobUsers)
                .HasForeignKey(ju => ju.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobUser>()
                .HasOne(ju => ju.User)
                .WithMany()
                .HasForeignKey(ju => ju.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<JobAlertSubscription>()
                .HasOne(jas => jas.Job)
                .WithMany()
                .HasForeignKey(jas => jas.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobAlertSubscription>()
                .HasOne(jas => jas.User)
                .WithMany()
                .HasForeignKey(jas => jas.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<JobAlertSubscription>()
                .HasIndex(jas => new { jas.JobId, jas.UserId })
                .IsUnique();

            modelBuilder.Entity<Alert>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Alert>()
                .HasIndex(a => a.UserId);

            modelBuilder.Entity<Alert>()
                .HasIndex(a => a.CreatedAt);

            modelBuilder.Entity<Alert>()
                .HasIndex(a => new
                {
                    a.UserId,
                    a.Type,
                    a.RelatedEntityId,
                    a.RelatedEntityType
                })
                .IsUnique()
                .HasFilter(null);
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