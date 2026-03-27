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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Adversarial / regression-guard tests for facet metadata ownership.
    ///
    /// Each test targets a specific failure mode that would still pass naive happy-path tests:
    ///   - ViewModel drift from DB
    ///   - Name-based lookup surviving a refactor back
    ///   - Stale template-snapshot being used instead of live Facet
    ///   - Silent metadata collisions when two facets share a name
    ///   - Metadata loss for null / partial field combinations
    ///   - Truncation of long strings at any layer
    ///   - Silent FK ignore for invalid CategoryId
    ///   - Missing ViewModel bindings caught before they reach the view
    /// </summary>
    public class FacetMetadataAdversarialTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("facet_adversarial_" + Guid.NewGuid())
                .Options);

        private AdminController CreateAdminController(AppDbContext context)
        {
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            IApplicationService applicationService = new ApplicationService(context);
            IAnalyticsService analyticsService = new AnalyticsService(context);
            IScorecardService scorecardService = new ScorecardService(context, templateService);
            IJobService jobService = new JobService(context);
            return new AdminController(context, new Mock<IWebHostEnvironment>().Object,
                applicationService, analyticsService, scorecardService, templateService, jobService, new ScorecardAnalyticsService(context), new InterviewService(context));
        }

        /// Builds the minimum application + template + facet chain needed to call CreateScorecard.
        /// Returns (applicationId, facetId).
        private async Task<(int applicationId, int facetId)> SeedSingleFacetApplicationAsync(
            AppDbContext context,
            Facet facet)
        {
            var template = new ScorecardTemplate { Name = "Tmpl_" + Guid.NewGuid() };
            context.ScorecardTemplates.Add(template);
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
            {
                ScorecardTemplateId = template.Id,
                FacetId = facet.Id,
                ScorecardFacetId = facet.Id
            });

            return (await SeedApplicationForTemplateAsync(context, template.Id), facet.Id);
        }

        private async Task<int> SeedApplicationForTemplateAsync(AppDbContext context, int templateId)
        {
            var job = new Job { Title = "Job_" + Guid.NewGuid(), ScorecardTemplateId = templateId };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var candidate = new Candidate
            {
                FirstName = "A", LastName = "B",
                Email = Guid.NewGuid() + "@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "A B", Email = candidate.Email, City = "Sydney",
                JobId = job.Id, CandidateId = candidate.Id, CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return application.Id;
        }

        private async Task<CreateScorecardViewModel> GetScorecardViewModelAsync(
            AppDbContext context, int applicationId)
        {
            var controller = CreateAdminController(context);
            var result = await controller.CreateScorecard(applicationId);
            return result.Should().BeOfType<ViewResult>().Subject
                .Model.Should().BeOfType<CreateScorecardViewModel>().Subject;
        }

        // -------------------------------------------------------
        // Task 1 — Cross-Layer Integrity: ViewModel must mirror DB
        // -------------------------------------------------------

        [Fact]
        public async Task Task1_ViewModel_Description_ExactlyEquals_DB_Value()
        {
            using var context = CreateInMemoryContext();
            var facet = new Facet { Name = "F1", Description = "DESC_A", NotesPlaceholder = "NOTE_A" };
            var (appId, facetId) = await SeedSingleFacetApplicationAsync(context, facet);

            var model = await GetScorecardViewModelAsync(context, appId);

            var dbFacet = await context.Facets.FirstAsync(f => f.Id == facetId);
            model.Responses.Should().ContainSingle();
            model.Responses[0].Description.Should().Be(dbFacet.Description);
        }

        [Fact]
        public async Task Task1_ViewModel_NotesPlaceholder_ExactlyEquals_DB_Value()
        {
            using var context = CreateInMemoryContext();
            var facet = new Facet { Name = "F2", Description = "DESC_A", NotesPlaceholder = "NOTE_A" };
            var (appId, facetId) = await SeedSingleFacetApplicationAsync(context, facet);

            var model = await GetScorecardViewModelAsync(context, appId);

            var dbFacet = await context.Facets.FirstAsync(f => f.Id == facetId);
            model.Responses[0].NotesPlaceholder.Should().Be(dbFacet.NotesPlaceholder);
        }

        [Fact]
        public async Task Task1_ViewModel_CategoryName_ExactlyEquals_DB_CategoryName()
        {
            using var context = CreateInMemoryContext();
            var category = new Category { Name = "Engineering" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = new Facet { Name = "F3", Description = "D", NotesPlaceholder = "N", CategoryId = category.Id };
            var (appId, facetId) = await SeedSingleFacetApplicationAsync(context, facet);

            var model = await GetScorecardViewModelAsync(context, appId);

            var dbFacet = await context.Facets.Include(f => f.Category).FirstAsync(f => f.Id == facetId);
            model.Responses[0].CategoryName.Should().Be(dbFacet.Category!.Name);
        }

        // -------------------------------------------------------
        // Task 2 — Two Facets With Same Name, Both In Template
        //          Proves ID-based lookup, not name-based
        // -------------------------------------------------------

        [Fact]
        public async Task Task2_TwoFacetsWithSameName_BothInTemplate_EachRetainsDistinctMetadata()
        {
            // Bypass service uniqueness check to simulate data that would expose a name-based
            // lookup bug — or to guard against any future relaxation of uniqueness.
            using var context = CreateInMemoryContext();
            var template = new ScorecardTemplate { Name = "DuplicateNameTemplate" };
            context.ScorecardTemplates.Add(template);

            var facetA = new Facet { Name = "SharedName", Description = "MetadataA", NotesPlaceholder = "NotesA" };
            var facetB = new Facet { Name = "SharedName", Description = "MetadataB", NotesPlaceholder = "NotesB" };
            context.Facets.AddRange(facetA, facetB);
            await context.SaveChangesAsync();

            context.ScorecardTemplateFacets.AddRange(
                new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facetA.Id, ScorecardFacetId = facetA.Id },
                new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facetB.Id, ScorecardFacetId = facetB.Id }
            );
            await context.SaveChangesAsync();

            var appId = await SeedApplicationForTemplateAsync(context, template.Id);
            var model = await GetScorecardViewModelAsync(context, appId);

            model.Responses.Should().HaveCount(2);

            var responseA = model.Responses.Single(r => r.FacetId == facetA.Id);
            var responseB = model.Responses.Single(r => r.FacetId == facetB.Id);

            responseA.Description.Should().Be("MetadataA");
            responseA.NotesPlaceholder.Should().Be("NotesA");
            responseB.Description.Should().Be("MetadataB");
            responseB.NotesPlaceholder.Should().Be("NotesB");

            // Neither response should carry the other's metadata
            responseA.Description.Should().NotBe("MetadataB");
            responseB.Description.Should().NotBe("MetadataA");
        }

        // -------------------------------------------------------
        // Task 3 — Multiple Sequential Updates: Last Write Wins
        //          Guards against any template-level snapshot caching
        // -------------------------------------------------------

        [Fact]
        public async Task Task3_CreateScorecard_AlwaysUses_FinalFacetState_AfterMultipleUpdates()
        {
            using var context = CreateInMemoryContext();
            var facet = new Facet { Name = "Volatile", Description = "V1" };
            var (appId, facetId) = await SeedSingleFacetApplicationAsync(context, facet);

            var service = new FacetService(context);
            await service.UpdateFacet(facetId, "Volatile", "V2", "N2", null);
            await service.UpdateFacet(facetId, "Volatile", "V3", "N3", null);
            await service.UpdateFacet(facetId, "Volatile", "FINAL_DESC", "FINAL_NOTES", null);

            var model = await GetScorecardViewModelAsync(context, appId);

            model.Responses[0].Description.Should().Be("FINAL_DESC");
            model.Responses[0].NotesPlaceholder.Should().Be("FINAL_NOTES");
            // Explicitly reject stale values from any earlier update
            model.Responses[0].Description.Should().NotBe("V1");
            model.Responses[0].Description.Should().NotBe("V2");
            model.Responses[0].Description.Should().NotBe("V3");
        }

        // -------------------------------------------------------
        // Task 4 — Null and Partial Field Combinations
        //          All three cases in one scorecard
        // -------------------------------------------------------

        [Fact]
        public async Task Task4_NullAndPartialMetadataCombinations_AllPropagateCorrectly()
        {
            using var context = CreateInMemoryContext();
            var template = new ScorecardTemplate { Name = "NullPartialTemplate" };
            context.ScorecardTemplates.Add(template);

            // Case A: Description=null, NotesPlaceholder present
            var facetA = new Facet { Name = "CaseA", Description = null, NotesPlaceholder = "NOTE_ONLY" };
            // Case B: Description present, NotesPlaceholder=null
            var facetB = new Facet { Name = "CaseB", Description = "DESC_ONLY", NotesPlaceholder = null };
            // Case C: Both null
            var facetC = new Facet { Name = "CaseC", Description = null, NotesPlaceholder = null };

            context.Facets.AddRange(facetA, facetB, facetC);
            await context.SaveChangesAsync();

            context.ScorecardTemplateFacets.AddRange(
                new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facetA.Id, ScorecardFacetId = facetA.Id },
                new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facetB.Id, ScorecardFacetId = facetB.Id },
                new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facetC.Id, ScorecardFacetId = facetC.Id }
            );
            await context.SaveChangesAsync();

            var appId = await SeedApplicationForTemplateAsync(context, template.Id);

            Func<Task> act = async () => await GetScorecardViewModelAsync(context, appId);
            await act.Should().NotThrowAsync("all null/partial combinations must be handled without exceptions");

            var model = await GetScorecardViewModelAsync(context, appId);
            model.Responses.Should().HaveCount(3);

            var rA = model.Responses.Single(r => r.FacetId == facetA.Id);
            var rB = model.Responses.Single(r => r.FacetId == facetB.Id);
            var rC = model.Responses.Single(r => r.FacetId == facetC.Id);

            // Case A
            rA.Description.Should().BeNull("Description was null on the facet");
            rA.NotesPlaceholder.Should().Be("NOTE_ONLY");

            // Case B
            rB.Description.Should().Be("DESC_ONLY");
            rB.NotesPlaceholder.Should().BeNull("NotesPlaceholder was null on the facet");

            // Case C
            rC.Description.Should().BeNull();
            rC.NotesPlaceholder.Should().BeNull();
            rC.CategoryName.Should().BeNull();
        }

        // -------------------------------------------------------
        // Task 5 — Long String Stress: No Truncation At Any Layer
        // -------------------------------------------------------

        [Fact]
        public async Task Task5_LongDescriptionAndNotesPlaceholder_PreservedExactly_NoTruncation()
        {
            using var context = CreateInMemoryContext();

            var longDescription = "D" + new string('x', 499);   // 500 chars
            var longNotes = "N" + new string('y', 299);          // 300 chars

            longDescription.Length.Should().Be(500);
            longNotes.Length.Should().Be(300);

            var facet = new Facet { Name = "LongStrings", Description = longDescription, NotesPlaceholder = longNotes };
            var (appId, _) = await SeedSingleFacetApplicationAsync(context, facet);

            var model = await GetScorecardViewModelAsync(context, appId);

            model.Responses[0].Description.Should().Be(longDescription,
                "no layer should truncate the Description");
            model.Responses[0].Description!.Length.Should().Be(500);

            model.Responses[0].NotesPlaceholder.Should().Be(longNotes,
                "no layer should truncate the NotesPlaceholder");
            model.Responses[0].NotesPlaceholder!.Length.Should().Be(300);
        }

        // -------------------------------------------------------
        // Task 6 — Invalid CategoryId: Documents Current Behavior
        //
        // EF InMemory does NOT enforce FK constraints.
        // UpdateFacet does NOT validate categoryId existence.
        // Current behavior: silently persists the invalid FK value.
        //
        // This test documents that behavior — if the service adds
        // validation in future, this test will need to be updated
        // and the behavior change will be explicit.
        // -------------------------------------------------------

        [Fact]
        public async Task Task6_UpdateFacet_WithNonExistentCategoryId_InMemory_SilentlyPersistsValue()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var facet = await service.CreateFacet("Uncategorised");
            const int nonExistentCategoryId = 99999;

            // With InMemory DB: no FK enforcement, no service-level validation
            // → silently succeeds, CategoryId is set to the non-existent value
            Func<Task> act = async () =>
                await service.UpdateFacet(facet.Id, facet.Name, "d", "n", nonExistentCategoryId);

            await act.Should().NotThrowAsync(
                "InMemory DB does not enforce FK constraints and UpdateFacet has no categoryId existence check");

            var loaded = await context.Facets.FirstAsync(f => f.Id == facet.Id);
            loaded.CategoryId.Should().Be(nonExistentCategoryId,
                "the value is accepted silently — Category navigation property will be null");
            loaded.Category.Should().BeNull("EF cannot resolve a navigation property with a non-existent FK");
        }

        // -------------------------------------------------------
        // Task 7 — All Four ViewModel Binding Fields Must Be Present
        //          Protects against anyone removing a property from
        //          ScorecardResponseInputViewModel or the controller mapping
        // -------------------------------------------------------

        [Fact]
        public async Task Task7_ViewModel_AllFacetMetadataFields_BoundCorrectly()
        {
            using var context = CreateInMemoryContext();
            var category = new Category { Name = "Soft Skills" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = new Facet
            {
                Name = "FullyPopulated",
                Description = "Complete description",
                NotesPlaceholder = "Complete placeholder",
                CategoryId = category.Id
            };
            var (appId, facetId) = await SeedSingleFacetApplicationAsync(context, facet);

            var model = await GetScorecardViewModelAsync(context, appId);

            model.Responses.Should().ContainSingle();
            var r = model.Responses[0];

            // FacetId binding — ID-based lookup machinery
            r.FacetId.Should().Be(facetId,
                "FacetId must be bound so rendering and POST can identify which facet each row belongs to");

            // FacetName binding — basic display
            r.FacetName.Should().Be("FullyPopulated");

            // Description binding — sourced from Facet, not template
            r.Description.Should().Be("Complete description");

            // NotesPlaceholder binding — sourced from Facet, not template
            r.NotesPlaceholder.Should().Be("Complete placeholder");

            // CategoryName binding — resolved from Facet.Category.Name, not stored directly
            r.CategoryName.Should().Be("Soft Skills");
        }

        // -------------------------------------------------------
        // Extra: FacetId Is Populated Even When Metadata Is Null
        //        Guards against FacetId being omitted when there's
        //        nothing else to set on the ViewModel row
        // -------------------------------------------------------

        [Fact]
        public async Task Task7_FacetId_IsAlwaysBound_EvenWhenAllMetadataIsNull()
        {
            using var context = CreateInMemoryContext();
            var facet = new Facet { Name = "MinimalFacet" };
            var (appId, facetId) = await SeedSingleFacetApplicationAsync(context, facet);

            var model = await GetScorecardViewModelAsync(context, appId);

            model.Responses.Should().ContainSingle();
            model.Responses[0].FacetId.Should().Be(facetId,
                "FacetId must always be bound regardless of whether metadata fields are null");
        }
    }
}
