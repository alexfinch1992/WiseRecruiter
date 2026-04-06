using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services;

/// <summary>
/// TDD tests for stage-sort ordering in JobQueryService and CandidateQueryService.
/// These tests drive alignment of sort key with display logic:
/// lifecycle (Application.Stage) first, pipeline (CurrentStage.Order) second, name third.
/// </summary>
public class StageSortTests
{
    private static AppDbContext CreateContext() => new AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // ─── Test 1: Basic lifecycle ordering (JobQueryService) ─────────────────

    [Fact]
    public async Task GetJobDetailAsync_StageSort_OrdersByLifecycleEnum()
    {
        // Arrange
        using var ctx = CreateContext();
        var job = new Job { Title = "Dev Role" };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        SeedLifecycleMix(ctx, job.Id);
        await ctx.SaveChangesAsync();

        var svc = new JobQueryService(ctx);

        // Act – "stage" hits the default switch branch
        var result = await svc.GetJobDetailAsync(job.Id, "stage");

        // Assert
        result.Should().NotBeNull();
        var stages = result!.Applications!.Select(a => a.Stage).ToList();
        // lifecycle order: Applied → Interview → Offer → Hired
        stages.Should().ContainInOrder(
            ApplicationStage.Applied,
            ApplicationStage.Interview,
            ApplicationStage.Offer,
            ApplicationStage.Hired);
    }

    // ─── Test 2: Basic lifecycle ordering (CandidateQueryService) ───────────

    [Fact]
    public async Task GetJobDetailSearchAsync_StageSort_OrdersByLifecycleEnum()
    {
        // Arrange
        using var ctx = CreateContext();
        var job = new Job { Title = "Dev Role" };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        SeedLifecycleMix(ctx, job.Id);
        await ctx.SaveChangesAsync();

        var svc = new CandidateQueryService(ctx);

        // Act
        var result = await svc.GetJobDetailSearchAsync(job.Id, null, "stage");

        // Assert
        result.Should().NotBeNull();
        var stages = result!.Applications!.Select(a => a.Stage).ToList();
        // lifecycle order: Applied → Interview → Offer → Hired
        stages.Should().ContainInOrder(
            ApplicationStage.Applied,
            ApplicationStage.Interview,
            ApplicationStage.Offer,
            ApplicationStage.Hired);
    }

    // ─── Test 3: Secondary sort by last name within the same lifecycle stage ────

    [Fact]
    public async Task GetJobDetailAsync_StageSort_WithinSameStage_SortsByLastName()
    {
        // Arrange
        using var ctx = CreateContext();
        var job = new Job { Title = "Dev Role" };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        // Both Interview, added in reverse alphabetical order
        var cZhu   = new Candidate { FirstName = "Zara",  LastName = "Zhu" };
        var cAdams = new Candidate { FirstName = "Alice", LastName = "Adams" };
        ctx.Candidates.AddRange(cZhu, cAdams);
        await ctx.SaveChangesAsync();

        ctx.Applications.AddRange(
            new Application { Name = "Zara Zhu",   City = "Syd", JobId = job.Id, CandidateId = cZhu.Id,   Stage = ApplicationStage.Interview, CurrentJobStageId = null },
            new Application { Name = "Alice Adams", City = "Syd", JobId = job.Id, CandidateId = cAdams.Id, Stage = ApplicationStage.Interview, CurrentJobStageId = null }
        );
        await ctx.SaveChangesAsync();

        var svc = new JobQueryService(ctx);

        // Act
        var result = await svc.GetJobDetailAsync(job.Id, "stage");

        // Assert – same lifecycle, no pipeline stage → secondary sort by LastName
        // Adams (A) must come before Zhu (Z)
        result!.Applications!.Select(a => a.CandidateId)
            .Should().ContainInOrder(cAdams.Id, cZhu.Id);
    }

    // ─── Test 4: Null pipeline stage – secondary sort by last name ───────────

    [Fact]
    public async Task GetJobDetailAsync_StageSort_NullPipelineAppliedCandidates_SortedByLastName()
    {
        // Arrange
        using var ctx = CreateContext();
        var job = new Job { Title = "Dev Role" };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        // Both Applied, no pipeline stage assigned
        var cZimmerman = new Candidate { FirstName = "Zara", LastName = "Zimmerman" };
        var cAnderson  = new Candidate { FirstName = "Adam", LastName = "Anderson" };
        ctx.Candidates.AddRange(cZimmerman, cAnderson);
        await ctx.SaveChangesAsync();

        ctx.Applications.AddRange(
            new Application { Name = "Zara Zimmerman", City = "Syd", JobId = job.Id, CandidateId = cZimmerman.Id, Stage = ApplicationStage.Applied, CurrentJobStageId = null },
            new Application { Name = "Adam Anderson",  City = "Syd", JobId = job.Id, CandidateId = cAnderson.Id,  Stage = ApplicationStage.Applied, CurrentJobStageId = null }
        );
        await ctx.SaveChangesAsync();

        var svc = new JobQueryService(ctx);

        // Act
        var result = await svc.GetJobDetailAsync(job.Id, "stage");

        // Assert – within same lifecycle group, sort by last name ascending
        // Anderson (A) must precede Zimmerman (Z)
        result!.Applications!.Select(a => a.CandidateId)
            .Should().ContainInOrder(cAnderson.Id, cZimmerman.Id);
    }

    // ─── Test 5: Mixed dataset – lifecycle groups intact ─────────────────────

    [Fact]
    public async Task GetJobDetailAsync_StageSort_MixedDataset_CorrectGroupOrder()
    {
        // Arrange
        using var ctx = CreateContext();
        var job = new Job { Title = "Dev Role" };
        ctx.Jobs.Add(job);
        await ctx.SaveChangesAsync();

        var pipeline = new JobStage { JobId = job.Id, Name = "Technical", Order = 1 };
        ctx.JobStages.Add(pipeline);
        await ctx.SaveChangesAsync();

        var cApplied   = new Candidate { FirstName = "A", LastName = "Applied" };
        var cInterview = new Candidate { FirstName = "I", LastName = "Interview" };
        var cHired     = new Candidate { FirstName = "H", LastName = "Hired" };
        ctx.Candidates.AddRange(cApplied, cInterview, cHired);
        await ctx.SaveChangesAsync();

        // Add in reverse order to prove sort is not insertion-order dependent
        ctx.Applications.AddRange(
            new Application { Name = "H Hired",     City = "Syd", JobId = job.Id, CandidateId = cHired.Id,     Stage = ApplicationStage.Hired,     CurrentJobStageId = null },
            new Application { Name = "I Interview", City = "Syd", JobId = job.Id, CandidateId = cInterview.Id, Stage = ApplicationStage.Interview, CurrentJobStageId = pipeline.Id },
            new Application { Name = "A Applied",   City = "Syd", JobId = job.Id, CandidateId = cApplied.Id,   Stage = ApplicationStage.Applied,   CurrentJobStageId = null }
        );
        await ctx.SaveChangesAsync();

        var svc = new JobQueryService(ctx);

        // Act
        var result = await svc.GetJobDetailAsync(job.Id, "stage");

        // Assert
        var stages = result!.Applications!.Select(a => a.Stage).ToList();
        // Applied→Interview→Hired regardless of insertion order
        stages.Should().Equal(
            ApplicationStage.Applied,
            ApplicationStage.Interview,
            ApplicationStage.Hired);
    }

    // ─── Shared helper ───────────────────────────────────────────────────────

    private static void SeedLifecycleMix(AppDbContext ctx, int jobId)
    {
        // Add in deliberately wrong order to prove sort is not insertion-order
        var cHired     = new Candidate { FirstName = "H", LastName = "Hired" };
        var cOffer     = new Candidate { FirstName = "O", LastName = "Offer" };
        var cApplied   = new Candidate { FirstName = "A", LastName = "Applied" };
        var cInterview = new Candidate { FirstName = "I", LastName = "Interview" };
        ctx.Candidates.AddRange(cHired, cOffer, cApplied, cInterview);
        ctx.SaveChanges();

        ctx.Applications.AddRange(
            new Application { Name = "H Hired",     City = "Syd", JobId = jobId, CandidateId = cHired.Id,     Stage = ApplicationStage.Hired,     CurrentJobStageId = null },
            new Application { Name = "O Offer",     City = "Syd", JobId = jobId, CandidateId = cOffer.Id,     Stage = ApplicationStage.Offer,     CurrentJobStageId = null },
            new Application { Name = "A Applied",   City = "Syd", JobId = jobId, CandidateId = cApplied.Id,   Stage = ApplicationStage.Applied,   CurrentJobStageId = null },
            new Application { Name = "I Interview", City = "Syd", JobId = jobId, CandidateId = cInterview.Id, Stage = ApplicationStage.Interview, CurrentJobStageId = null }
        );
    }
}
