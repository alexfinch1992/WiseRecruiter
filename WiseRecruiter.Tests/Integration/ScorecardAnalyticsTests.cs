using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Tests for IScorecardAnalyticsService.GetCandidateAnalyticsAsync.
    ///
    /// Failure modes targeted:
    ///   - Early-return when scorecard count is insufficient
    ///   - OverallAverageScore being average-of-averages (not flat average)
    ///   - CategoryAverages being accidentally average-of-averages
    ///   - Grouping by FacetName instead of FacetId
    ///   - Zero/default scores silently inflating or deflating averages
    /// </summary>
    public class ScorecardAnalyticsTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("scorecard_analytics_" + Guid.NewGuid())
                .Options);

        private IScorecardAnalyticsService CreateService(AppDbContext context) =>
            new ScorecardAnalyticsService(context);

        /// <summary>
        /// Seeds one candidate with one application and returns (applicationId, candidateId).
        /// Scorecards must be added separately.
        /// </summary>
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
            IEnumerable<(int facetId, decimal score)> responses)
        {
            var scorecard = new Scorecard
            {
                CandidateId = candidateId,
                SubmittedBy = "interviewer@test.com",
                SubmittedAt = DateTime.UtcNow
            };
            context.Scorecards.Add(scorecard);
            await context.SaveChangesAsync();

            var facetNames = new Dictionary<int, string>();
            foreach (var (facetId, score) in responses)
            {
                if (!facetNames.TryGetValue(facetId, out var fname))
                {
                    fname = (await context.Facets.FindAsync(facetId))?.Name ?? $"Facet_{facetId}";
                    facetNames[facetId] = fname;
                }

                context.ScorecardResponses.Add(new ScorecardResponse
                {
                    ScorecardId = scorecard.Id,
                    FacetId = facetId,
                    FacetName = fname,
                    Score = score
                });
            }
            await context.SaveChangesAsync();

            return scorecard;
        }

        // -------------------------------------------------------------------
        // Test 1: Insufficient scorecards → partial DTO only
        // -------------------------------------------------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task Returns_NullAnalytics_When_LessThanTwoScorecards(int scorecardCount)
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Technical" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            for (int i = 0; i < scorecardCount; i++)
                await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 4.0m) });

            var service = CreateService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            result.ScorecardCount.Should().Be(scorecardCount);
            result.OverallAverageScore.Should().BeNull();
            result.CategoryAverages.Should().BeEmpty();
        }

        // -------------------------------------------------------------------
        // Test 2: OverallAverageScore is average-of-per-scorecard-averages
        // -------------------------------------------------------------------

        [Fact]
        public async Task OverallAverageScore_ComputedCorrectly_MultipleScorecards()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facetA = new Facet { Name = "Facet A" };
            var facetB = new Facet { Name = "Facet B" };
            context.Facets.AddRange(facetA, facetB);
            await context.SaveChangesAsync();

            // Scorecard 1: scores [2.0, 4.0] → per-scorecard avg = 3.0
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (facetA.Id, 2.0m),
                (facetB.Id, 4.0m)
            });

            // Scorecard 2: scores [1.0, 5.0] → per-scorecard avg = 3.0
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (facetA.Id, 1.0m),
                (facetB.Id, 5.0m)
            });

            // Scorecard 3: scores [3.0] → per-scorecard avg = 3.0
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (facetA.Id, 3.0m)
            });

            var service = CreateService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            result.ScorecardCount.Should().Be(3);
            result.OverallAverageScore.Should().NotBeNull();
            // Each scorecard avg = 3.0 → overall avg = 3.0
            result.OverallAverageScore!.Value.Should().BeApproximately(3.0m, 0.001m);
        }

        // -------------------------------------------------------------------
        // Test 3: CategoryAverages groups correctly across scorecards
        // -------------------------------------------------------------------

        [Fact]
        public async Task CategoryAverages_GroupedCorrectly_AcrossScorecards()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var techCategory = new Category { Name = "Technical" };
            var softCategory = new Category { Name = "Soft Skills" };
            context.Categories.AddRange(techCategory, softCategory);
            await context.SaveChangesAsync();

            var techFacet = new Facet { Name = "Coding", CategoryId = techCategory.Id };
            var softFacet = new Facet { Name = "Communication", CategoryId = softCategory.Id };
            context.Facets.AddRange(techFacet, softFacet);
            await context.SaveChangesAsync();

            // Scorecard 1: Tech=4.0, Soft=3.0
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (techFacet.Id, 4.0m),
                (softFacet.Id, 3.0m)
            });

            // Scorecard 2: Tech=2.0, Soft=5.0
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (techFacet.Id, 2.0m),
                (softFacet.Id, 5.0m)
            });

            var service = CreateService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            result.CategoryAverages.Should().HaveCount(2);

            var tech = result.CategoryAverages.Single(c => c.CategoryId == techCategory.Id);
            tech.CategoryName.Should().Be("Technical");
            tech.AverageScore.Should().BeApproximately(3.0m, 0.001m); // (4+2)/2

            var soft = result.CategoryAverages.Single(c => c.CategoryId == softCategory.Id);
            soft.CategoryName.Should().Be("Soft Skills");
            soft.AverageScore.Should().BeApproximately(4.0m, 0.001m); // (3+5)/2
        }

        // -------------------------------------------------------------------
        // Test 4: Responses with Score == 0 are excluded (default/unset)
        // -------------------------------------------------------------------

        [Fact]
        public async Task Ignores_NullScores_InCalculations()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Technical" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            // Scorecard 1: one valid score (4.0) and one invalid (0 = unset)
            var sc1 = new Scorecard { CandidateId = candidateId, SubmittedBy = "a@test.com" };
            context.Scorecards.Add(sc1);
            await context.SaveChangesAsync();

            context.ScorecardResponses.AddRange(
                new ScorecardResponse { ScorecardId = sc1.Id, FacetId = facet.Id, FacetName = "Technical", Score = 4.0m },
                // Score = 0: default decimal, should be excluded
                new ScorecardResponse { ScorecardId = sc1.Id, FacetId = facet.Id, FacetName = "Technical", Score = 0m }
            );
            await context.SaveChangesAsync();

            // Scorecard 2: one valid score (2.0)
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 2.0m) });

            var service = CreateService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            // Scorecard 1 valid scores → avg = 4.0 (0 excluded, not (4+0)/2 = 2.0)
            // Scorecard 2 valid scores → avg = 2.0
            // Overall = (4.0 + 2.0) / 2 = 3.0
            result.OverallAverageScore.Should().NotBeNull();
            result.OverallAverageScore!.Value.Should().BeApproximately(3.0m, 0.001m);

            // Category average should also exclude the 0-score: (4.0 + 2.0) / 2 = 3.0
            var catAvg = result.CategoryAverages.Should().ContainSingle().Subject;
            catAvg.AverageScore.Should().BeApproximately(3.0m, 0.001m);
        }

        // -------------------------------------------------------------------
        // Test 5: Grouping is by FacetId, not FacetName
        // -------------------------------------------------------------------

        [Fact]
        public async Task Uses_FacetId_Not_Name_For_CategoryGrouping()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var categoryA = new Category { Name = "Category A" };
            var categoryB = new Category { Name = "Category B" };
            context.Categories.AddRange(categoryA, categoryB);
            await context.SaveChangesAsync();

            // Two facets with the SAME name but different categories
            var facet1 = new Facet { Name = "Communication", CategoryId = categoryA.Id };
            var facet2 = new Facet { Name = "Communication", CategoryId = categoryB.Id };
            context.Facets.AddRange(facet1, facet2);
            await context.SaveChangesAsync();

            // Scorecard 1: facet1 (Cat A) = 5.0
            await SeedScorecardAsync(context, candidateId, new[] { (facet1.Id, 5.0m) });

            // Scorecard 2: facet2 (Cat B) = 1.0
            await SeedScorecardAsync(context, candidateId, new[] { (facet2.Id, 1.0m) });

            var service = CreateService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            // Correct (ID-based): two separate categories with averages 5.0 and 1.0
            // Wrong (name-based): one merged bucket with average 3.0 (both "Communication" merged)
            result.CategoryAverages.Should().HaveCount(2, "facets with the same name but different CategoryIds must remain separate");

            result.CategoryAverages.Should().ContainSingle(c => c.CategoryId == categoryA.Id)
                .Which.AverageScore.Should().BeApproximately(5.0m, 0.001m);

            result.CategoryAverages.Should().ContainSingle(c => c.CategoryId == categoryB.Id)
                .Which.AverageScore.Should().BeApproximately(1.0m, 0.001m);
        }

        // -------------------------------------------------------------------
        // Test 6: CategoryAverage is flat average, NOT average-of-averages
        // -------------------------------------------------------------------

        [Fact]
        public async Task CategoryAverage_IsNotAverageOfAverages()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var category = new Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = new Facet { Name = "Coding", CategoryId = category.Id };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            // Scorecard 1: two responses [1.0, 1.0] → per-scorecard avg = 1.0
            var sc1 = new Scorecard { CandidateId = candidateId, SubmittedBy = "a@test.com" };
            context.Scorecards.Add(sc1);
            await context.SaveChangesAsync();
            context.ScorecardResponses.AddRange(
                new ScorecardResponse { ScorecardId = sc1.Id, FacetId = facet.Id, FacetName = "Coding", Score = 1.0m },
                new ScorecardResponse { ScorecardId = sc1.Id, FacetId = facet.Id, FacetName = "Coding", Score = 1.0m }
            );
            await context.SaveChangesAsync();

            // Scorecard 2: one response [5.0] → per-scorecard avg = 5.0
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 5.0m) });

            var service = CreateService(context);
            var result = await service.GetCandidateAnalyticsAsync(applicationId);

            var catAvg = result.CategoryAverages.Should().ContainSingle().Subject;

            // Flat average of all 3 responses: (1.0 + 1.0 + 5.0) / 3 ≈ 2.33
            // Average-of-averages (wrong): (1.0 + 5.0) / 2 = 3.0
            catAvg.AverageScore.Should().BeApproximately(2.333m, 0.001m,
                "category average must be a flat average of raw scores, not an average of per-scorecard averages");
        }
    }
}
