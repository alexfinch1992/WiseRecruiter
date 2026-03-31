using Microsoft.AspNetCore.Identity;
using Moq;
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
        public async Task Create_Get_ReturnsViewWithoutException()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            var controller = new AdminSettingsController(context, facetService, templateService, new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null).Object);

            Func<Task> act = async () =>
            {
                var result = await controller.Create();
                result.Should().BeOfType<ViewResult>();
            };

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task Index_Get_PopulatesTemplateNamesByFacetIdMapping()
        {
            using var context = CreateInMemoryContext();
            IFacetService facetService = new FacetService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);

            var communication = await facetService.CreateFacet("Communication");
            var quality = await facetService.CreateFacet("Quality");
            var technicalTemplate = await templateService.CreateTemplate("Technical");
            var defaultTemplate = await templateService.CreateTemplate("Default Scorecard");

            await templateService.AddFacetToTemplate(defaultTemplate.Id, communication.Id);
            await templateService.AddFacetToTemplate(defaultTemplate.Id, quality.Id);
            await templateService.AddFacetToTemplate(technicalTemplate.Id, quality.Id);

            var controller = new AdminSettingsController(context, facetService, templateService, new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null).Object);

            var result = await controller.Index();

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<Facet>>().Subject;
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

            var facet = await facetService.CreateFacet("Communication");
            var template = await templateService.CreateTemplate("Hiring Template");
            await templateService.AddFacetToTemplate(template.Id, facet.Id);

            var controller = new AdminSettingsController(context, facetService, templateService, new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null).Object);

            var result = await controller.EditTemplateFacets(template.Id, new List<TemplateFacetInput>());

            result.Should().BeOfType<ViewResult>();
            controller.ModelState[string.Empty]!.Errors.Should().Contain(error => error.ErrorMessage == "A scorecard template must have at least one facet.");

            var facetsAfterAttempt = await templateService.GetFacetsForTemplate(template.Id);
            facetsAfterAttempt.Should().HaveCount(1);
            facetsAfterAttempt[0].FacetId.Should().Be(facet.Id);
        }
    }
}
