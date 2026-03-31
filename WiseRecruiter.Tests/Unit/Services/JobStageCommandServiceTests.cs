using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class JobStageCommandServiceTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("job_stage_cmd_" + Guid.NewGuid())
                .Options);

        private static JobStageCommandService CreateService(AppDbContext ctx) =>
            new JobStageCommandService(ctx);

        /// <summary>Seed a job with N custom stages ordered 0..N-1.</summary>
        private static async Task<(Job job, JobStage[] stages)> SeedJobWithStagesAsync(
            AppDbContext ctx, params string[] stageNames)
        {
            var job = new Job { Title = "Engineer" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var stages = new JobStage[stageNames.Length];
            for (int i = 0; i < stageNames.Length; i++)
            {
                stages[i] = new JobStage { JobId = job.Id, Name = stageNames[i], Order = i };
                ctx.JobStages.Add(stages[i]);
            }
            await ctx.SaveChangesAsync();

            return (job, stages);
        }

        // ── AddStageAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task AddStage_HappyPath_CreatesStageWithNextOrder()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview");
            var svc = CreateService(ctx);

            var result = await svc.AddStageAsync(job.Id, "Offer");

            result.Success.Should().BeTrue();
            result.Error.Should().Be(JobStageCommandError.None);

            var created = await ctx.JobStages.FirstAsync(s => s.Name == "Offer");
            created.JobId.Should().Be(job.Id);
            created.Order.Should().Be(2);  // max(0,1) + 1
        }

        [Fact]
        public async Task AddStage_FirstStage_OrderIsZero()
        {
            using var ctx = CreateInMemoryContext();
            var job = new Job { Title = "EmptyJob" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            var svc = CreateService(ctx);
            var result = await svc.AddStageAsync(job.Id, "Applied");

            result.Success.Should().BeTrue();
            var created = await ctx.JobStages.FirstAsync(s => s.JobId == job.Id);
            created.Order.Should().Be(0);  // -1 + 1
        }

        [Fact]
        public async Task AddStage_EmptyName_ReturnsNameEmpty()
        {
            using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedJobWithStagesAsync(ctx, "Applied");
            var svc = CreateService(ctx);

            var result = await svc.AddStageAsync(job.Id, "   ");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.NameEmpty);
            (await ctx.JobStages.CountAsync(s => s.JobId == job.Id)).Should().Be(1);
        }

        [Fact]
        public async Task AddStage_JobNotFound_ReturnsJobNotFound()
        {
            using var ctx = CreateInMemoryContext();
            var svc = CreateService(ctx);

            var result = await svc.AddStageAsync(9999, "Offer");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.JobNotFound);
        }

        [Fact]
        public async Task AddStage_DuplicateName_ReturnsStageAlreadyExists()
        {
            using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview");
            var svc = CreateService(ctx);

            var result = await svc.AddStageAsync(job.Id, "Interview");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.StageAlreadyExists);
            (await ctx.JobStages.CountAsync(s => s.JobId == job.Id)).Should().Be(2);
        }

        [Fact]
        public async Task AddStage_DuplicateNameOnDifferentJob_Succeeds()
        {
            using var ctx = CreateInMemoryContext();
            var (job1, _) = await SeedJobWithStagesAsync(ctx, "Interview");
            var (job2, _) = await SeedJobWithStagesAsync(ctx);
            var svc = CreateService(ctx);

            // "Interview" exists for job1 but not job2 — should succeed for job2
            var result = await svc.AddStageAsync(job2.Id, "Interview");

            result.Success.Should().BeTrue();
        }

        // ── RemoveStageAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveStage_HappyPath_RemovesStage()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview", "Offer");
            var svc = CreateService(ctx);

            var result = await svc.RemoveStageAsync(stages[1].Id);

            result.Success.Should().BeTrue();
            (await ctx.JobStages.CountAsync(s => s.JobId == job.Id)).Should().Be(2);
            (await ctx.JobStages.AnyAsync(s => s.Name == "Interview")).Should().BeFalse();
        }

        [Fact]
        public async Task RemoveStage_ValidStage_ReturnsSuccess()
        {
            using var ctx = CreateInMemoryContext();
            var (_, stages) = await SeedJobWithStagesAsync(ctx, "Applied");
            var svc = CreateService(ctx);

            var result = await svc.RemoveStageAsync(stages[0].Id);

            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task RemoveStage_StageNotFound_ReturnsStageNotFound()
        {
            using var ctx = CreateInMemoryContext();
            var svc = CreateService(ctx);

            var result = await svc.RemoveStageAsync(9999);

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.StageNotFound);
        }

        // ── MoveStageAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task MoveStage_Down_SwapsOrderWithNextStage()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview", "Offer");
            var svc = CreateService(ctx);

            var result = await svc.MoveStageAsync(stages[0].Id, job.Id, "down");

            result.Success.Should().BeTrue();
            await ctx.Entry(stages[0]).ReloadAsync();
            await ctx.Entry(stages[1]).ReloadAsync();
            stages[0].Order.Should().Be(1);   // was 0, swapped with Interview
            stages[1].Order.Should().Be(0);   // was 1
        }

        [Fact]
        public async Task MoveStage_Up_SwapsOrderWithPreviousStage()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview", "Offer");
            var svc = CreateService(ctx);

            var result = await svc.MoveStageAsync(stages[2].Id, job.Id, "up");

            result.Success.Should().BeTrue();
            await ctx.Entry(stages[1]).ReloadAsync();
            await ctx.Entry(stages[2]).ReloadAsync();
            stages[2].Order.Should().Be(1);   // was 2, swapped with Interview
            stages[1].Order.Should().Be(2);   // was 1
        }

        [Fact]
        public async Task MoveStage_FirstStage_UpReturnsCannnotMove()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview");
            var svc = CreateService(ctx);

            var result = await svc.MoveStageAsync(stages[0].Id, job.Id, "up");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.CannotMove);
            // Orders unchanged
            await ctx.Entry(stages[0]).ReloadAsync();
            stages[0].Order.Should().Be(0);
        }

        [Fact]
        public async Task MoveStage_LastStage_DownReturnsCannotMove()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview");
            var svc = CreateService(ctx);

            var result = await svc.MoveStageAsync(stages[1].Id, job.Id, "down");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.CannotMove);
            await ctx.Entry(stages[1]).ReloadAsync();
            stages[1].Order.Should().Be(1);
        }

        [Fact]
        public async Task MoveStage_SingleStage_UpCannotMove()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Solo");
            var svc = CreateService(ctx);

            var resultUp = await svc.MoveStageAsync(stages[0].Id, job.Id, "up");
            resultUp.Success.Should().BeFalse();
            resultUp.Error.Should().Be(JobStageCommandError.CannotMove);
        }

        [Fact]
        public async Task MoveStage_SingleStage_DownCannotMove()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Solo");
            var svc = CreateService(ctx);

            var resultDown = await svc.MoveStageAsync(stages[0].Id, job.Id, "down");
            resultDown.Success.Should().BeFalse();
            resultDown.Error.Should().Be(JobStageCommandError.CannotMove);
        }

        [Fact]
        public async Task MoveStage_StageNotFound_ReturnsStageNotFound()
        {
            using var ctx = CreateInMemoryContext();
            var (job, _) = await SeedJobWithStagesAsync(ctx, "Applied");
            var svc = CreateService(ctx);

            var result = await svc.MoveStageAsync(9999, job.Id, "up");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.StageNotFound);
        }

        [Fact]
        public async Task MoveStage_StageFromDifferentJob_ReturnsStageNotFound()
        {
            using var ctx = CreateInMemoryContext();
            var (job1, stages1) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview");
            var (job2, _)       = await SeedJobWithStagesAsync(ctx, "Applied");
            var svc = CreateService(ctx);

            // stage belongs to job1; pass job2.Id — should be treated as not found
            var result = await svc.MoveStageAsync(stages1[0].Id, job2.Id, "down");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(JobStageCommandError.StageNotFound);
        }

        [Fact]
        public async Task MoveStage_ValidMove_ReturnsSuccess()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "Applied", "Interview");
            var svc = CreateService(ctx);

            var result = await svc.MoveStageAsync(stages[0].Id, job.Id, "down");

            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task MoveStage_MultipleStages_IntermediateMove_PreservesOtherOrders()
        {
            // A B C D — move B down → A C B D
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "A", "B", "C", "D");
            var svc = CreateService(ctx);

            await svc.MoveStageAsync(stages[1].Id, job.Id, "down"); // move B down

            await ctx.Entry(stages[0]).ReloadAsync();
            await ctx.Entry(stages[1]).ReloadAsync();
            await ctx.Entry(stages[2]).ReloadAsync();
            await ctx.Entry(stages[3]).ReloadAsync();

            var ordered = new[] { stages[0], stages[1], stages[2], stages[3] }
                .OrderBy(s => s.Order).Select(s => s.Name).ToArray();

            ordered.Should().Equal("A", "C", "B", "D");
        }

        [Fact]
        public async Task MoveStageAsync_Should_NotCreateDuplicateOrderValues()
        {
            using var ctx = CreateInMemoryContext();
            var (job, stages) = await SeedJobWithStagesAsync(ctx, "A", "B", "C", "D");
            var svc = CreateService(ctx);

            await svc.MoveStageAsync(stages[1].Id, job.Id, "down"); // move B down

            var allStages = await ctx.JobStages.Where(s => s.JobId == job.Id).ToListAsync();
            var orders = allStages.Select(s => s.Order).ToList();
            orders.Should().OnlyHaveUniqueItems("swapping must not create duplicate Order values");
            allStages.Should().HaveCount(4);
        }
    }
}
