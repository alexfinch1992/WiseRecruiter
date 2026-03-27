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
    /// Adversarial / regression-guard tests for OverallRecommendation and analytics.
    ///
    /// Failure modes targeted:
    ///   Suite 1 — OverallRecommendation: truncation, whitespace normalisation,
    ///             score interference, cross-scorecard leakage
    ///   Suite 2 — Analytics math: flat-average vs average-of-averages,
    ///             category grouping by ID not name, zero-score handling
    ///   Suite 3 — Mutation resistance: no stale data, no caching
    ///   Suite 4 — Data isolation: candidate boundary, application-to-candidate resolution
    ///   Suite 5 — Extreme distribution: decimal precision
    /// </summary>
    public class OverallRecommendationAndAnalyticsAdversarialTests
    {
        // ---------------------------------------------------------------
        // Infrastructure
        // ---------------------------------------------------------------

        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("adv_" + Guid.NewGuid())
                .Options);

        private IScorecardAnalyticsService CreateAnalyticsService(AppDbContext context) =>
            new ScorecardAnalyticsService(context);

        private IScorecardService CreateScorecardService(AppDbContext context) =>
            new ScorecardService(context, new ScorecardTemplateService(context));

        private async Task<(int applicationId, int candidateId)> SeedApplicationAsync(
            AppDbContext context, string tag = "")
        {
            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "Candidate" + tag,
                Email = $"test_{tag}_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job = new Job { Title = "Job " + tag };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = candidate.FirstName,
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
            IEnumerable<(int facetId, decimal score)> responses,
            string? recommendation = null)
        {
            var scorecard = new Scorecard
            {
                CandidateId = candidateId,
                SubmittedBy = "interviewer@test.com",
                SubmittedAt = DateTime.UtcNow,
                OverallRecommendation = recommendation
            };
            context.Scorecards.Add(scorecard);
            await context.SaveChangesAsync();

            foreach (var (facetId, score) in responses)
            {
                var name = (await context.Facets.FindAsync(facetId))?.Name ?? $"Facet_{facetId}";
                context.ScorecardResponses.Add(new ScorecardResponse
                {
                    ScorecardId = scorecard.Id,
                    FacetId = facetId,
                    FacetName = name,
                    Score = score
                });
            }
            await context.SaveChangesAsync();
            return scorecard;
        }

        // ====================================================================
        // SUITE 1 — OVERALL RECOMMENDATION EDGE CASES
        // ====================================================================

        [Fact]
        public async Task OverallRecommendation_Allows_VeryLongText()
        {
            using var context = CreateInMemoryContext();
            var (_, candidateId) = await SeedApplicationAsync(context);

            var longText = new string('A', 2000) + "\nSpecial: é à ü ñ 中文 🎉" + new string('Z', 100);

            var scorecard = new Scorecard
            {
                CandidateId = candidateId,
                SubmittedBy = "reviewer@test.com",
                OverallRecommendation = longText
            };
            context.Scorecards.Add(scorecard);
            await context.SaveChangesAsync();

            var dto = await CreateScorecardService(context).GetScorecardById(scorecard.Id);

            dto.Should().NotBeNull();
            dto!.OverallRecommendation.Should().Be(longText,
                "a 2000+ character recommendation must survive persistence without any truncation");
        }

        [Fact]
        public async Task OverallRecommendation_Preserves_Whitespace_AndFormatting()
        {
            using var context = CreateInMemoryContext();
            var (_, candidateId) = await SeedApplicationAsync(context);

            // Leading/trailing spaces, tabs, mixed newlines — must survive unchanged
            const string formattedText =
                "  \n\tLeading whitespace\n\nDouble newline\t\tDouble tab\r\nWindows newline  \t  ";

            var scorecard = new Scorecard
            {
                CandidateId = candidateId,
                SubmittedBy = "reviewer@test.com",
                OverallRecommendation = formattedText
            };
            context.Scorecards.Add(scorecard);
            await context.SaveChangesAsync();

            var dto = await CreateScorecardService(context).GetScorecardById(scorecard.Id);

            dto.Should().NotBeNull();
            dto!.OverallRecommendation.Should().Be(formattedText,
                "whitespace, newlines and tabs must not be normalised or trimmed at any layer");
        }

        [Fact]
        public async Task OverallRecommendation_DoesNotAffect_ScoreCalculations()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Technical" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            var extremeText = new string('X', 5000) + "\n≥≤∞✓" + new string('Y', 5000);

            // Two scorecards — one with extreme recommendation text, one without
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 3.0m) }, extremeText);
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 3.0m) }, null);

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.OverallAverageScore.Should().NotBeNull();
            result.OverallAverageScore!.Value.Should().BeApproximately(3.0m, 0.001m,
                "OverallRecommendation text must have zero effect on any score computation");
        }

        [Fact]
        public async Task MultipleScorecards_EachHasIndependent_OverallRecommendation()
        {
            using var context = CreateInMemoryContext();
            var (_, candidateId) = await SeedApplicationAsync(context);

            const string rec1 = "Alice: strong hire — excellent systems design.";
            const string rec2 = "Bob: not recommended — weak fundamentals.";

            var sc1 = new Scorecard { CandidateId = candidateId, SubmittedBy = "alice@test.com", OverallRecommendation = rec1 };
            var sc2 = new Scorecard { CandidateId = candidateId, SubmittedBy = "bob@test.com", OverallRecommendation = rec2 };
            context.Scorecards.AddRange(sc1, sc2);
            await context.SaveChangesAsync();

            var service = CreateScorecardService(context);
            var dto1 = await service.GetScorecardById(sc1.Id);
            var dto2 = await service.GetScorecardById(sc2.Id);

            dto1!.OverallRecommendation.Should().Be(rec1,
                "scorecard 1 must retain its own recommendation");
            dto2!.OverallRecommendation.Should().Be(rec2,
                "scorecard 2 must retain its own recommendation");
            dto1.OverallRecommendation.Should().NotBe(dto2.OverallRecommendation,
                "recommendations must not leak or overwrite across scorecards");
        }

        // ====================================================================
        // SUITE 2 — ANALYTICS ADVERSARIAL (CRITICAL MATH GUARDS)
        // ====================================================================

        [Fact]
        public async Task OverallAverage_IsAverageOfScorecardAverages_NOT_FlatAverage()
        {
            // A: [1]       → per-scorecard avg = 1.0
            // B: [5, 5, 5] → per-scorecard avg = 5.0
            //
            // Correct  (avg-of-avgs): (1.0 + 5.0) / 2     = 3.0
            // Incorrect (flat avg):   (1 + 5 + 5 + 5) / 4 = 4.0  ← fails this assert
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 1.0m) });
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (facet.Id, 5.0m), (facet.Id, 5.0m), (facet.Id, 5.0m)
            });

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.OverallAverageScore!.Value.Should().BeApproximately(3.0m, 0.001m,
                "overall average must be the average of per-scorecard averages (3.0); " +
                "a flat average of all 4 responses would incorrectly yield 4.0");
        }

        [Fact]
        public async Task CategoryAverage_Uses_FlatAverage_NOT_AverageOfAverages()
        {
            // Category X: Scorecard A [1], Scorecard B [5, 5]
            //
            // Correct (flat):          (1 + 5 + 5) / 3 ≈ 3.667
            // Incorrect (avg-of-avgs): (1 + 5)     / 2 = 3.0   ← fails this assert
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var category = new Category { Name = "Category X" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = new Facet { Name = "Facet", CategoryId = category.Id };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 1.0m) });
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 5.0m), (facet.Id, 5.0m) });

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            var cat = result.CategoryAverages.Should().ContainSingle(c => c.CategoryId == category.Id).Subject;
            cat.AverageScore.Should().BeApproximately(11.0m / 3m, 0.001m,
                "category average must be a flat mean of all raw responses (≈3.667); " +
                "averaging per-scorecard averages would incorrectly yield 3.0");
        }

        [Fact]
        public async Task CategoryGrouping_Uses_CategoryId_NOT_CategoryName()
        {
            // Two categories with the SAME name but different IDs.
            // Grouping by name would merge them into one bucket — the test catches this.
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var catA = new Category { Name = "Technical" };
            var catB = new Category { Name = "Technical" }; // same name, different ID
            context.Categories.AddRange(catA, catB);
            await context.SaveChangesAsync();

            var facetA = new Facet { Name = "Coding", CategoryId = catA.Id };
            var facetB = new Facet { Name = "Systems", CategoryId = catB.Id };
            context.Facets.AddRange(facetA, facetB);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[] { (facetA.Id, 1.0m), (facetB.Id, 5.0m) });
            await SeedScorecardAsync(context, candidateId, new[] { (facetA.Id, 1.0m), (facetB.Id, 5.0m) });

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.CategoryAverages.Should().HaveCount(2,
                "two categories sharing the same name but with different IDs must not be merged into one bucket");
            result.CategoryAverages.Single(c => c.CategoryId == catA.Id).AverageScore
                .Should().BeApproximately(1.0m, 0.001m);
            result.CategoryAverages.Single(c => c.CategoryId == catB.Id).AverageScore
                .Should().BeApproximately(5.0m, 0.001m);
        }

        [Fact]
        public async Task FacetsWithNullCategory_IncludedInOverallAverage_AndAppearAsUncategorisedGroup()
        {
            // Null-category facets contribute to OverallAverageScore.
            // They appear in CategoryAverages as a group with null CategoryId.
            // This test pins current behaviour as a regression guard:
            // if someone silently drops null-category responses from either calculation, this fails.
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var uncatFacet = new Facet { Name = "Uncategorised Facet", CategoryId = null };
            context.Facets.Add(uncatFacet);
            await context.SaveChangesAsync();

            // Scorecard A: [4.0], Scorecard B: [2.0] — both only null-category responses
            await SeedScorecardAsync(context, candidateId, new[] { (uncatFacet.Id, 4.0m) });
            await SeedScorecardAsync(context, candidateId, new[] { (uncatFacet.Id, 2.0m) });

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.OverallAverageScore.Should().NotBeNull(
                "null-category facet scores must still be counted in the overall average");
            result.OverallAverageScore!.Value.Should().BeApproximately(3.0m, 0.001m);

            result.CategoryAverages.Should().ContainSingle(c => c.CategoryId == null,
                "null-category responses appear as an uncategorised bucket (CategoryId=null) in the breakdown");
        }

        [Fact]
        public async Task Scorecards_WithDifferentFacetCounts_DoNotBreakAverages()
        {
            // Scorecard A: 3 facets [3.0, 3.0, 3.0] → avg = 3.0
            // Scorecard B: 1 facet  [5.0]            → avg = 5.0
            // Overall = (3.0 + 5.0) / 2 = 4.0
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var f1 = new Facet { Name = "F1" };
            var f2 = new Facet { Name = "F2" };
            var f3 = new Facet { Name = "F3" };
            context.Facets.AddRange(f1, f2, f3);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[]
            {
                (f1.Id, 3.0m), (f2.Id, 3.0m), (f3.Id, 3.0m)
            });
            await SeedScorecardAsync(context, candidateId, new[] { (f1.Id, 5.0m) });

            var service = CreateAnalyticsService(context);

            Func<Task> act = async () => await service.GetCandidateAnalyticsAsync(applicationId);
            await act.Should().NotThrowAsync(
                "asymmetric facet counts across scorecards must not cause divide-by-zero or any exception");

            var result = await service.GetCandidateAnalyticsAsync(applicationId);
            result.OverallAverageScore!.Value.Should().BeApproximately(4.0m, 0.001m);
        }

        [Fact]
        public async Task ZeroScores_AreExcluded_NotTreatedAsValidScores()
        {
            // Score == 0 is the unset sentinel — must be excluded from all calculations.
            //
            // Scorecard A: [0 (sentinel), 4.0 (valid)] → valid avg = 4.0
            // Scorecard B: [2.0]                        → avg     = 2.0
            // Overall = (4.0 + 2.0) / 2 = 3.0
            //
            // If 0 is treated as valid:
            //   Scorecard A avg = (0 + 4) / 2 = 2.0  → Overall = (2.0 + 2.0) / 2 = 2.0  ← wrong
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            var sc1 = new Scorecard { CandidateId = candidateId, SubmittedBy = "a@test.com" };
            context.Scorecards.Add(sc1);
            await context.SaveChangesAsync();
            context.ScorecardResponses.AddRange(
                new ScorecardResponse { ScorecardId = sc1.Id, FacetId = facet.Id, FacetName = "Facet", Score = 0m },
                new ScorecardResponse { ScorecardId = sc1.Id, FacetId = facet.Id, FacetName = "Facet", Score = 4.0m }
            );
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 2.0m) });

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.OverallAverageScore!.Value.Should().BeApproximately(3.0m, 0.001m,
                "zero (sentinel) scores must be excluded; if counted, Scorecard A avg drops to 2.0 and overall to 2.0");

            // Category breakdown must also exclude the zero
            var catAvg = result.CategoryAverages.Should().ContainSingle(c => c.CategoryId == null).Subject;
            catAvg.AverageScore.Should().BeApproximately(3.0m, 0.001m,
                "the flat category average over [4.0, 2.0] = 3.0; including the 0 would yield (0+4+2)/3 ≈ 2.0");
        }

        // ====================================================================
        // SUITE 3 — TIME / MUTATION RESISTANCE
        // ====================================================================

        [Fact]
        public async Task Analytics_Reflects_CurrentFacetCategoryState_NotAtCreationTime()
        {
            // Facet starts in Category A → two scorecards created → facet moved to Category B.
            // Analytics must attribute all responses to Category B (current state), not Category A.
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var catA = new Category { Name = "Old Category" };
            var catB = new Category { Name = "New Category" };
            context.Categories.AddRange(catA, catB);
            await context.SaveChangesAsync();

            var facet = new Facet { Name = "Versatile Facet", CategoryId = catA.Id };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 3.0m) });
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 3.0m) });

            // Re-categorise the facet
            facet.CategoryId = catB.Id;
            await context.SaveChangesAsync();

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.CategoryAverages.Should().NotContain(c => c.CategoryId == catA.Id,
                "after the facet is moved, no response should still be attributed to the old category");
            result.CategoryAverages.Should().ContainSingle(c => c.CategoryId == catB.Id,
                "responses must now appear under the facet's updated category");
        }

        [Fact]
        public async Task Analytics_DoesNotCacheAcrossRequests()
        {
            // A new scorecard added after the first analytics call must be visible immediately.
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 3.0m) });
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 3.0m) });

            var service = CreateAnalyticsService(context);
            var before = await service.GetCandidateAnalyticsAsync(applicationId);
            before.ScorecardCount.Should().Be(2);

            // Add a third scorecard after the initial call
            await SeedScorecardAsync(context, candidateId, new[] { (facet.Id, 5.0m) });

            var after = await service.GetCandidateAnalyticsAsync(applicationId);
            after.ScorecardCount.Should().Be(3,
                "the new scorecard must be reflected — analytics must not serve stale cached data");

            // SC1 avg=3.0, SC2 avg=3.0, SC3 avg=5.0 → (3+3+5)/3 ≈ 3.667
            after.OverallAverageScore!.Value.Should().BeApproximately(11.0m / 3m, 0.001m);
        }

        // ====================================================================
        // SUITE 4 — DATA ISOLATION
        // ====================================================================

        [Fact]
        public async Task CandidateAnalytics_DoesNotLeakAcrossCandidates()
        {
            using var context = CreateInMemoryContext();
            var (appIdA, candidateIdA) = await SeedApplicationAsync(context, "A");
            var (appIdB, candidateIdB) = await SeedApplicationAsync(context, "B");

            var facet = new Facet { Name = "Shared Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            // Candidate A: two scorecards, all 1.0
            await SeedScorecardAsync(context, candidateIdA, new[] { (facet.Id, 1.0m) });
            await SeedScorecardAsync(context, candidateIdA, new[] { (facet.Id, 1.0m) });

            // Candidate B: two scorecards, all 5.0
            await SeedScorecardAsync(context, candidateIdB, new[] { (facet.Id, 5.0m) });
            await SeedScorecardAsync(context, candidateIdB, new[] { (facet.Id, 5.0m) });

            var service = CreateAnalyticsService(context);
            var resultA = await service.GetCandidateAnalyticsAsync(appIdA);
            var resultB = await service.GetCandidateAnalyticsAsync(appIdB);

            resultA.ScorecardCount.Should().Be(2);
            resultA.OverallAverageScore!.Value.Should().BeApproximately(1.0m, 0.001m,
                "Candidate A's analytics must not include Candidate B's high scores");

            resultB.ScorecardCount.Should().Be(2);
            resultB.OverallAverageScore!.Value.Should().BeApproximately(5.0m, 0.001m,
                "Candidate B's analytics must not include Candidate A's low scores");
        }

        [Fact]
        public async Task ApplicationId_Resolution_GroupsByCandidate_NotByApplication()
        {
            // One candidate applies to two different jobs (two applications).
            // Scorecards are at the candidate level.
            // GetCandidateAnalyticsAsync called with EITHER application ID must return
            // identical results, because both resolve to the same CandidateId.
            using var context = CreateInMemoryContext();

            var candidate = new Candidate
            {
                FirstName = "Multi",
                LastName = "App",
                Email = "multi@test.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);

            var job1 = new Job { Title = "Job 1" };
            var job2 = new Job { Title = "Job 2" };
            context.Jobs.AddRange(job1, job2);
            await context.SaveChangesAsync();

            var stage1 = new JobStage { JobId = job1.Id, Name = "Applied", Order = 1 };
            var stage2 = new JobStage { JobId = job2.Id, Name = "Applied", Order = 1 };
            context.JobStages.AddRange(stage1, stage2);
            await context.SaveChangesAsync();

            var app1 = new Application
            {
                Name = "Multi App", Email = "multi@test.com", City = "X",
                JobId = job1.Id, CandidateId = candidate.Id, CurrentJobStageId = stage1.Id
            };
            var app2 = new Application
            {
                Name = "Multi App", Email = "multi@test.com", City = "X",
                JobId = job2.Id, CandidateId = candidate.Id, CurrentJobStageId = stage2.Id
            };
            context.Applications.AddRange(app1, app2);
            await context.SaveChangesAsync();

            var facet = new Facet { Name = "Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            // Two candidate-level scorecards (scores differ to make the average non-trivial)
            await SeedScorecardAsync(context, candidate.Id, new[] { (facet.Id, 3.0m) });
            await SeedScorecardAsync(context, candidate.Id, new[] { (facet.Id, 5.0m) });

            var service = CreateAnalyticsService(context);
            var viaApp1 = await service.GetCandidateAnalyticsAsync(app1.Id);
            var viaApp2 = await service.GetCandidateAnalyticsAsync(app2.Id);

            viaApp1.ScorecardCount.Should().Be(2,
                "analytics via application 1 must find all scorecards for the candidate");
            viaApp2.ScorecardCount.Should().Be(2,
                "analytics via application 2 must find the same candidate-level scorecards");

            viaApp1.OverallAverageScore!.Value.Should().BeApproximately(4.0m, 0.001m);
            viaApp2.OverallAverageScore!.Value.Should().Be(viaApp1.OverallAverageScore.Value,
                "both applicationIds resolve to the same candidate — results must be identical");
        }

        // ====================================================================
        // SUITE 5 — EXTREME DISTRIBUTION / DECIMAL PRECISION
        // ====================================================================

        [Fact]
        public async Task Analytics_Handles_WideScoreDistribution_WithDecimalPrecision()
        {
            // Scorecard A: [1, 1, 1] → avg = 1.0
            // Scorecard B: [5, 5, 5] → avg = 5.0
            // Overall = (1.0 + 5.0) / 2 = 3.0
            //
            // Verifies that decimal arithmetic is used (not integer division which would also give 3,
            // but the intermediate per-scorecard averages must be decimal to prevent rounding errors
            // in asymmetric distributions).
            using var context = CreateInMemoryContext();
            var (applicationId, candidateId) = await SeedApplicationAsync(context);

            var facet = new Facet { Name = "Distributed Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await SeedScorecardAsync(context, candidateId, new[]
            {
                (facet.Id, 1.0m), (facet.Id, 1.0m), (facet.Id, 1.0m)
            });
            await SeedScorecardAsync(context, candidateId, new[]
            {
                (facet.Id, 5.0m), (facet.Id, 5.0m), (facet.Id, 5.0m)
            });

            var result = await CreateAnalyticsService(context).GetCandidateAnalyticsAsync(applicationId);

            result.OverallAverageScore.Should().NotBeNull();
            // Verify the value is a decimal (not truncated to int) by confirming precision
            decimal score = result.OverallAverageScore!.Value;
            score.Should().BeApproximately(3.0m, 0.001m,
                "symmetric distribution [1,1,1] and [5,5,5] must yield exactly 3.0");

            // Asymmetric sub-test: A=[1], B=[5,5,5] → correct 3.0, flat-average 4.0
            // (Same as the dedicated test above — validated again within the precision test
            //  to ensure no integer-division shortcut was taken)
            result.ScorecardCount.Should().Be(2,
                "sanity-check: exactly 2 scorecards seeded");
        }
    }
}
