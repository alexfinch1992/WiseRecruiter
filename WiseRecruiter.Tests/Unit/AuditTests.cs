using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace WiseRecruiter.Tests.Unit
{
    /// <summary>
    /// TDD suite for the audit trail feature.
    /// Tests drive the requirement that every stage move and soft-gate override
    /// produces a durable, unerasable AuditLog row.
    /// </summary>
    public class AuditTests
    {
        // ── Infrastructure ────────────────────────────────────────────────────

        private static AppDbContext CreateInMemoryContext() =>
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase("audit_db_" + Guid.NewGuid())
                    .Options);

        // ── Tests ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task MoveStage_ShouldCreateAuditEntry()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var auditService  = new AuditService(context);

            // Seed a minimal application so the EntityId references a real row
            context.Jobs.Add(new Job { Id = 1, Title = "Engineer", CreatedByUserId = "System_Seed" });
            context.Candidates.Add(new Candidate { Id = 1, FirstName = "Alice", LastName = "Johnson", Email = "alice@example.com" });
            context.Applications.Add(new Application
            {
                Id                = 1,
                Name              = "Alice Johnson",
                Email             = "alice@example.com",
                City              = "Sydney",
                JobId             = 1,
                CandidateId       = 1,
                Stage             = ApplicationStage.Applied,
                CreatedByUserId   = "System_Seed"
            });
            await context.SaveChangesAsync();

            // Act — simulate controller calling LogAsync after a stage move
            await auditService.LogAsync(
                entityName : "Application",
                entityId   : 1,
                action     : "StageMove",
                changes    : "Old: Applied -> New: Interview; Override: False",
                userId     : "Legacy_Admin");

            // Assert
            var logs = await context.AuditLogs.ToListAsync();
            logs.Should().HaveCount(1);

            var log = logs.Single();
            log.EntityName.Should().Be("Application");
            log.EntityId.Should().Be(1);
            log.Action.Should().Be("StageMove");
            log.UserId.Should().Be("Legacy_Admin");
            log.Changes.Should().Contain("Applied").And.Contain("Interview");
            log.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Override_ShouldCaptureOverrideActionInAuditEntry()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var auditService  = new AuditService(context);

            // Act — simulate an overridden gate move
            await auditService.LogAsync(
                entityName : "Application",
                entityId   : 42,
                action     : "Override",
                changes    : "Old: Screen -> New: Offer; Override: True",
                userId     : "Legacy_Admin");

            // Assert — the action column must distinguish an override from a normal move
            var log = await context.AuditLogs.SingleAsync();
            log.Action.Should().Be("Override");
            log.Changes.Should().Contain("Override: True");
        }

        [Fact]
        public async Task MultipleEvents_ShouldEachProduceSeparateAuditRow()
        {
            // Arrange
            using var context = CreateInMemoryContext();
            var auditService  = new AuditService(context);

            // Act
            await auditService.LogAsync("Application", 1, "StageMove",  "Old: Applied -> New: Screen; Override: False",  "Legacy_Admin");
            await auditService.LogAsync("Application", 1, "StageMove",  "Old: Screen -> New: Interview; Override: False", "Legacy_Admin");
            await auditService.LogAsync("Application", 1, "Override",   "Old: Interview -> New: Offer; Override: True",   "Legacy_Admin");

            // Assert — three independent, immutable rows
            var logs = await context.AuditLogs.OrderBy(l => l.Id).ToListAsync();
            logs.Should().HaveCount(3);
            logs[2].Action.Should().Be("Override");
        }
    }
}
