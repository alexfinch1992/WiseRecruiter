using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class GlobalSearchServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("global_search_" + System.Guid.NewGuid())
                .Options);

        private async Task<(AppDbContext ctx, Candidate candidate, Job job, Application application)>
            SeedCandidateAsync(AppDbContext ctx, string firstName, string lastName, string jobTitle = "Developer")
        {
            var job = new Job { Title = jobTitle };
            ctx.Jobs.Add(job);
            var candidate = new Candidate { FirstName = firstName, LastName = lastName, Email = $"{firstName.ToLower()}@test.com" };
            ctx.Candidates.Add(candidate);
            await ctx.SaveChangesAsync();

            var application = new Application
            {
                Name = $"{firstName} {lastName}",
                Email = $"{firstName.ToLower()}@test.com",
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id
            };
            ctx.Applications.Add(application);
            await ctx.SaveChangesAsync();
            return (ctx, candidate, job, application);
        }

        // ── Candidate search ──────────────────────────────────────────────────

        [Fact]
        public async Task SearchAsync_ReturnsCandidatesMatchingByFirstName()
        {
            await using var ctx = CreateInMemoryContext();
            await SeedCandidateAsync(ctx, "Alice", "Smith");

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("alice");

            results.Should().ContainSingle(r => r.Type == "Candidate" && r.DisplayText == "Alice Smith");
        }

        [Fact]
        public async Task SearchAsync_ReturnsCandidatesMatchingByLastName()
        {
            await using var ctx = CreateInMemoryContext();
            await SeedCandidateAsync(ctx, "Bob", "Johnson");

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("johnson");

            results.Should().ContainSingle(r => r.Type == "Candidate" && r.DisplayText == "Bob Johnson");
        }

        [Fact]
        public async Task SearchAsync_CandidateResult_IdIsApplicationId()
        {
            await using var ctx = CreateInMemoryContext();
            var (_, _, _, application) = await SeedCandidateAsync(ctx, "Carol", "White");

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("carol");

            results.Single(r => r.Type == "Candidate").Id.Should().Be(application.Id);
        }

        [Fact]
        public async Task SearchAsync_DoesNotReturnDuplicateCandidates_WhenCandidateHasMultipleApplications()
        {
            await using var ctx = CreateInMemoryContext();
            var (_, candidate, _, _) = await SeedCandidateAsync(ctx, "Dave", "Jones");

            // Add a second application for the same candidate to a different job
            var job2 = new Job { Title = "Second Job" };
            ctx.Jobs.Add(job2);
            await ctx.SaveChangesAsync();
            ctx.Applications.Add(new Application
            {
                Name = "Dave Jones",
                Email = "dave@test.com",
                City = "Sydney",
                JobId = job2.Id,
                CandidateId = candidate.Id
            });
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("dave");

            results.Count(r => r.Type == "Candidate").Should().Be(1);
        }

        // ── Job search ────────────────────────────────────────────────────────

        [Fact]
        public async Task SearchAsync_ReturnsJobsMatchingQuery()
        {
            await using var ctx = CreateInMemoryContext();
            ctx.Jobs.Add(new Job { Title = "Software Engineer" });
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("software");

            results.Should().ContainSingle(r => r.Type == "Job" && r.DisplayText == "Software Engineer");
        }

        [Fact]
        public async Task SearchAsync_JobResult_IdIsJobId()
        {
            await using var ctx = CreateInMemoryContext();
            var job = new Job { Title = "Product Manager" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("product");

            results.Single(r => r.Type == "Job").Id.Should().Be(job.Id);
        }

        // ── Case insensitivity ────────────────────────────────────────────────

        [Fact]
        public async Task SearchAsync_IsCaseInsensitive_ForCandidates()
        {
            await using var ctx = CreateInMemoryContext();
            await SeedCandidateAsync(ctx, "Eve", "Brown");

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("EVE");

            results.Should().ContainSingle(r => r.Type == "Candidate");
        }

        [Fact]
        public async Task SearchAsync_IsCaseInsensitive_ForJobs()
        {
            await using var ctx = CreateInMemoryContext();
            ctx.Jobs.Add(new Job { Title = "Senior Developer" });
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("SENIOR");

            results.Should().ContainSingle(r => r.Type == "Job");
        }

        // ── Limits ────────────────────────────────────────────────────────────

        [Fact]
        public async Task SearchAsync_LimitsCandidateResultsToFive()
        {
            await using var ctx = CreateInMemoryContext();
            for (int i = 1; i <= 8; i++)
            {
                await SeedCandidateAsync(ctx, $"TestPerson{i}", "Match");
            }

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("testperson");

            results.Count(r => r.Type == "Candidate").Should().BeLessThanOrEqualTo(5);
        }

        [Fact]
        public async Task SearchAsync_LimitsJobResultsToFive()
        {
            await using var ctx = CreateInMemoryContext();
            for (int i = 1; i <= 8; i++)
            {
                ctx.Jobs.Add(new Job { Title = $"MatchJob Role {i}" });
            }
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("matchjob");

            results.Count(r => r.Type == "Job").Should().BeLessThanOrEqualTo(5);
        }

        // ── Empty / no match ─────────────────────────────────────────────────

        [Fact]
        public async Task SearchAsync_ReturnsEmpty_WhenQueryIsEmpty()
        {
            await using var ctx = CreateInMemoryContext();
            ctx.Jobs.Add(new Job { Title = "Some Job" });
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("");

            results.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchAsync_ReturnsEmpty_WhenNoMatches()
        {
            await using var ctx = CreateInMemoryContext();
            ctx.Jobs.Add(new Job { Title = "Accountant" });
            await ctx.SaveChangesAsync();

            var sut = new GlobalSearchService(ctx);
            var results = await sut.SearchAsync("zzznomatch");

            results.Should().BeEmpty();
        }
    }
}
