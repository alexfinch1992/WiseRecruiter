using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Unit
{
    /// <summary>
    /// TDD suite for the Candidate Unification refactor.
    /// Tests here drive the requirement that GET /Admin/GetCandidatesJson returns
    /// one row per unique email address, collapsing multiple applications.
    /// </summary>
    public class CandidateUnificationTests
    {
        // ── Infrastructure ────────────────────────────────────────────────────

        private static AppDbContext CreateInMemoryContext() =>
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase("unification_db_" + Guid.NewGuid())
                    .Options);

        private static AdminController CreateController(AppDbContext context)
            => AdminControllerFactory.Create(context);

        /// <summary>
        /// Seeds a minimal Job + Candidate + Application triple for a given email.
        /// Returns the created Application.
        /// </summary>
        private static async Task<Application> SeedApplicationAsync(
            AppDbContext context,
            string name,
            string email,
            string jobTitle = "Software Engineer",
            ApplicationStage stage = ApplicationStage.Applied)
        {
            var job = new Job { Title = jobTitle, Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var candidate = new Candidate
            {
                FirstName = name.Split(' ')[0],
                LastName  = name.Contains(' ') ? name.Split(' ')[1] : "Test",
                Email     = email,
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name        = name,
                Email       = email,
                City        = "Sydney",
                JobId       = job.Id,
                CandidateId = candidate.Id,
                Stage       = stage,
                AppliedDate = DateTime.UtcNow,
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        /// <summary>
        /// RED → GREEN: Two applications for "User A" (same email) plus one for
        /// "User B" must produce exactly 2 unified rows, not 3 raw rows.
        /// </summary>
        [Fact]
        public async Task GetUnifiedCandidates_ShouldGroupByEmail()
        {
            using var context = CreateInMemoryContext();

            // Seed: 2 applications for Alex (same email), 1 for Bob
            await SeedApplicationAsync(context, "Alex Finch", "alex@example.com", "Backend Engineer");
            await SeedApplicationAsync(context, "Alex Finch", "alex@example.com", "Frontend Engineer");
            await SeedApplicationAsync(context, "Bob Smith",  "bob@example.com",  "DevOps Engineer");

            var controller = CreateController(context);

            // Act
            var actionResult = await controller.GetCandidatesJson(search: null);
            var json    = actionResult.Should().BeOfType<JsonResult>().Subject;
            var results = json.Value.Should()
                .BeAssignableTo<IEnumerable<UnifiedCandidateDto>>().Subject
                .ToList();

            // Assert: one row per unique email
            results.Should().HaveCount(2,
                because: "two distinct emails were seeded");

            // Assert: Alex's unified row reports 2 active applications
            var alexRow = results.FirstOrDefault(r => r.Email == "alex@example.com");
            alexRow.Should().NotBeNull(because: "Alex was seeded");
            alexRow!.ActiveApplicationCount.Should().Be(2,
                because: "Alex has two non-terminal applications");
            alexRow.ApplicationIds.Should().HaveCount(2,
                because: "both application IDs must be accessible");
            alexRow.Name.Should().Be("Alex Finch");

            // Assert: Bob's row is present and untouched
            var bobRow = results.FirstOrDefault(r => r.Email == "bob@example.com");
            bobRow.Should().NotBeNull();
            bobRow!.ActiveApplicationCount.Should().Be(1);
        }

        /// <summary>
        /// A rejected application must NOT count toward ActiveApplicationCount,
        /// but its ID must still appear in ApplicationIds.
        /// </summary>
        [Fact]
        public async Task GetUnifiedCandidates_RejectedApplication_NotCountedAsActive()
        {
            using var context = CreateInMemoryContext();

            await SeedApplicationAsync(context, "Alex Finch", "alex@example.com", "Role A", ApplicationStage.Applied);
            await SeedApplicationAsync(context, "Alex Finch", "alex@example.com", "Role B", ApplicationStage.Rejected);

            var controller = CreateController(context);

            var actionResult = await controller.GetCandidatesJson(search: null);
            var json    = actionResult.Should().BeOfType<JsonResult>().Subject;
            var results = json.Value.Should()
                .BeAssignableTo<IEnumerable<UnifiedCandidateDto>>().Subject
                .ToList();

            results.Should().HaveCount(1);
            var row = results.Single();
            row.ApplicationIds.Should().HaveCount(2, "both IDs are accessible");
            row.ActiveApplicationCount.Should().Be(1, "only the Applied one is active");
        }
    }
}
