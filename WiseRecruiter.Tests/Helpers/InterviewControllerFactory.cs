using System.Security.Claims;
using JobPortal.Data;
using JobPortal.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace WiseRecruiter.Tests.Helpers
{
    internal static class InterviewControllerFactory
    {
        private static ClaimsPrincipal DefaultAdmin =>
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Name, "admin"),
                        new Claim(ClaimTypes.Role, "Admin"),
                    },
                    "Identity.Application"));

        public static InterviewController Create(AppDbContext context, ClaimsPrincipal? user = null)
        {
            var recSvc       = new RecommendationService(context, new StageOrderService());
            var interviewSvc = new InterviewService(context);
            var cmdSvc       = new InterviewCommandService(context, interviewSvc, recSvc);

            return new InterviewController(cmdSvc)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user ?? DefaultAdmin }
                },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };
        }
    }
}
