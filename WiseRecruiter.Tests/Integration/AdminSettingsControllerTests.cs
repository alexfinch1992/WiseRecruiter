using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class AdminSettingsControllerTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "admin_settings_controller_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + System.Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public void Create_Get_ReturnsViewWithoutException()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = new AdminSettingsController(context, facetService, templateService);

            Action act = () =>
            {
                var result = controller.Create();
                result.Should().BeOfType<ViewResult>();
            };

            act.Should().NotThrow();
        }

        [Fact]
        public async Task Create_Post_WithDuplicateDisplayOrder_AddsModelStateErrorAndReturnsView()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            await facetService.CreateFacet("Communication", 1);

            var controller = new AdminSettingsController(context, facetService, templateService);

            var result = await controller.Create("Quality", 1);

            result.Should().BeOfType<ViewResult>();
            controller.ModelState.ContainsKey("displayOrder").Should().BeTrue();
            controller.ModelState["displayOrder"]!.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Edit_Post_WithDuplicateDisplayOrder_AddsModelStateErrorAndReturnsView()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var first = await facetService.CreateFacet("Communication", 1);
            var second = await facetService.CreateFacet("Quality", 2);

            var controller = new AdminSettingsController(context, facetService, templateService);

            var result = await controller.Edit(second.Id, second.Name, first.DisplayOrder, second.IsActive);

            result.Should().BeOfType<ViewResult>();
            controller.ModelState.ContainsKey("displayOrder").Should().BeTrue();
            controller.ModelState["displayOrder"]!.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Index_Get_PopulatesTemplateNamesByFacetIdMapping()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);

            var communication = await facetService.CreateFacet("Communication", 1);
            var quality = await facetService.CreateFacet("Quality", 2);
            var technicalTemplate = await templateService.CreateTemplate("Technical");
            var defaultTemplate = await templateService.CreateTemplate("Default Scorecard");

            await templateService.AddFacetToTemplate(defaultTemplate.Id, communication.Id, 1);
            await templateService.AddFacetToTemplate(defaultTemplate.Id, quality.Id, 2);
            await templateService.AddFacetToTemplate(technicalTemplate.Id, quality.Id, 1);

            var controller = new AdminSettingsController(context, facetService, templateService);

            var result = await controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<ScorecardFacet>>().Subject;
            model.Should().HaveCount(2);

            var mapping = controller.ViewBag.TemplateNamesByFacetId as Dictionary<int, List<string>>;
            mapping.Should().NotBeNull();
            mapping![communication.Id].Should().ContainSingle().Which.Should().Be("Default Scorecard");
            mapping[quality.Id].Should().BeEquivalentTo(new[] { "Default Scorecard", "Technical" });
        }

        [Fact]
        public async Task EditTemplateFacets_Post_WithZeroFacets_AddsErrorAndDoesNotClearExistingFacets()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);

            var facet = await facetService.CreateFacet("Communication", 1);
            var template = await templateService.CreateTemplate("Hiring Template");
            await templateService.AddFacetToTemplate(template.Id, facet.Id, 1);

            var controller = new AdminSettingsController(context, facetService, templateService);

            var result = await controller.EditTemplateFacets(template.Id, new List<TemplateFacetInput>());

            result.Should().BeOfType<ViewResult>();
            controller.ModelState[string.Empty]!.Errors.Should().Contain(error => error.ErrorMessage == "A scorecard template must have at least one facet.");

            var facetsAfterAttempt = await templateService.GetFacetsForTemplate(template.Id);
            facetsAfterAttempt.Should().HaveCount(1);
            facetsAfterAttempt[0].ScorecardFacetId.Should().Be(facet.Id);
        }

        [Fact]
        public async Task EditTemplateFacets_Post_WithExtendedFields_PersistsDescriptionNotesAndCategory()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);

            var facet = await facetService.CreateFacet("Technical Skill", 1);
            var template = await templateService.CreateTemplate("Engineering");
            var category = new JobPortal.Models.Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var controller = new AdminSettingsController(context, facetService, templateService);

            var result = await controller.EditTemplateFacets(template.Id, new List<TemplateFacetInput>
            {
                new TemplateFacetInput
                {
                    FacetId = facet.Id,
                    DisplayOrder = 1,
                    Description = "Rate the candidate's technical depth.",
                    NotesPlaceholder = "e.g. Solved the system design challenge well",
                    CategoryId = category.Id
                }
            });

            result.Should().BeOfType<RedirectToActionResult>();

            // Description, NotesPlaceholder, CategoryId are now stored on ScorecardFacet (globally)
            var savedFacet = await context.ScorecardFacets.FirstAsync(f => f.Name == "Technical Skill");

            savedFacet.Description.Should().Be("Rate the candidate's technical depth.");
            savedFacet.NotesPlaceholder.Should().Be("e.g. Solved the system design challenge well");
            savedFacet.CategoryId.Should().Be(category.Id);
        }
    }
}
