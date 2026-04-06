using System.Security.Claims;
using JobPortal.Data;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;

namespace WiseRecruiter.Tests.Helpers
{
    /// <summary>
    /// Central factory for constructing <see cref="AdminController"/> and all its
    /// dependencies in integration/unit tests.
    /// Eliminates per-test-class service wiring boilerplate.
    /// </summary>
    internal static class AdminControllerFactory
    {
        /// <summary>Default admin identity used when no <paramref name="user"/> is supplied.</summary>
        private static ClaimsPrincipal DefaultAdmin =>
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Name, "admin"),
                        new Claim(ClaimTypes.Role, "Admin"),
                    },
                    "Identity.Application"));

        /// <summary>
        /// Constructs a fully wired <see cref="AdminController"/> backed by
        /// <paramref name="context"/>.
        /// </summary>
        /// <param name="context">In-memory (or test) database context.</param>
        /// <param name="user">
        /// Identity to place on the controller's <c>HttpContext</c>.
        /// Defaults to an Admin user when <c>null</c>.
        /// </param>
        /// <param name="templateService">
        /// Override the scorecard-template service (e.g. to inject a pre-seeded instance).
        /// A default <see cref="ScorecardTemplateService"/> is created when <c>null</c>.
        /// </param>
        public static AdminController Create(
            AppDbContext context,
            ClaimsPrincipal? user = null,
            IScorecardTemplateService? templateService = null)
        {
            var template       = templateService ?? new ScorecardTemplateService(context);
            var scorecardSvc   = new ScorecardService(context, template);
            var recSvc         = new RecommendationService(context, new StageOrderService());
            var jobCommandSvc  = new JobCommandService(context);
            var jobAccessSvc   = new JobAccessService(context);
            var jobQuerySvc    = new JobQueryService(context);
            var interviewSvc   = new InterviewService(context);
            var hiringPipeline = new HiringPipelineService();
            var analytics      = new ScorecardAnalyticsService(context);
            var resumeReview   = new ResumeReviewService(context);

            var candidateDetails = new CandidateDetailsService(
                new CandidateCoreService(context, jobAccessSvc),
                new ScorecardSummaryService(scorecardSvc),
                analytics,
                interviewSvc,
                new RelatedApplicationsService(context),
                new RecommendationSummaryService(context),
                hiringPipeline);

            var auditSvc          = new AuditService(context);
            var appStageSvc        = new ApplicationStageService(context, recSvc);

            var controller = new AdminController(
                new Mock<IWebHostEnvironment>().Object,
                new ApplicationService(context),
                new AnalyticsService(context),
                scorecardSvc,
                template,
                new JobService(context),
                jobCommandSvc,
                jobQuerySvc,
                analytics,
                appStageSvc,
                hiringPipeline,
                new GlobalSearchService(context),
                auditSvc,
                jobAccessSvc,
                candidateDetails,
                new MoveApplicationStageService(context, appStageSvc, auditSvc),
                resumeReview,
                new ScorecardCommandService(context, scorecardSvc),
                new CandidateQueryService(context),
                interviewSvc,
                Mock.Of<ILogger<AdminController>>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user ?? DefaultAdmin }
                },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };

            return controller;
        }
    }
}
