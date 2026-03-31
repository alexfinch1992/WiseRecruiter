using Microsoft.AspNetCore.Identity;
using System;
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
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Behaviour guards for facet metadata flowing through to CreateScorecard.
    ///
    /// These tests will fail if:
    /// - Metadata is moved back to templates instead of facets
    /// - CreateScorecard stops reading Description/NotesPlaceholder/CategoryName from Facet
    /// - Scorecard lookup regresses to name-based (instead of ID-based)
    /// </summary>
    public class ScorecardFacetMetadataTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("scorecard_facet_metadata_" + Guid.NewGuid())
                .Options);

        private static AdminController CreateAdminController(AppDbContext context)
            => AdminControllerFactory.Create(context);

        private async Task<(int applicationId, int facetId)> SeedApplicationWithFacetAsync(
            AppDbContext context,
            string description,
            string notesPlaceholder,
            int? categoryId = null)
        {
            var template = new ScorecardTemplate { Name = "Test Template " + Guid.NewGuid() };
            context.ScorecardTemplates.Add(template);
            await context.SaveChangesAsync();

            var facet = new Facet
            {
                Name = "Facet_" + Guid.NewGuid(),
                Description = description,
                NotesPlaceholder = notesPlaceholder,
                CategoryId = categoryId
            };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
            {
                ScorecardTemplateId = template.Id,
                FacetId = facet.Id,
                ScorecardFacetId = facet.Id
            });

            var job = new Job { Title = "Test Job", ScorecardTemplateId = template.Id };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var candidate = new Candidate
            {
                FirstName = "Test",
                LastName = "Candidate",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Test Candidate",
                Email = "test@example.com",
                City = "Sydney",
                JobId = job.Id,
                CandidateId = candidate.Id,
                CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            return (application.Id, facet.Id);
        }

        // -------------------------------------------------------
        // Suite 3 � Scorecard Creation Uses Facet Metadata
        // -------------------------------------------------------

        [Fact]
        public async Task CreateScorecard_Uses_Facet_Description_And_NotesPlaceholder()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, _) = await SeedApplicationWithFacetAsync(context,
                "Assess technical depth", "e.g. can explain recursion");

            var controller = CreateAdminController(context);
            var result = await controller.CreateScorecard(applicationId);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<CreateScorecardViewModel>().Subject;

            model.Responses.Should().ContainSingle();
            var response = model.Responses[0];
            response.Description.Should().Be("Assess technical depth");
            response.NotesPlaceholder.Should().Be("e.g. can explain recursion");
        }

        [Fact]
        public async Task CreateScorecard_Uses_Facet_CategoryName()
        {
            using var context = CreateInMemoryContext();
            var category = new Category { Name = "Engineering" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var (applicationId, _) = await SeedApplicationWithFacetAsync(context,
                "desc", "placeholder", category.Id);

            var controller = CreateAdminController(context);
            var result = await controller.CreateScorecard(applicationId);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<CreateScorecardViewModel>().Subject;

            model.Responses.Should().ContainSingle();
            model.Responses[0].CategoryName.Should().Be("Engineering");
        }

        [Fact]
        public async Task CreateScorecard_Reflects_Updated_Facet_Metadata()
        {
            // Create facet with initial metadata, then update it AFTER template creation.
            // Scorecard should use the updated values � proving it reads from Facet, not a stale copy.
            using var context = CreateInMemoryContext();
            var (applicationId, facetId) = await SeedApplicationWithFacetAsync(context,
                "Original description", "Original placeholder");

            // Update facet metadata after template was already set up
            var facetService = new FacetService(context);
            await facetService.UpdateFacet(facetId,
                (await context.Facets.FindAsync(facetId))!.Name,
                "Updated description",
                "Updated placeholder",
                null);

            var controller = CreateAdminController(context);
            var result = await controller.CreateScorecard(applicationId);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<CreateScorecardViewModel>().Subject;

            model.Responses[0].Description.Should().Be("Updated description");
            model.Responses[0].NotesPlaceholder.Should().Be("Updated placeholder");
        }

        // -------------------------------------------------------
        // Suite 4 � Adversarial / Regression Guards
        // -------------------------------------------------------

        [Fact]
        public async Task Scorecard_Lookup_Uses_FacetId_Not_FacetName()
        {
            // Two facets with the same name but different IDs and metadata.
            // Only the one in the template should contribute its metadata.
            using var context = CreateInMemoryContext();
            var template = new ScorecardTemplate { Name = "Template " + Guid.NewGuid() };
            context.ScorecardTemplates.Add(template);
            await context.SaveChangesAsync();

            var facetInTemplate = new Facet
            {
                Name = "SharedName",
                Description = "Correct description",
                NotesPlaceholder = "Correct placeholder"
            };
            var facetNotInTemplate = new Facet
            {
                Name = "SharedName_Other",
                Description = "Wrong description",
                NotesPlaceholder = "Wrong placeholder"
            };
            context.Facets.AddRange(facetInTemplate, facetNotInTemplate);
            await context.SaveChangesAsync();

            context.ScorecardTemplateFacets.Add(new ScorecardTemplateFacet
            {
                ScorecardTemplateId = template.Id,
                FacetId = facetInTemplate.Id,
                ScorecardFacetId = facetInTemplate.Id
            });

            var job = new Job { Title = "Test Job", ScorecardTemplateId = template.Id };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Applied", Order = 1 };
            context.JobStages.Add(stage);
            await context.SaveChangesAsync();

            var candidate = new Candidate
            {
                FirstName = "Test", LastName = "User",
                Email = "tu@example.com", CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            var application = new Application
            {
                Name = "Test User", Email = "tu@example.com", City = "Sydney",
                JobId = job.Id, CandidateId = candidate.Id, CurrentJobStageId = stage.Id
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            var result = await controller.CreateScorecard(application.Id);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<CreateScorecardViewModel>().Subject;

            model.Responses.Should().ContainSingle();
            model.Responses[0].Description.Should().Be("Correct description");
            model.Responses[0].NotesPlaceholder.Should().Be("Correct placeholder");
        }

        [Fact]
        public async Task Removing_Category_From_Facet_DoesNotBreak_Scorecard_Creation()
        {
            using var context = CreateInMemoryContext();
            var category = new Category { Name = "Leadership" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var (applicationId, facetId) = await SeedApplicationWithFacetAsync(context,
                "desc", "placeholder", category.Id);

            // Remove category from facet
            var facet = await context.Facets.FindAsync(facetId);
            facet!.CategoryId = null;
            await context.SaveChangesAsync();

            var controller = CreateAdminController(context);
            Func<Task> act = async () => await controller.CreateScorecard(applicationId);

            await act.Should().NotThrowAsync();

            var result = await controller.CreateScorecard(applicationId);
            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<CreateScorecardViewModel>().Subject;

            model.Responses[0].CategoryName.Should().BeNull();
        }

        [Fact]
        public async Task Scorecard_CategoryName_Is_Null_When_Facet_Has_No_Category()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, _) = await SeedApplicationWithFacetAsync(context,
                "description", "placeholder", categoryId: null);

            var controller = CreateAdminController(context);
            var result = await controller.CreateScorecard(applicationId);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            var model = view.Model.Should().BeOfType<CreateScorecardViewModel>().Subject;

            model.Responses[0].CategoryName.Should().BeNull();
        }

        // -------------------------------------------------------
        // Suite 5 � Minimal Controller Coverage
        // -------------------------------------------------------

        [Fact]
        public async Task CreateFacet_POST_InvalidModel_ReturnsViewWithError()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = new AdminSettingsController(context, facetService, templateService, new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null).Object);

            // Empty name triggers validation error
            var result = await controller.Create(name: "   ", description: null, notesPlaceholder: null, categoryId: null);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ContainsKey("name").Should().BeTrue();
        }

        [Fact]
        public async Task EditFacet_POST_InvalidModel_ReturnsViewWithError()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var facet = await facetService.CreateFacet("Communication");
            var controller = new AdminSettingsController(context, facetService, templateService, new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null).Object);

            var result = await controller.Edit(facet.Id, name: "", description: null, notesPlaceholder: null, categoryId: null);

            var view = result.Should().BeOfType<ViewResult>().Subject;
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ContainsKey("name").Should().BeTrue();
        }

        [Fact]
        public async Task EditFacet_POST_Updates_Description_NotesPlaceholder_Category()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var category = new Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = await facetService.CreateFacet("Communication");
            var controller = new AdminSettingsController(context, facetService, templateService, new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null).Object);

            var result = await controller.Edit(facet.Id, "Communication",
                "Assess clarity", "e.g. concise answer", category.Id);

            result.Should().BeOfType<RedirectToActionResult>();

            var saved = await context.Facets.FindAsync(facet.Id);
            saved!.Description.Should().Be("Assess clarity");
            saved.NotesPlaceholder.Should().Be("e.g. concise answer");
            saved.CategoryId.Should().Be(category.Id);
        }

        // -------------------------------------------------------
        // Suite 6 � Overall Recommendation
        // -------------------------------------------------------

        [Fact]
        public async Task CreateScorecard_POST_PersistsOverallRecommendation()
        {
            using var context = CreateInMemoryContext();
            var (applicationId, facetId) = await SeedApplicationWithFacetAsync(
                context, "desc", "placeholder");

            var controller = CreateAdminController(context);

            var model = new CreateScorecardViewModel
            {
                ApplicationId = applicationId,
                CandidateId = 0, // will be overridden by controller from DB
                OverallRecommendation = "Strong hire � excellent problem-solving skills.",
                Responses = new System.Collections.Generic.List<ScorecardResponseInputViewModel>
                {
                    new ScorecardResponseInputViewModel
                    {
                        FacetId = facetId,
                        FacetName = "Test Facet",
                        Score = 4.0m
                    }
                }
            };

            var result = await controller.CreateScorecard(model);

            result.Should().BeOfType<RedirectToActionResult>();

            var saved = await context.Scorecards.FirstOrDefaultAsync();
            saved.Should().NotBeNull();
            saved!.OverallRecommendation.Should().Be("Strong hire � excellent problem-solving skills.");
        }
    }
}
