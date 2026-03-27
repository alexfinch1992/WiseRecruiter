using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class ScorecardArchivingTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("scorecard_archiving_" + Guid.NewGuid())
                .Options);

        private IScorecardService CreateScorecardService(AppDbContext context) =>
            new ScorecardService(context, new ScorecardTemplateService(context));

        private IScorecardAnalyticsService CreateAnalyticsService(AppDbContext context) =>
            new ScorecardAnalyticsService(context);

        private AdminController CreateAdminController(AppDbContext context)
        {
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            IApplicationService applicationService = new ApplicationService(context);
            IAnalyticsService analyticsService = new AnalyticsService(context);
            IScorecardService scorecardService = new ScorecardService(context, templateService);
            IJobService jobService = new JobService(context);
            IScorecardAnalyticsService scorecardAnalyticsService = new ScorecardAnalyticsService(context);

            var controller = new AdminController(
                context,
                new Mock<IWebHostEnvironment>().Object,
                applicationService, analyticsService, scorecardService,
                templateService, jobService, scorecardAnalyticsService)
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

        private async Task<(int applicationId, int candidateId)> SeedApplicationAsync(AppDbContext context)
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "Candidate",
                Email = $"test_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Engineer" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
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

            return (application.Id, candidate.Id);
        }

        private async Task<Scorecard> SeedScorecardAsync(
            AppDbContext context,
            int candidateId,
            decimal score = 3.0m,
            bool isArchived = false)
        {
            var facet = new Facet { Name = "Facet_" + Guid.NewGuid() };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            var scorecard = new Scorecard
            {
                CandidateId = candidateId,
                SubmittedBy = "interviewer@test.com",
                SubmittedAt = DateTime.UtcNow,
                IsArchived = isArchived,
                ArchivedAt = isArchived ? DateTime.UtcNow : null
            };
            context.Scorecards.Add(scorecard);
            await context.SaveChangesAsync();

            context.ScorecardResponses.Add(new ScorecardResponse
            {
                ScorecardId = scorecard.Id,
                FacetId = facet.Id,
                FacetName = facet.Name,
                Score = score
            });
            await context.SaveChangesAsync();

            return scorecard;
        }

        // -------------------------------------------------------------------
        // Test 1: Archiving sets both flags correctly
        // -------------------------------------------------------------------

        [Fact]
        public async Task ArchiveScorecard_SetsFlagsCorrectly()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);
            var scorecard = await SeedScorecardAsync(context, candidateId);

            scorecard.IsArchived.Should().BeFalse("scorecard starts as active");
            scorecard.ArchivedAt.Should().BeNull("ArchivedAt starts as null");

            var before = DateTime.UtcNow;
            var controller = CreateAdminController(context);
            await controller.ArchiveScorecard(scorecard.Id);
            var after = DateTime.UtcNow;

            var updated = await context.Scorecards.FindAsync(scorecard.Id);
            updated!.IsArchived.Should().BeTrue();
            updated.ArchivedAt.Should().NotBeNull();
            updated.ArchivedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }

        // -------------------------------------------------------------------
        // Test 2: Archived scorecard not returned by GetScorecardsByCandidateAsync
        // -------------------------------------------------------------------

        [Fact]
        public async Task ArchivedScorecard_NotReturnedInCandidateScorecards()
        {
            using var context = CreateInMemoryContext();
            var (_, candidateId) = await SeedApplicationAsync(context);

            var active = await SeedScorecardAsync(context, candidateId, score: 4.0m);
            var archived = await SeedScorecardAsync(context, candidateId, score: 2.0m, isArchived: true);

            var service = CreateScorecardService(context);
            var results = await service.GetScorecardsByCandidateAsync(candidateId);

            results.Should().ContainSingle(s => s.Id == active.Id,
                "active scorecard must be returned");
            results.Should().NotContain(s => s.Id == archived.Id,
                "archived scorecard must be excluded");
        }

        // -------------------------------------------------------------------
        // Test 3: Archived scorecard excluded from analytics count + averages
        // -------------------------------------------------------------------

        [Fact]
        public async Task ArchivedScorecard_NotIncludedInAnalytics()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            // Two active scorecards and one archived
            await SeedScorecardAsync(context, candidateId, score: 4.0m);
            await SeedScorecardAsync(context, candidateId, score: 4.0m);
            await SeedScorecardAsync(context, candidateId, score: 1.0m, isArchived: true);

            var service = CreateAnalyticsService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            result.ScorecardCount.Should().Be(2,
                "archived scorecard must not count toward ScorecardCount");
            result.OverallAverageScore.Should().NotBeNull();
            result.OverallAverageScore!.Value.Should().BeApproximately(4.0m, 0.001m,
                "the archived low-score (1.0) must not affect the average; only the two active 4.0 scorecards count");
        }

        // -------------------------------------------------------------------
        // Test 4: Archiving one scorecard does not affect others
        // -------------------------------------------------------------------

        [Fact]
        public async Task ArchivingOneScorecard_DoesNotAffectOthers()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var sc1 = await SeedScorecardAsync(context, candidateId, score: 3.0m);
            var sc2 = await SeedScorecardAsync(context, candidateId, score: 5.0m);
            var sc3 = await SeedScorecardAsync(context, candidateId, score: 5.0m);

            // Archive only sc1
            var controller = CreateAdminController(context);
            await controller.ArchiveScorecard(sc1.Id);

            var service = CreateScorecardService(context);
            var results = await service.GetScorecardsByCandidateAsync(candidateId);

            results.Should().HaveCount(2,
                "only sc2 and sc3 should remain active after archiving sc1");
            results.Select(s => s.Id).Should().BeEquivalentTo(new[] { sc2.Id, sc3.Id });

            // sc1 must be archived in the database but not deleted
            var sc1InDb = await context.Scorecards.FindAsync(sc1.Id);
            sc1InDb.Should().NotBeNull("archived scorecards must not be physically deleted");
            sc1InDb!.IsArchived.Should().BeTrue();

            // sc2 and sc3 must be unaffected
            var sc2InDb = await context.Scorecards.FindAsync(sc2.Id);
            var sc3InDb = await context.Scorecards.FindAsync(sc3.Id);
            sc2InDb!.IsArchived.Should().BeFalse();
            sc3InDb!.IsArchived.Should().BeFalse();
        }

        // -------------------------------------------------------------------
        // Test 5: Analytics with all scorecards archived returns count 0 (< 2 threshold)
        // -------------------------------------------------------------------

        [Fact]
        public async Task Analytics_WhenAllScorecards_Archived_ReturnsZeroCount()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            await SeedScorecardAsync(context, candidateId, isArchived: true);
            await SeedScorecardAsync(context, candidateId, isArchived: true);

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.ScorecardCount.Should().Be(0);
            result.OverallAverageScore.Should().BeNull(
                "with no active scorecards the analytics threshold is not met");
            result.CategoryAverages.Should().BeEmpty();
        }
    }
}
