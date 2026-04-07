using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// TDD tests for the Email Template Library feature.
    ///
    /// Test 1 — Unit: GetParsedBody correctly replaces {{FirstName}} placeholder.
    /// Test 2 — Integration: SaveTemplate POST persists Subject and Body to the database.
    /// Test 3 — Integration: SendMockEmail POST creates an AuditLog entry containing
    ///           the template name and the recipient's email address.
    /// </summary>
    public class EmailTemplateTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        private static AppDbContext CreateContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("email_tmpl_" + Guid.NewGuid())
                .Options);

        private static ClaimsPrincipal DefaultAdmin =>
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Name, "admin"),
                        new Claim(ClaimTypes.Role, "Admin"),
                    },
                    "Identity.Application"));

        private static EmailController CreateEmailController(AppDbContext context)
        {
            var auditSvc  = new AuditService(context);
            var emailSvc  = new EmailService(context, auditSvc);
            var controller = new EmailController(emailSvc, Mock.Of<ILogger<EmailController>>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = DefaultAdmin }
                },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
            };
            return controller;
        }

        // ── Test 1: placeholder replacement ────────────────────────────────────

        [Fact]
        public void GetParsedBody_ReplacesFirstNamePlaceholder()
        {
            // Arrange
            var template = new EmailTemplate
            {
                Name        = "Screening Invite",
                Subject     = "Interview invite for {{FirstName}}",
                BodyContent = "Dear {{FirstName}}, we would love to connect.",
                LastModified = DateTime.UtcNow
            };

            // Act
            var result = template.GetParsedBody("Jane");

            // Assert
            result.Should().Be("Dear Jane, we would love to connect.");
        }

        // ── Test 2: SaveTemplate persists subject and body ──────────────────────

        [Fact]
        public async Task SaveTemplate_PersistsSubjectAndBody()
        {
            // Arrange
            var context    = CreateContext();
            var controller = CreateEmailController(context);

            var template = new EmailTemplate
            {
                Name         = "Offer Letter",
                Subject      = "Congratulations {{FirstName}}!",
                BodyContent  = "Hi {{FirstName}}, we are pleased to make you an offer.",
                LastModified = DateTime.UtcNow
            };

            // Act
            var result = await controller.SaveTemplate(template);

            // Assert — redirects back to the list view
            result.Should().BeOfType<RedirectToActionResult>()
                .Which.ActionName.Should().Be("EmailTemplates");

            // Assert — record was persisted
            var saved = await context.EmailTemplates
                .FirstOrDefaultAsync(t => t.Name == "Offer Letter");

            saved.Should().NotBeNull();
            saved!.Subject.Should().Be("Congratulations {{FirstName}}!");
            saved.BodyContent.Should().Be("Hi {{FirstName}}, we are pleased to make you an offer.");
        }

        // ── Test 3: SendMockEmail creates an audit log entry ────────────────────

        [Fact]
        public async Task SendMockEmail_CreatesAuditLogWithTemplateNameAndRecipient()
        {
            // Arrange
            var context    = CreateContext();
            var controller = CreateEmailController(context);

            var template = new EmailTemplate
            {
                Name         = "Screening Invite",
                Subject      = "Interview invite for {{FirstName}}",
                BodyContent  = "Hi {{FirstName}}, we'd like to invite you to a call.",
                LastModified = DateTime.UtcNow
            };
            context.EmailTemplates.Add(template);

            var candidate = new Candidate
            {
                FirstName = "John",
                LastName  = "Doe",
                Email     = "john.doe@example.com",
                CreatedAt = DateTime.UtcNow
            };
            context.Candidates.Add(candidate);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.SendMockEmail(template.Id, candidate.Id);

            // Assert — action returns a JSON success response
            result.Should().BeOfType<JsonResult>()
                .Which.Value.Should().BeEquivalentTo(new { success = true });

            // Assert — audit log was written with the expected content
            var auditLog = await context.AuditLogs
                .FirstOrDefaultAsync(a => a.Action == "EmailSent");

            auditLog.Should().NotBeNull();
            auditLog!.Changes.Should().Contain("Screening Invite");
            auditLog.Changes.Should().Contain("john.doe@example.com");
        }
    }
}