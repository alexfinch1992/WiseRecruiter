using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseRecruiter.Tests.Helpers;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    /// <summary>
    /// Tests for unified pagination, sorting, and search in AdminController.JobDetail.
    /// Written BEFORE implementation — expected to FAIL until the fix lands.
    /// </summary>
    public class JobDetailPaginationTests
    {
        private static AppDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("pagination_" + Guid.NewGuid())
                .Options);

        private static async Task<Job> SeedJobWithCandidates(AppDbContext ctx, int count)
        {
            var job = new Job { Title = "Paginated Job", Description = "Test" };
            ctx.Jobs.Add(job);
            await ctx.SaveChangesAsync();

            // Add pipeline stages
            ctx.JobStages.Add(new JobStage { JobId = job.Id, Name = "Interview", Order = 1 });
            await ctx.SaveChangesAsync();

            var names = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank",
                                "Grace", "Hank", "Ivy", "Jack", "Karen", "Leo" };

            for (int i = 0; i < count; i++)
            {
                var first = names[i % names.Length];
                var last = $"Tester{i:D3}";
                var candidate = new Candidate
                {
                    FirstName = first,
                    LastName = last,
                    Email = $"test{i}@example.com",
                    CreatedAt = DateTime.UtcNow
                };
                ctx.Candidates.Add(candidate);
                await ctx.SaveChangesAsync();

                ctx.Applications.Add(new Application
                {
                    Name = $"{first} {last}",
                    Email = candidate.Email,
                    City = "TestCity",
                    JobId = job.Id,
                    CandidateId = candidate.Id,
                    Stage = (ApplicationStage)(i % 5), // distribute across stages
                    AppliedDate = DateTime.UtcNow.AddDays(-i)
                });
            }
            await ctx.SaveChangesAsync();
            return job;
        }

        // ── 1. Pagination returns correct page size ──

        [Fact]
        public async Task JobDetail_Page1_Returns25Candidates()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id, page: 1);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<Job>().Subject;
            model.Applications!.Count.Should().Be(25);
        }

        // ── 2. Page 2 returns different items than page 1 ──

        [Fact]
        public async Task JobDetail_Page2_ReturnsDifferentCandidatesThanPage1()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);

            var ctrl1 = AdminControllerFactory.Create(ctx);
            var result1 = await ctrl1.JobDetail(job.Id, page: 1);
            var page1Ids = ((result1 as ViewResult)!.Model as Job)!
                .Applications!.Select(a => a.Id).ToList();

            var ctrl2 = AdminControllerFactory.Create(ctx);
            var result2 = await ctrl2.JobDetail(job.Id, page: 2);
            var page2Ids = ((result2 as ViewResult)!.Model as Job)!
                .Applications!.Select(a => a.Id).ToList();

            page1Ids.Should().NotIntersectWith(page2Ids);
        }

        // ── 3. Sort + pagination together ──

        [Fact]
        public async Task JobDetail_SortByName_Page2_IsSortedAndPaginated()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id, sort: "name", dir: "asc", page: 2);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<Job>().Subject;

            model.Applications!.Count.Should().Be(25);

            // Items should be sorted by name ascending
            var names = model.Applications!.Select(a => a.Candidate?.LastName ?? "").ToList();
            names.Should().BeInAscendingOrder();
        }

        // ── 4. Search + pagination ──

        [Fact]
        public async Task JobDetail_SearchFilter_ReturnsOnlyMatchingCandidates()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id, searchQuery: "Alice");

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeOfType<Job>().Subject;

            model.Applications!.Should().AllSatisfy(a =>
                a.Name.Should().Contain("Alice"));
        }

        // ── 5. TotalCount via ViewBag reflects full dataset, not paginated subset ──

        [Fact]
        public async Task JobDetail_TotalCount_ReflectsFullDataset()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id, page: 1);

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var totalCount = (int)controller.ViewBag.TotalCount;
            totalCount.Should().Be(60);
        }

        // ── 6. Search reduces TotalCount ──

        [Fact]
        public async Task JobDetail_SearchFilter_ReducesTotalCount()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id, searchQuery: "Alice");

            var totalCount = (int)controller.ViewBag.TotalCount;
            totalCount.Should().BeLessThan(60);
            totalCount.Should().BeGreaterThan(0);
        }

        // ── 7. Default sort with no page defaults to page 1 ──

        [Fact]
        public async Task JobDetail_NoPageParam_DefaultsToPage1()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id);

            var currentPage = (int)controller.ViewBag.CurrentPage;
            currentPage.Should().Be(1);
        }

        // ── 8. ViewData includes searchQuery for Razor ──

        [Fact]
        public async Task JobDetail_SearchQuery_IsPassedToViewData()
        {
            using var ctx = CreateContext();
            var job = await SeedJobWithCandidates(ctx, 60);
            var controller = AdminControllerFactory.Create(ctx);

            var result = await controller.JobDetail(job.Id, searchQuery: "Alice");

            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            controller.ViewData["SearchQuery"].Should().Be("Alice");
        }
    }
}
