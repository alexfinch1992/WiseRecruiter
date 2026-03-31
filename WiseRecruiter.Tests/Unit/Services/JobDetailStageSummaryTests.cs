using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    /// <summary>
    /// TDD tests for JobService.GetStageSummary.
    ///
    /// The "Candidates" summary box on the JobDetail page must group candidates
    /// by their *effective* stage — not by CurrentJobStageId alone.
    ///
    /// Effective stage rule:
    ///   - If CurrentJobStageId is set  → use the custom JobStage.Name
    ///   - If CurrentJobStageId is null → use app.Stage.ToString()
    ///
    /// "Unassigned" must NEVER appear unless Stage is genuinely unknown.
    /// </summary>
    public class JobDetailStageSummaryTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("stage_summary_" + Guid.NewGuid())
                .Options);

        // ── Test 1 ── System stage groups appear with correct counts ──────────

        [Fact]
        public void GetStageSummary_ReturnsCorrectCountsPerSystemStage()
        {
            // Arrange: 2 Applied, 1 Interview, 1 Offer — no custom stages
            using var ctx = CreateInMemoryContext();
            var service = new JobService(ctx);

            var job = new Job
            {
                Title = "Test Job",
                Applications = new List<Application>
                {
                    new() { Stage = ApplicationStage.Applied },
                    new() { Stage = ApplicationStage.Applied },
                    new() { Stage = ApplicationStage.Interview },
                    new() { Stage = ApplicationStage.Offer },
                },
                Stages = new List<JobStage>(),
            };

            // Act
            var summary = service.GetStageSummary(job);

            // Assert
            summary.Should().Contain(s => s.StageName == "Applied"   && s.Count == 2);
            summary.Should().Contain(s => s.StageName == "Interview" && s.Count == 1);
            summary.Should().Contain(s => s.StageName == "Offer"     && s.Count == 1);
            summary.Should().HaveCount(3);
        }

        // ── Test 2 ── System-stage candidates do NOT appear as "Unassigned" ──

        [Fact]
        public void GetStageSummary_DoesNotLabel_SystemStageCandidates_AsUnassigned()
        {
            // Arrange: candidates in Applied and Screen — CurrentJobStageId = null
            // Old (broken) logic would group them all under "Unassigned".
            using var ctx = CreateInMemoryContext();
            var service = new JobService(ctx);

            var job = new Job
            {
                Title = "Test Job",
                Applications = new List<Application>
                {
                    new() { Stage = ApplicationStage.Applied,   CurrentJobStageId = null },
                    new() { Stage = ApplicationStage.Screen,    CurrentJobStageId = null },
                    new() { Stage = ApplicationStage.Hired,     CurrentJobStageId = null },
                },
                Stages = new List<JobStage>(),
            };

            // Act
            var summary = service.GetStageSummary(job);

            // Assert — "Unassigned" must not appear at all
            summary.Should().NotContain(s => s.StageName == "Unassigned",
                because: "candidates in system stages should appear under their actual stage name");

            summary.Should().Contain(s => s.StageName == "Applied" && s.Count == 1);
            summary.Should().Contain(s => s.StageName == "Screen"  && s.Count == 1);
            summary.Should().Contain(s => s.StageName == "Hired"   && s.Count == 1);
        }

        // ── Test 3 ── Empty job → empty summary ───────────────────────────────

        [Fact]
        public void GetStageSummary_ReturnsEmpty_WhenNoApplications()
        {
            using var ctx = CreateInMemoryContext();
            var service = new JobService(ctx);

            var job = new Job
            {
                Title = "Empty Job",
                Applications = new List<Application>(),
                Stages = new List<JobStage>(),
            };

            var summary = service.GetStageSummary(job);

            summary.Should().BeEmpty();
        }

        // ── Test 4 ── Custom stages use the stage name, not "Unassigned" ──────

        [Fact]
        public void GetStageSummary_GroupsByCustomStageName_WhenCurrentJobStageIdIsSet()
        {
            // Arrange: two apps in a custom "Technical Screen" stage, one in Applied
            using var ctx = CreateInMemoryContext();
            var service = new JobService(ctx);

            var customStage = new JobStage { Id = 99, Name = "Technical Screen", Order = 2 };

            var job = new Job
            {
                Title = "Test Job",
                Applications = new List<Application>
                {
                    new() { Stage = ApplicationStage.Interview, CurrentJobStageId = 99, CurrentStage = customStage },
                    new() { Stage = ApplicationStage.Interview, CurrentJobStageId = 99, CurrentStage = customStage },
                    new() { Stage = ApplicationStage.Applied,   CurrentJobStageId = null },
                },
                Stages = new List<JobStage> { customStage },
            };

            var summary = service.GetStageSummary(job);

            summary.Should().Contain(s => s.StageName == "Technical Screen" && s.Count == 2);
            summary.Should().Contain(s => s.StageName == "Applied"          && s.Count == 1);
            summary.Should().NotContain(s => s.StageName == "Unassigned");
        }

        // ── Test 5 ── Null Applications collection → empty summary ───────────

        [Fact]
        public void GetStageSummary_ReturnsEmpty_WhenApplicationsIsNull()
        {
            using var ctx = CreateInMemoryContext();
            var service = new JobService(ctx);

            var job = new Job
            {
                Title = "Null Apps Job",
                Applications = null,
                Stages = new List<JobStage>(),
            };

            var summary = service.GetStageSummary(job);

            summary.Should().BeEmpty();
        }
    }
}
