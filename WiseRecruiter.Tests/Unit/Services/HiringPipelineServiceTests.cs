using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Implementations;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class HiringPipelineServiceTests
    {
        private readonly HiringPipelineService _sut = new();

        private static Application MakeApp(ApplicationStage stage, int? currentJobStageId = null) =>
            new Application { Stage = stage, CurrentJobStageId = currentJobStageId };

        private static JobStage MakeJobStage(int id, string name, int order) =>
            new JobStage { Id = id, Name = name, Order = order };

        // ── Structure ──────────────────────────────────────────────────────────

        [Fact]
        public void GetPipeline_WithNoJobStages_ReturnsFourSystemStages()
        {
            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Applied), new List<JobStage>());

            result.Should().HaveCount(4);
            result.Select(s => s.Name).Should().ContainInOrder("Applied", "Screen", "Offer", "Hired");
        }

        [Fact]
        public void GetPipeline_WithTwoJobStages_ReturnsSixStagesInOrder()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "Technical Screen", 1),
                MakeJobStage(2, "Final Interview", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Applied), jobStages);

            result.Should().HaveCount(6);
            result.Select(s => s.Name)
                  .Should().ContainInOrder("Applied", "Screen", "Technical Screen", "Final Interview", "Offer", "Hired");
        }

        [Fact]
        public void GetPipeline_JobStagesOrderedByOrderProperty()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(10, "Final Panel", 3),
                MakeJobStage(20, "First Chat", 1),
                MakeJobStage(30, "Technical Test", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Applied), jobStages);

            result.Select(s => s.Name)
                  .Should().ContainInOrder("Applied", "Screen", "First Chat", "Technical Test", "Final Panel", "Offer", "Hired");
        }

        // ── System-stage IsCurrent ─────────────────────────────────────────────

        [Fact]
        public void GetPipeline_AppliedStage_AppliedIsCurrent()
        {
            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Applied), new List<JobStage>());

            result.Single(s => s.Name == "Applied").IsCurrent.Should().BeTrue();
            result.Where(s => s.Name != "Applied").All(s => !s.IsCurrent).Should().BeTrue();
        }

        [Fact]
        public void GetPipeline_ScreenStage_ScreenIsCurrentAppliedIsCompleted()
        {
            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Screen), new List<JobStage>());

            result.Single(s => s.Name == "Screen").IsCurrent.Should().BeTrue();
            result.Single(s => s.Name == "Applied").IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void GetPipeline_OfferStage_OfferIsCurrent()
        {
            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Offer), new List<JobStage>());

            result.Single(s => s.Name == "Offer").IsCurrent.Should().BeTrue();
        }

        [Fact]
        public void GetPipeline_HiredStage_HiredIsCurrent()
        {
            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Hired), new List<JobStage>());

            result.Single(s => s.Name == "Hired").IsCurrent.Should().BeTrue();
        }

        // ── System-stage IsCompleted ───────────────────────────────────────────

        [Fact]
        public void GetPipeline_HiredStage_AllStagesBeforeHiredAreCompleted()
        {
            var jobStages = new List<JobStage> { MakeJobStage(1, "Interview", 1) };
            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Hired), jobStages);

            result.Where(s => s.Name != "Hired").All(s => s.IsCompleted).Should().BeTrue();
            result.Single(s => s.Name == "Hired").IsCompleted.Should().BeFalse();
        }

        // ── Job-stage IsCurrent ────────────────────────────────────────────────

        [Fact]
        public void GetPipeline_InterviewStageWithMatchingJobStageId_JobStageIsCurrent()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "Tech Screen", 1),
                MakeJobStage(2, "Final Round", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Interview, currentJobStageId: 2), jobStages);

            result.Single(s => s.Name == "Final Round").IsCurrent.Should().BeTrue();
        }

        [Fact]
        public void GetPipeline_InterviewStageWithNullJobStageId_NoJobStageCurrent()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "Tech Screen", 1),
                MakeJobStage(2, "Final Round", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Interview, currentJobStageId: null), jobStages);

            result.Where(s => s.Name is "Tech Screen" or "Final Round")
                  .All(s => !s.IsCurrent).Should().BeTrue();
        }

        // ── Job-stage IsCompleted ──────────────────────────────────────────────

        [Fact]
        public void GetPipeline_InterviewStageAtSecondJobStage_FirstJobStageIsCompleted()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "First Round", 1),
                MakeJobStage(2, "Second Round", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Interview, currentJobStageId: 2), jobStages);

            result.Single(s => s.Name == "First Round").IsCompleted.Should().BeTrue();
            result.Single(s => s.Name == "Second Round").IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void GetPipeline_InterviewStageWithNullJobStageId_NoJobStageCompleted()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "First Round", 1),
                MakeJobStage(2, "Second Round", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Interview, currentJobStageId: null), jobStages);

            result.Where(s => s.Name is "First Round" or "Second Round")
                  .All(s => !s.IsCompleted).Should().BeTrue();
        }

        [Fact]
        public void GetPipeline_StageBeforeInterview_NoJobStageCompleted()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "Tech Screen", 1),
                MakeJobStage(2, "Final Panel", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Screen), jobStages);

            result.Where(s => s.Name is "Tech Screen" or "Final Panel")
                  .All(s => !s.IsCompleted).Should().BeTrue();
        }

        [Fact]
        public void GetPipeline_StageAboveInterview_AllJobStagesCompleted()
        {
            var jobStages = new List<JobStage>
            {
                MakeJobStage(1, "Tech Screen", 1),
                MakeJobStage(2, "Final Panel", 2)
            };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Offer), jobStages);

            result.Where(s => s.Name is "Tech Screen" or "Final Panel")
                  .All(s => s.IsCompleted).Should().BeTrue();
        }

        // ── Rejected edge case ─────────────────────────────────────────────────

        [Fact]
        public void GetPipeline_RejectedStage_NoStageIsCurrentOrCompleted()
        {
            var jobStages = new List<JobStage> { MakeJobStage(1, "Interview Round", 1) };

            var result = _sut.GetPipeline(MakeApp(ApplicationStage.Rejected), jobStages);

            result.All(s => !s.IsCurrent && !s.IsCompleted).Should().BeTrue();
        }
    }
}
