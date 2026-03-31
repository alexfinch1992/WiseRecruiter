using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Verifies the endpoint wiring for InterviewController:
    /// routing attributes, HTTP method, authorization, CSRF enforcement,
    /// parameter shape for model binding, and response types.
    ///
    /// These tests deliberately do NOT re-test business logic (covered by
    /// InterviewCommandServiceTests) or happy-path / invalid-application
    /// scenarios (covered by InterviewControllerTests).
    /// </summary>
    public class InterviewEndpointWiringTests
    {
        // ── helpers ─────────────────────────────────────────────────────────────

        private static MethodInfo CreateInterviewMethod() =>
            typeof(InterviewController)
                .GetMethod(nameof(InterviewController.CreateInterview))
            ?? throw new InvalidOperationException("CreateInterview method not found.");

        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("interview_wiring_" + Guid.NewGuid())
                .Options);

        private static async Task<(Candidate candidate, Application application, JobStage stage)> SeedAsync(AppDbContext ctx)
        {
            var candidate = new Candidate
            {
                FirstName = "Wire",
                LastName = "Test",
                Email = $"wire_{Guid.NewGuid()}@example.com",
                CreatedAt = DateTime.UtcNow
            };
            ctx.Candidates.Add(candidate);

            var job = new Job { Title = "Engineer" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var stage = new JobStage { JobId = job.Id, Name = "Interview", Order = 1 };
            ctx.JobStages.Add(stage);
            await ctx.SaveChangesAsync();

            var application = new Application
            {
                CandidateId = candidate.Id,
                JobId       = job.Id,
                Name        = "Wire Test",
                Email       = candidate.Email,
                City        = "Sydney",
                Stage       = ApplicationStage.Applied,
                CurrentJobStageId = stage.Id
            };
            ctx.Applications.Add(application);
            await ctx.SaveChangesAsync();

            return (candidate, application, stage);
        }

        // ── Routing wiring ───────────────────────────────────────────────────────

        [Fact]
        public void InterviewController_HasRouteAttribute_Admin()
        {
            // The controller-level [Route("Admin")] combined with
            // [HttpPost("CreateInterview")] produces POST /Admin/CreateInterview.
            var routeAttr = typeof(InterviewController)
                .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
                .Cast<RouteAttribute>()
                .FirstOrDefault();

            routeAttr.Should().NotBeNull("InterviewController must carry a [Route] attribute");
            routeAttr!.Template.Should().Be("Admin",
                "controller route must be 'Admin' so endpoint resolves to /Admin/*");
        }

        [Fact]
        public void CreateInterview_HasHttpPostAttribute_WithCreateInterviewTemplate()
        {
            var method   = CreateInterviewMethod();
            var httpPost = method
                .GetCustomAttributes(typeof(HttpPostAttribute), inherit: false)
                .Cast<HttpPostAttribute>()
                .FirstOrDefault();

            httpPost.Should().NotBeNull("CreateInterview must be decorated with [HttpPost]");
            httpPost!.Template.Should().Be("CreateInterview",
                "route template must be 'CreateInterview' to resolve POST /Admin/CreateInterview");
        }

        // ── Authorization + CSRF wiring ──────────────────────────────────────────

        [Fact]
        public void InterviewController_RequiresAuthorization()
        {
            // [Authorize] on the class protects all actions including CreateInterview.
            // Unauthenticated requests are redirected to the login page by Identity middleware.
            var authorizeAttr = typeof(InterviewController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();

            authorizeAttr.Should().NotBeNull(
                "InterviewController must carry [Authorize] so unauthenticated users are blocked");
        }

        [Fact]
        public void CreateInterview_HasValidateAntiForgeryToken()
        {
            // [ValidateAntiForgeryToken] prevents CSRF attacks on the POST endpoint.
            var method = CreateInterviewMethod();
            var csrfAttr = method
                .GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: false)
                .FirstOrDefault();

            csrfAttr.Should().NotBeNull(
                "CreateInterview must be protected by [ValidateAntiForgeryToken]");
        }

        // ── Model-binding parameter shape ────────────────────────────────────────

        [Fact]
        public void CreateInterview_ParameterNames_MatchExpectedFormFieldNames()
        {
            // Verifies that the action's parameter names match the form field names sent
            // by the client, so MVC model binding resolves correctly.
            var parameters = CreateInterviewMethod()
                .GetParameters()
                .Select(p => p.Name!)
                .ToArray();

            // Required fields
            parameters.Should().Contain("candidateId",             "candidateId is required for ownership check");
            parameters.Should().Contain("applicationId",           "applicationId identifies the application");
            parameters.Should().Contain("selectedStage",           "selectedStage carries the stage:N or enum:X value");
            parameters.Should().Contain("scheduledAt",             "scheduledAt is the interview datetime");

            // Optional fields (default values in signature)
            parameters.Should().Contain("SelectedInterviewerIds",  "SelectedInterviewerIds is the list of assigned interviewers");
            parameters.Should().Contain("proceedWithoutApproval",  "proceedWithoutApproval enables bypass flow");
            parameters.Should().Contain("bypassReason",            "bypassReason captures the bypass justification");
        }

        // ── Response-type wiring (invalid stage → BadRequest) ────────────────────

        [Fact]
        public async Task CreateInterview_WithBareStageString_ReturnsBadRequest()
        {
            // Verifies the controller correctly surfaces an InvalidStageFormat error
            // as an HTTP 400 when the selectedStage has no recognised prefix.
            // (Service-level enum coverage is in InterviewCommandServiceTests;
            //  this test confirms the controller switch expression is wired up.)
            using var ctx = CreateInMemoryContext();
            var (candidate, application, _) = await SeedAsync(ctx);
            var controller = InterviewControllerFactory.Create(ctx);

            var result = await controller.CreateInterview(
                candidateId:   candidate.Id,
                applicationId: application.Id,
                selectedStage: "TechnicalInterview",   // missing stage:/enum: prefix
                scheduledAt:   DateTime.UtcNow.AddDays(1));

            result.Should().BeOfType<BadRequestResult>(
                "a stage string without the required prefix must return 400 Bad Request");
        }

        [Fact]
        public async Task CreateInterview_WithUnparsableStageId_ReturnsBadRequest()
        {
            // stage:abc — has the right prefix but the ID component is non-numeric.
            using var ctx = CreateInMemoryContext();
            var (candidate, application, _) = await SeedAsync(ctx);
            var controller = InterviewControllerFactory.Create(ctx);

            var result = await controller.CreateInterview(
                candidateId:   candidate.Id,
                applicationId: application.Id,
                selectedStage: "stage:abc",
                scheduledAt:   DateTime.UtcNow.AddDays(1));

            result.Should().BeOfType<BadRequestResult>(
                "a non-numeric stage ID must return 400 Bad Request");
        }
    }
}
