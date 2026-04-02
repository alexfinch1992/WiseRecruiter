using System;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class JobOwnershipConstraintTests
    {
        private static AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("job_ownership_" + Guid.NewGuid())
                .Options);

        // ── Test 1 — Deactivating last recruiter throws ────────────────────────

        [Fact]
        public async Task DeactivateRecruiter_WhenLastActive_ThrowsInvalidOperation()
        {
            using var context = CreateInMemoryContext();
            var service = new JobCommandService(context);

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var jobUser = new JobUser
            {
                JobId = job.Id,
                UserId = "user-1",
                Role = "Recruiter",
                IsActive = true
            };
            context.JobUsers.Add(jobUser);
            await context.SaveChangesAsync();

            var act = () => service.DeactivateRecruiterAsync(jobUser.Id);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*at least one active recruiter*");
        }

        // ── Test 2 — Deactivating non-last recruiter succeeds ──────────────────

        [Fact]
        public async Task DeactivateRecruiter_WhenOthersRemain_Succeeds()
        {
            using var context = CreateInMemoryContext();
            var service = new JobCommandService(context);

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var jobUser1 = new JobUser
            {
                JobId = job.Id,
                UserId = "user-1",
                Role = "Recruiter",
                IsActive = true
            };
            var jobUser2 = new JobUser
            {
                JobId = job.Id,
                UserId = "user-2",
                Role = "Recruiter",
                IsActive = true
            };
            context.JobUsers.AddRange(jobUser1, jobUser2);
            await context.SaveChangesAsync();

            await service.DeactivateRecruiterAsync(jobUser1.Id);

            var deactivated = await context.JobUsers.FindAsync(jobUser1.Id);
            deactivated!.IsActive.Should().BeFalse();
        }

        // ── Test 3 — AssignRecruiter adds active JobUser ───────────────────────

        [Fact]
        public async Task AssignRecruiter_CreatesActiveJobUser()
        {
            using var context = CreateInMemoryContext();
            var service = new JobCommandService(context);

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            await service.AssignRecruiterAsync(job.Id, "user-1", "Owner");

            var jobUser = await context.JobUsers
                .FirstOrDefaultAsync(ju => ju.JobId == job.Id && ju.UserId == "user-1");

            jobUser.Should().NotBeNull();
            jobUser!.IsActive.Should().BeTrue();
            jobUser.Role.Should().Be("Owner");
        }
    }
}
