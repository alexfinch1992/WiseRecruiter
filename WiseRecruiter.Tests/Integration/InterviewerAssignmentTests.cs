using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class InterviewerAssignmentTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "interviewer_assignment_db_" + Guid.NewGuid())
                .Options;
            return new AppDbContext(options);
        }

        private static InterviewCommandService CreateService(AppDbContext context) =>
            new InterviewCommandService(
                context,
                new InterviewService(context),
                new RecommendationService(context, new StageOrderService()));

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "Candidate",
                Email = $"test_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Engineer", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Technical Interview", Order = 2 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Test Candidate",
                Email = candidate.Email,
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return (candidate, application, stage);
        }

        [Fact]
        public async Task CreateInterview_WithTwoInterviewers_CreatesInterviewInterviewerRows()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var (candidate, application, stage) = await SeedAsync(context);

            var admin1 = new AdminUser { Username = "alice", PasswordHash = "hash1" };
            var admin2 = new AdminUser { Username = "bob", PasswordHash = "hash2" };
            context.AdminUsers.AddRange(admin1, admin2);
            await context.SaveChangesAsync();

            var svc = CreateService(context);
            var scheduledAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
            var interviewerIds = new List<int> { admin1.Id, admin2.Id };

            // Act
            var result = await svc.CreateAsync(
                candidate.Id, application.Id, $"stage:{stage.Id}", scheduledAt,
                interviewerIds, proceedWithoutApproval: false, bypassReason: null, userId: "");

            // Assert - succeeded
            result.Success.Should().BeTrue();

            // Assert - exactly 2 InterviewInterviewer rows
            var links = await context.InterviewInterviewers.ToListAsync();
            links.Should().HaveCount(2);

            // Assert - correct AdminUserIds
            var linkedAdminIds = links.Select(l => l.AdminUserId).OrderBy(x => x).ToList();
            linkedAdminIds.Should().BeEquivalentTo(new[] { admin1.Id, admin2.Id });
        }
    }
}
