using System;
using System.IO;
using System.Linq;
using System.Text;
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
    public class ApplicationScorecardFlowTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "application_scorecard_flow_db_" + Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        private ApplicationsController CreateController(AppDbContext context, string webRootPath)
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.WebRootPath).Returns(webRootPath);

            IApplicationService applicationService = new ApplicationService(context);
            IScorecardTemplateService templateService = new ScorecardTemplateService(context);
            IScorecardService scorecardService = new ScorecardService(context, templateService);

            var controller = new ApplicationsController(context, environment.Object, applicationService, scorecardService)
            {
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }

        private static IFormFile CreateResumeFile()
        {
            var bytes = Encoding.UTF8.GetBytes("resume content");
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "resume", "resume.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };
        }

        private static async Task<Job> SeedJobWithStageAsync(AppDbContext context, int? templateId)
        {
            var job = new Job
            {
                Title = "Software Engineer",
                Description = "Build high quality software",
                ScorecardTemplateId = templateId
            };

            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            context.JobStages.Add(new JobStage
            {
                JobId = job.Id,
                Name = "Applied",
                Order = 1
            });

            await context.SaveChangesAsync();
            return job;
        }

        [Fact]
        public async Task CreateApplication_WithValidTemplate_CreatesApplicationScorecardAndResponses()
        {
            using var context = CreateInMemoryContext();
            var webRootPath = Path.Combine(Path.GetTempPath(), "WiseRecruiterTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(webRootPath);

            try
            {
                var template = new ScorecardTemplate { Name = "Engineering Template" };
                var facet1 = new Facet { Name = "Technical Skill" };
                var facet2 = new Facet { Name = "Communication" };

                context.ScorecardTemplates.Add(template);
                context.Facets.AddRange(facet1, facet2);
                await context.SaveChangesAsync();

                context.ScorecardTemplateFacets.AddRange(
                    new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facet1.Id, ScorecardFacetId = facet1.Id },
                    new ScorecardTemplateFacet { ScorecardTemplateId = template.Id, FacetId = facet2.Id, ScorecardFacetId = facet2.Id });
                await context.SaveChangesAsync();

                var job = await SeedJobWithStageAsync(context, template.Id);
                var controller = CreateController(context, webRootPath);

                var application = new Application
                {
                    Name = "Alice Smith",
                    Email = "alice@example.com",
                    City = "Sydney",
                    JobId = job.Id
                };

                var result = await controller.Create(application, CreateResumeFile());

                result.Should().BeOfType<RedirectToActionResult>();
                var redirect = (RedirectToActionResult)result;
                redirect.ActionName.Should().Be(nameof(ApplicationsController.Index));

                context.Applications.Should().ContainSingle();
                context.Scorecards.Should().ContainSingle();
                context.ScorecardResponses.Should().HaveCount(2);
                controller.TempData.ContainsKey("Warning").Should().BeFalse();

                var createdApplication = await context.Applications.SingleAsync();
                var scorecard = await context.Scorecards.SingleAsync();
                scorecard.CandidateId.Should().Be(createdApplication.CandidateId);

                var facetNames = await context.ScorecardResponses.Select(r => r.FacetName).ToListAsync();
                facetNames.Should().BeEquivalentTo(new[] { "Technical Skill", "Communication" });
            }
            finally
            {
                if (Directory.Exists(webRootPath))
                    Directory.Delete(webRootPath, true);
            }
        }

    }
}