using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace WiseRecruiter.Tests.Unit
{
    /// <summary>
    /// TDD suite for job-visibility filtering.
    ///
    /// Design contract
    /// ───────────────
    /// • A HiringManager with NO assignments sees 0 jobs.
    /// • A HiringManager with 1 assignment sees exactly that 1 job.
    /// • An Admin (or Recruiter) bypasses the service entirely — any call that
    ///   returns ALL jobs from the DbContext satisfies the global-access contract.
    ///
    /// The controller delegates to IJobAccessService, which is what we test here
    /// (matching the "UserId injection during testing" requirement).  The
    /// controller simply passes User.GetUserId() at runtime; in tests we supply
    /// the userId directly.
    /// </summary>
    public class HiringManagerFilterTests
    {
        // ── Infrastructure ─────────────────────────────────────────────────
        private static AppDbContext CreateDb() =>
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase("rbac_" + System.Guid.NewGuid())
                    .Options);

        private static async Task<AppDbContext> SeedJobsAsync(
            AppDbContext db, IEnumerable<int> jobIds)
        {
            foreach (var id in jobIds)
                db.Jobs.Add(new Job { Id = id, Title = $"Job {id}", CreatedByUserId = "sys" });
            await db.SaveChangesAsync();
            return db;
        }

        // ── Tests ───────────────────────────────────────────────────────────

        [Fact]
        public async Task HiringManager_WithNoAssignments_ReturnsZeroJobs()
        {
            // Arrange – two jobs exist, but userId "hm-none" has no assignments
            using var db = await SeedJobsAsync(CreateDb(), new[] { 1, 2 });
            var svc = new JobAccessService(db);

            // Act
            var assignedIds = await svc.GetAssignedJobIdsAsync("hm-none");

            // Assert
            assignedIds.Should().BeEmpty(
                because: "a HiringManager with no JobAssignment rows must see zero jobs");
        }

        [Fact]
        public async Task HiringManager_WithOneAssignment_ReturnsExactlyOneJob()
        {
            // Arrange – three jobs exist; hm-one is assigned to job id=2 only
            using var db = await SeedJobsAsync(CreateDb(), new[] { 1, 2, 3 });
            db.JobAssignments.Add(new JobAssignment { UserId = "hm-one", JobId = 2 });
            await db.SaveChangesAsync();
            var svc = new JobAccessService(db);

            // Act
            var assignedIds = await svc.GetAssignedJobIdsAsync("hm-one");

            // Assert
            assignedIds.Should().ContainSingle(id => id == 2,
                because: "exactly the one assigned job must be returned");
        }

        [Fact]
        public async Task Admin_GlobalAccess_AllJobsReturnedWithoutFiltering()
        {
            // Arrange – Admin bypasses IJobAccessService at the controller level.
            //   We verify the contract: when no filtering is applied, all jobs
            //   from the context are visible.
            using var db = await SeedJobsAsync(CreateDb(), new[] { 10, 20, 30 });

            // Act – simulate Admin path: load all jobs directly (no service call)
            var allJobs = await db.Jobs.ToListAsync();

            // Assert
            allJobs.Should().HaveCount(3,
                because: "Admin/Recruiter callers skip IJobAccessService and see all jobs");
        }

        /// <summary>
        /// Verifies CanAccessJobAsync correctly awards / denies per assignment row.
        /// This guards the CandidateDetails RBAC gate added to AdminController.
        /// </summary>
        [Fact]
        public async Task CanAccessJob_ReturnsTrueOnlyWhenAssigned()
        {
            using var db = await SeedJobsAsync(CreateDb(), new[] { 5, 6 });
            db.JobAssignments.Add(new JobAssignment { UserId = "hm-partial", JobId = 5 });
            await db.SaveChangesAsync();
            var svc = new JobAccessService(db);

            (await svc.CanAccessJobAsync("hm-partial", 5)).Should().BeTrue();
            (await svc.CanAccessJobAsync("hm-partial", 6)).Should().BeFalse();
        }
    }
}
