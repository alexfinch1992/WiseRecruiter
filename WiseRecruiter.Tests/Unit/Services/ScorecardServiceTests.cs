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

namespace WiseRecruiter.Tests.Unit.Services
{
    /// <summary>
    /// TDD guardrails for Scorecard behavior.
    /// These tests intentionally define expected business behavior first.
    /// </summary>
    public class ScorecardServiceTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "scorecard_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        private async Task<int> SeedCandidateAsync(AppDbContext context)
        {
            var person = new Candidate
            {
                FirstName = "Candidate",
                LastName = "One",
                Email = "candidate@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(person);
            await context.SaveChangesAsync();

            var job = new Job { Title = "Platform Engineer" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Candidate One",
                Email = "candidate@example.com",
                City = "Sydney",
                JobId = job.Id,
                CandidateId = person.Id,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return person.Id;
        }

        private async Task<int> SeedDefaultTemplateWithFacetsAsync(AppDbContext context, params (string name, int order)[] facets)
        {
            var template = new ScorecardTemplate { Name = "Default Scorecard" };
            context.ScorecardTemplates.Add(template);
            await context.SaveChangesAsync();

            foreach (var (name, order) in facets)
            {
                var facet = new Facet
                {
                    Name = name
                };
                context.Facets.Add(facet);
                await context.SaveChangesAsync();

                context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
                {
                    ScorecardTemplateId = template.Id,
                    FacetId = facet.Id,
                    ScorecardFacetId = facet.Id
                });
                await context.SaveChangesAsync();
            }

            return template.Id;
        }

        private async Task<int> SeedTemplateWithFacetsAsync(AppDbContext context, string templateName, params (string name, int order)[] facets)
        {
            var template = new ScorecardTemplate { Name = templateName };
            context.ScorecardTemplates.Add(template);
            await context.SaveChangesAsync();

            foreach (var (name, order) in facets)
            {
                var facet = new Facet
                {
                    Name = $"{templateName}-{name}"
                };
                context.Facets.Add(facet);
                await context.SaveChangesAsync();

                context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
                {
                    ScorecardTemplateId = template.Id,
                    FacetId = facet.Id,
                    ScorecardFacetId = facet.Id
                });
                await context.SaveChangesAsync();
            }

            return template.Id;
        }

        private async Task<int> SeedApplicationForCandidateAsync(AppDbContext context, int candidateId, int? scorecardTemplateId = null)
        {
            var job = new Job
            {
                Title = "Template-specific job",
                ScorecardTemplateId = scorecardTemplateId
            };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var candidate = await context.Candidates.FindAsync(candidateId);

            var application = new Application
            {
                Name = candidate == null ? "Candidate" : $"{candidate.FirstName} {candidate.LastName}",
                Email = candidate?.Email ?? "candidate@example.com",
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidateId,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application.Id;
        }

        [Fact]
        public async Task CreateScorecardAsync_WithValidCandidate_CreatesScorecard()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);

            var scorecard = await service.CreateScorecardAsync(candidateId, "interviewer@wiserecruiter.local");

            scorecard.Should().NotBeNull();
            scorecard.Id.Should().BeGreaterThan(0);
            scorecard.CandidateId.Should().Be(candidateId);
            scorecard.SubmittedBy.Should().Be("interviewer@wiserecruiter.local");
            scorecard.SubmittedAt.Should().NotBe(default);
        }

        [Fact]
        public async Task CreateScorecardAsync_WithoutValidCandidate_ThrowsControlledException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardService(context);

            Func<Task> act = async () => await service.CreateScorecardAsync(9999, "interviewer@wiserecruiter.local");

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task AddResponsesAsync_WithValidScorecard_SavesAllFacetEvaluations()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "interviewer@wiserecruiter.local");

            var responses = new List<ScorecardResponse>
            {
                new ScorecardResponse { FacetName = "Communication", Score = 3.7m, Notes = "Clear and concise" },
                new ScorecardResponse { FacetName = "Technical Depth", Score = 4.2m, Notes = "Strong problem solving" },
                new ScorecardResponse { FacetName = "Collaboration", Score = 3.9m, Notes = "Good teamwork signals" }
            };

            var saved = await service.AddResponsesAsync(scorecard.Id, responses);

            saved.Should().HaveCount(3);
            saved.Select(r => r.FacetName).Should().Equal("Communication", "Technical Depth", "Collaboration");
            saved.Select(r => r.Score).Should().Equal(3.7m, 4.2m, 3.9m);
            saved.Select(r => r.Notes).Should().Equal("Clear and concise", "Strong problem solving", "Good teamwork signals");
        }

        [Fact]
        public async Task AddResponsesAsync_PreservesDecimalPrecision()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "precision@wiserecruiter.local");

            var saved = await service.AddResponsesAsync(
                scorecard.Id,
                new[] { new ScorecardResponse { FacetName = "Problem Solving", Score = 3.7m, Notes = "Precise score expected" } });

            saved.Single().Score.Should().Be(3.7m);
        }

        [Fact]
        public async Task AddResponsesAsync_WithoutValidScorecard_ThrowsControlledException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardService(context);

            Func<Task> act = async () => await service.AddResponsesAsync(
                4242,
                new[] { new ScorecardResponse { FacetName = "Communication", Score = 3.8m, Notes = "N/A" } });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task AddResponsesAsync_WithScoreOutsideValidRange_FailsValidation()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "validator@wiserecruiter.local");

            Func<Task> act = async () => await service.AddResponsesAsync(
                scorecard.Id,
                new[] { new ScorecardResponse { FacetName = "Technical Depth", Score = 5.7m, Notes = "Out of range" } });

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task CalculateAverageScoreAsync_WithResponses_UsesDecimalPrecisionWithoutPrematureRounding()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "avg@wiserecruiter.local");

            await service.AddResponsesAsync(
                scorecard.Id,
                new[]
                {
                    new ScorecardResponse { FacetName = "A", Score = 3.7m, Notes = "" },
                    new ScorecardResponse { FacetName = "B", Score = 4.1m, Notes = "" },
                    new ScorecardResponse { FacetName = "C", Score = 2.8m, Notes = "" }
                });

            var average = await service.CalculateAverageScoreAsync(scorecard.Id);

            average.Should().Be((3.7m + 4.1m + 2.8m) / 3m);
        }

        [Fact]
        public async Task CalculateAverageScoreAsync_EmptyScorecard_DoesNotThrow()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "empty@wiserecruiter.local");

            var average = await service.CalculateAverageScoreAsync(scorecard.Id);

            average.Should().Be(0m);
        }

        [Fact]
        public async Task GetScorecardById_WithResponses_ReturnsMappedDetailWithCorrectResponses()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "viewer@wiserecruiter.local");

            await service.AddResponsesAsync(
                scorecard.Id,
                new[]
                {
                    new ScorecardResponse { FacetName = "Communication", Score = 3.7m, Notes = "Clear and concise" },
                    new ScorecardResponse { FacetName = "Problem Solving", Score = 4.2m, Notes = "Strong reasoning" }
                });

            var detail = await service.GetScorecardById(scorecard.Id);

            detail.Should().NotBeNull();
            detail!.Id.Should().Be(scorecard.Id);
            detail.CandidateId.Should().Be(candidateId);
            detail.Responses.Should().HaveCount(2);
            detail.Responses.Select(r => r.FacetName).Should().Equal("Communication", "Problem Solving");
            detail.Responses.Select(r => r.Score).Should().Equal(3.7m, 4.2m);
            detail.Responses.Select(r => r.Notes).Should().Equal("Clear and concise", "Strong reasoning");
        }

        [Fact]
        public async Task GetScorecardById_ComputesAverageUsingDecimalPrecision()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "viewer@wiserecruiter.local");

            await service.AddResponsesAsync(
                scorecard.Id,
                new[]
                {
                    new ScorecardResponse { FacetName = "A", Score = 3.7m, Notes = string.Empty },
                    new ScorecardResponse { FacetName = "B", Score = 4.1m, Notes = string.Empty },
                    new ScorecardResponse { FacetName = "C", Score = 2.8m, Notes = string.Empty }
                });

            var detail = await service.GetScorecardById(scorecard.Id);

            detail.Should().NotBeNull();
            detail!.AverageScore.Should().Be((3.7m + 4.1m + 2.8m) / 3m);
        }

        [Fact]
        public async Task GetScorecardById_WithInvalidId_ReturnsNull()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardService(context);

            var detail = await service.GetScorecardById(9999);

            detail.Should().BeNull();
        }

        [Fact]
        public async Task GetScorecardById_WithoutResponses_ReturnsSafeDefaultAverage()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "viewer@wiserecruiter.local");

            var detail = await service.GetScorecardById(scorecard.Id);

            detail.Should().NotBeNull();
            detail!.Responses.Should().BeEmpty();
            detail.AverageScore.Should().Be(0m);
        }

        [Fact]
        public async Task UpdateScorecard_WithValidResponses_UpdatesScorecardWithNewResponses()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "editor@wiserecruiter.local");

            await service.AddResponsesAsync(
                scorecard.Id,
                new[]
                {
                    new ScorecardResponse { FacetName = "Communication", Score = 3.1m, Notes = "Original" },
                    new ScorecardResponse { FacetName = "Quality", Score = 3.4m, Notes = "Original" }
                });

            await service.UpdateScorecard(
                scorecard.Id,
                new List<ScorecardDetailDto.ScorecardResponseDto>
                {
                    new() { FacetName = "Communication", Score = 4.5m, Notes = "Updated" },
                    new() { FacetName = "Problem Solving", Score = 4.2m, Notes = "New facet" }
                });

            var detail = await service.GetScorecardById(scorecard.Id);

            detail.Should().NotBeNull();
            detail!.Responses.Should().HaveCount(2);
            detail.Responses.Select(r => r.FacetName).Should().Equal("Communication", "Problem Solving");
            detail.Responses.Select(r => r.Score).Should().Equal(4.5m, 4.2m);
            detail.Responses.Select(r => r.Notes).Should().Equal("Updated", "New facet");
        }

        [Fact]
        public async Task UpdateScorecard_FullyReplacesOldResponses()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "editor@wiserecruiter.local");

            var originalResponses = await service.AddResponsesAsync(
                scorecard.Id,
                new[]
                {
                    new ScorecardResponse { FacetName = "Communication", Score = 3.1m, Notes = "Original" },
                    new ScorecardResponse { FacetName = "Quality", Score = 3.4m, Notes = "Original" }
                });

            var originalIds = originalResponses.Select(response => response.Id).ToList();

            await service.UpdateScorecard(
                scorecard.Id,
                new List<ScorecardDetailDto.ScorecardResponseDto>
                {
                    new() { FacetName = "Speed", Score = 4.0m, Notes = "Replacement" }
                });

            var persistedResponses = await context.ScorecardResponses
                .Where(response => response.ScorecardId == scorecard.Id)
                .ToListAsync();

            persistedResponses.Should().HaveCount(1);
            persistedResponses.Single().FacetName.Should().Be("Speed");
            persistedResponses.Select(response => response.Id).Should().NotContain(originalIds);
        }

        [Fact]
        public async Task UpdateScorecard_WithInvalidScorecardId_ThrowsControlledException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardService(context);

            Func<Task> act = async () => await service.UpdateScorecard(
                9999,
                new List<ScorecardDetailDto.ScorecardResponseDto>
                {
                    new() { FacetName = "Communication", Score = 4.0m, Notes = "N/A" }
                });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateScorecard_WithInvalidScoreValues_RejectsUpdate()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "editor@wiserecruiter.local");

            Func<Task> act = async () => await service.UpdateScorecard(
                scorecard.Id,
                new List<ScorecardDetailDto.ScorecardResponseDto>
                {
                    new() { FacetName = "Communication", Score = 5.1m, Notes = "Invalid" }
                });

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task UpdateScorecard_PreservesDecimalPrecision()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "editor@wiserecruiter.local");

            await service.UpdateScorecard(
                scorecard.Id,
                new List<ScorecardDetailDto.ScorecardResponseDto>
                {
                    new() { FacetName = "Problem Solving", Score = 3.7m, Notes = "Precise" }
                });

            var detail = await service.GetScorecardById(scorecard.Id);

            detail.Should().NotBeNull();
            detail!.Responses.Single().Score.Should().Be(3.7m);
        }

        [Fact]
        public async Task UpdateScorecard_WithEmptyResponseList_RemovesAllResponses()
        {
            using var context = CreateInMemoryContext();
            var candidateId = await SeedCandidateAsync(context);
            var service = new ScorecardService(context);
            var scorecard = await service.CreateScorecardAsync(candidateId, "editor@wiserecruiter.local");

            await service.AddResponsesAsync(
                scorecard.Id,
                new[]
                {
                    new ScorecardResponse { FacetName = "Communication", Score = 3.1m, Notes = "Original" }
                });

            await service.UpdateScorecard(scorecard.Id, new List<ScorecardDetailDto.ScorecardResponseDto>());

            var detail = await service.GetScorecardById(scorecard.Id);

            detail.Should().NotBeNull();
            detail!.Responses.Should().BeEmpty();
            detail.AverageScore.Should().Be(0m);
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_UsesDefaultTemplateFacets()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Communication", 1), ("Quality", 2));
            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesFromTemplate();

            responses.Should().HaveCount(2);
            responses.Select(r => r.FacetName).Should().Equal("Communication", "Quality");
            responses.Select(r => r.Score).Should().OnlyContain(score => score == 3.0m);
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_RespectsTemplateOrdering()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Problem Solving", 2), ("Communication", 1));
            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesFromTemplate();

            responses.Select(r => r.FacetName).Should().Equal("Communication", "Problem Solving");
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_WithNonSequentialDisplayOrder_ReturnsFacetsAlphabetically()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Quality", 3), ("Communication", 1), ("Problem Solving", 7));
            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesFromTemplate();

            // Ordering is now alphabetical by facet name (DisplayOrder is no longer used for sorting)
            responses.Select(r => r.FacetName).Should().Equal("Communication", "Problem Solving", "Quality");
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_WithDuplicateTemplateDisplayOrder_StillReturnsResponses()
        {
            using var context = CreateInMemoryContext();
            var templateId = await SeedDefaultTemplateWithFacetsAsync(context, ("Communication", 1), ("Quality", 2));
            // Manually add a duplicate DisplayOrder — no longer causes a throw since ordering is by name
            var qualityFacet = await context.Facets.SingleAsync(f => f.Name == "Quality");
            context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
            {
                ScorecardTemplateId = templateId,
                FacetId = qualityFacet.Id,
                ScorecardFacetId = qualityFacet.Id
            });
            await context.SaveChangesAsync();

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            // Duplicate DisplayOrder no longer throws; facets are ordered by name
            var responses = await service.CreateDefaultResponsesFromTemplate();

            responses.Should().NotBeEmpty();
            responses.Select(r => r.FacetName).Should().Contain("Communication");
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_WithTemplateContainingNoFacets_ReturnsEmptyList()
        {
            using var context = CreateInMemoryContext();
            context.ScorecardTemplates.Add(new ScorecardTemplate { Name = "Default Scorecard" });
            await context.SaveChangesAsync();

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesFromTemplate();

            responses.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateDefaultResponsesForApplication_UsesJobAssignedTemplate()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Communication", 1));
            var assignedTemplateId = await SeedTemplateWithFacetsAsync(context, "Technical", ("System Design", 1), ("Depth", 2));
            var candidateId = await SeedCandidateAsync(context);
            var applicationId = await SeedApplicationForCandidateAsync(context, candidateId, assignedTemplateId);

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesForApplication(applicationId);

            // Ordered alphabetically: "Technical-Depth" < "Technical-System Design"
            responses.Select(r => r.FacetName).Should().Equal("Technical-Depth", "Technical-System Design");
        }

        [Fact]
        public async Task CreateDefaultResponsesForApplication_WithoutAssignedTemplate_FallsBackToDefaultTemplate()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Communication", 1), ("Quality", 2));
            await SeedTemplateWithFacetsAsync(context, "Technical", ("System Design", 1));
            var candidateId = await SeedCandidateAsync(context);
            var applicationId = await SeedApplicationForCandidateAsync(context, candidateId, null);

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesForApplication(applicationId);

            responses.Select(r => r.FacetName).Should().Equal("Communication", "Quality");
        }

        [Fact]
        public async Task CreateDefaultResponsesForApplication_ReturnsAlphabeticalFacetOrder()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Communication", 1));
            var assignedTemplateId = await SeedTemplateWithFacetsAsync(context, "Technical", ("Depth", 7), ("System Design", 1), ("Execution", 3));
            var candidateId = await SeedCandidateAsync(context);
            var applicationId = await SeedApplicationForCandidateAsync(context, candidateId, assignedTemplateId);

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesForApplication(applicationId);

            // Ordered alphabetically: "Technical-Depth" < "Technical-Execution" < "Technical-System Design"
            responses.Select(r => r.FacetName).Should().Equal("Technical-Depth", "Technical-Execution", "Technical-System Design");
        }

        [Fact]
        public async Task CreateDefaultResponsesForApplication_WhenAssignedTemplateHasNoFacets_ReturnsEmptySafely()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Communication", 1));
            var emptyTemplateId = await SeedTemplateWithFacetsAsync(context, "Empty Template");
            var candidateId = await SeedCandidateAsync(context);
            var applicationId = await SeedApplicationForCandidateAsync(context, candidateId, emptyTemplateId);

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesForApplication(applicationId);

            responses.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_UsesTemplateNotActiveFacetList()
        {
            using var context = CreateInMemoryContext();
            await SeedDefaultTemplateWithFacetsAsync(context, ("Template Facet", 1));
            context.Facets.Add(new Facet
            {
                Name = "Active But Not In Template"
            });
            await context.SaveChangesAsync();

            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            var responses = await service.CreateDefaultResponsesFromTemplate();

            responses.Should().HaveCount(1);
            responses.Single().FacetName.Should().Be("Template Facet");
        }

        [Fact]
        public async Task CreateDefaultResponsesFromTemplate_ThrowsWhenDefaultTemplateMissing()
        {
            using var context = CreateInMemoryContext();
            var templateService = new ScorecardTemplateService(context);
            var service = new ScorecardService(context, templateService);

            Func<Task> act = async () => await service.CreateDefaultResponsesFromTemplate();

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task ScorecardService_EmptyData_RetrieveOperationsDoNotThrow()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardService(context);

            var byCandidate = await service.GetScorecardsByCandidateAsync(12345);
            var detail = await service.GetScorecardWithResponsesAsync(67890);

            byCandidate.Should().BeEmpty();
            detail.Should().BeNull();
        }
    }
}
