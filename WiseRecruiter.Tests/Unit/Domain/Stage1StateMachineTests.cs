using System;
using FluentAssertions;
using JobPortal.Domain.Recommendations;
using JobPortal.Models;
using JobPortal.Services.Models;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Domain
{
    public class Stage1StateMachineTests
    {
        private readonly Stage1StateMachine _machine = new();

        private static CandidateRecommendation MakeRec(RecommendationStatus status) => new()
        {
            ApplicationId = 1,
            Stage = RecommendationStage.Stage1,
            Status = status,
            LastUpdatedUtc = DateTime.UtcNow
        };

        private static Stage1TransitionContext DraftContext(
            string? notes = "Notes", string? strengths = null, string? concerns = null, bool? hire = null)
            => Stage1TransitionContext.ForDraftSave(notes, strengths, concerns, hire);

        private static Stage1TransitionContext SubmitContext(int userId = 7)
            => Stage1TransitionContext.ForSubmit(userId);

        private static Stage1TransitionContext ApproveContext(int userId = 42)
            => Stage1TransitionContext.ForApproval(userId);

        // ─── CanTransition ──────────────────────────────────────────────────

        [Theory]
        [InlineData(RecommendationStatus.Draft,     RecommendationStatus.Draft)]
        [InlineData(RecommendationStatus.Draft,     RecommendationStatus.Submitted)]
        [InlineData(RecommendationStatus.Submitted, RecommendationStatus.Draft)]
        [InlineData(RecommendationStatus.Submitted, RecommendationStatus.Approved)]
        public void CanTransition_ValidTransitions_ReturnsTrue(RecommendationStatus from, RecommendationStatus to)
        {
            _machine.CanTransition(from, to).Should().BeTrue();
        }

        [Theory]
        [InlineData(RecommendationStatus.Draft,     RecommendationStatus.Approved)]
        [InlineData(RecommendationStatus.Submitted, RecommendationStatus.Submitted)]
        [InlineData(RecommendationStatus.Approved,  RecommendationStatus.Draft)]
        [InlineData(RecommendationStatus.Approved,  RecommendationStatus.Submitted)]
        [InlineData(RecommendationStatus.Approved,  RecommendationStatus.Approved)]
        public void CanTransition_InvalidTransitions_ReturnsFalse(RecommendationStatus from, RecommendationStatus to)
        {
            _machine.CanTransition(from, to).Should().BeFalse();
        }

        // ─── ApplyTransition side effects ─────────────────────────────────

        [Fact]
        public void ApplyTransition_DraftToDraft_UpdatesContentOnly()
        {
            var rec = MakeRec(RecommendationStatus.Draft);
            var ctx = DraftContext("Updated notes", "Key strengths", "Minor concern", true);

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Draft, ctx);

            result.Should().Be(TransitionResult.Success);
            rec.Status.Should().Be(RecommendationStatus.Draft);
            rec.Summary.Should().Be("Updated notes");
            rec.ExperienceFit.Should().Be("Key strengths");
            rec.Concerns.Should().Be("Minor concern");
            rec.HireRecommendation.Should().BeTrue();
        }

        [Fact]
        public void ApplyTransition_DraftToSubmitted_SetsStatusAndTimestamp()
        {
            var rec = MakeRec(RecommendationStatus.Draft);
            var ctx = SubmitContext(userId: 7);
            var before = DateTime.UtcNow;

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Submitted, ctx);

            result.Should().Be(TransitionResult.Success);
            rec.Status.Should().Be(RecommendationStatus.Submitted);
            rec.SubmittedByUserId.Should().Be(7);
            rec.SubmittedUtc.Should().NotBeNull();
            rec.SubmittedUtc!.Value.Should().BeOnOrAfter(before);
        }

        [Fact]
        public void ApplyTransition_SubmittedToDraft_SetsStatusAndPreservesSubmittedUtc()
        {
            var submittedAt = DateTime.UtcNow.AddHours(-1);
            var rec = MakeRec(RecommendationStatus.Submitted);
            rec.SubmittedUtc = submittedAt;

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Draft, DraftContext("Revised notes"));

            result.Should().Be(TransitionResult.Success);
            rec.Status.Should().Be(RecommendationStatus.Draft);
            rec.SubmittedUtc.Should().BeCloseTo(submittedAt, TimeSpan.FromSeconds(1));
            rec.Summary.Should().Be("Revised notes");
        }

        [Fact]
        public void ApplyTransition_SubmittedToApproved_SetsApprovedFieldsAndClearsBypass()
        {
            var rec = MakeRec(RecommendationStatus.Submitted);
            rec.BypassedApproval = true;
            rec.BypassReason = "Urgent";
            var before = DateTime.UtcNow;

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Approved, ApproveContext(userId: 42));

            result.Should().Be(TransitionResult.Success);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.ReviewedByUserId.Should().Be(42);
            rec.ReviewedUtc.Should().NotBeNull();
            rec.ReviewedUtc!.Value.Should().BeOnOrAfter(before);
            rec.BypassedApproval.Should().BeFalse();
            rec.BypassReason.Should().BeNull();
        }

        [Fact]
        public void ApplyTransition_InvalidTransition_ReturnsInvalidStateWithoutMutation()
        {
            var rec = MakeRec(RecommendationStatus.Draft);
            rec.Summary = "Original";

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Approved, ApproveContext());

            result.Should().Be(TransitionResult.InvalidState);
            rec.Status.Should().Be(RecommendationStatus.Draft);
            rec.Summary.Should().Be("Original");
        }

        [Fact]
        public void ApplyTransition_ApprovedToAnything_ReturnsInvalidStateWithoutMutation()
        {
            var rec = MakeRec(RecommendationStatus.Approved);
            rec.Summary = "Approved content";

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Draft, DraftContext("New content"));

            result.Should().Be(TransitionResult.InvalidState);
            rec.Status.Should().Be(RecommendationStatus.Approved);
            rec.Summary.Should().Be("Approved content");
        }

        // ─── Guard tests: context validity ────────────────────────────────

        [Fact]
        public void ApplyTransition_ToSubmitted_WithDraftSaveContext_ThrowsInvalidOperation()
        {
            var rec = MakeRec(RecommendationStatus.Draft);
            var draftCtx = Stage1TransitionContext.ForDraftSave("Notes", null, null, null);

            var act = () => _machine.ApplyTransition(rec, RecommendationStatus.Submitted, draftCtx);

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ApplyTransition_ToApproved_WithDraftSaveContext_ThrowsInvalidOperation()
        {
            var rec = MakeRec(RecommendationStatus.Submitted);
            var draftCtx = Stage1TransitionContext.ForDraftSave("Notes", null, null, null);

            var act = () => _machine.ApplyTransition(rec, RecommendationStatus.Approved, draftCtx);

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ApplyTransition_DraftSave_WithNullContent_IsValid()
        {
            var rec = MakeRec(RecommendationStatus.Draft);
            var ctx = Stage1TransitionContext.ForDraftSave(null, null, null, null);

            var result = _machine.ApplyTransition(rec, RecommendationStatus.Draft, ctx);

            result.Should().Be(TransitionResult.Success);
            rec.Summary.Should().BeNull();
            rec.ExperienceFit.Should().BeNull();
            rec.Concerns.Should().BeNull();
            rec.HireRecommendation.Should().BeNull();
        }
    }
}
