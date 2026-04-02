using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Alerts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WiseRecruiter.Tests.Integration
{
    public class AlertRecipientResolverTests
    {
        private static ServiceProvider BuildServiceProvider(string dbName)
        {
            var services = new ServiceCollection();

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();

            services.AddLogging();

            return services.BuildServiceProvider();
        }

        private static async Task<(AppDbContext context, AlertRecipientResolver resolver, UserManager<ApplicationUser> userManager)>
            CreateTestEnvironmentAsync(string testName)
        {
            var sp = BuildServiceProvider("alert_resolver_" + testName + "_" + Guid.NewGuid());
            var context = sp.GetRequiredService<AppDbContext>();
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure roles exist
            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            if (!await roleManager.RoleExistsAsync("Recruiter"))
                await roleManager.CreateAsync(new IdentityRole("Recruiter"));

            var resolver = new AlertRecipientResolver(context, userManager);
            return (context, resolver, userManager);
        }

        private static async Task<ApplicationUser> CreateUserAsync(
            UserManager<ApplicationUser> userManager, string email, string role)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = email.Split('@')[0]
            };
            var result = await userManager.CreateAsync(user, "Test@1234");
            result.Succeeded.Should().BeTrue($"creating user {email} should succeed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        // ── Test 1 — Recruiter default ON ──────────────────────────────────────

        [Fact]
        public async Task ResolveUserIds_RecruiterWithNoSubscription_IsIncluded()
        {
            var (context, resolver, userManager) = await CreateTestEnvironmentAsync("recruiter_default");

            var recruiter = await CreateUserAsync(userManager, "recruiter1@test.com", "Recruiter");

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            context.JobUsers.Add(new JobUser
            {
                JobId = job.Id,
                UserId = recruiter.Id,
                Role = "Recruiter",
                IsActive = true
            });
            await context.SaveChangesAsync();

            var result = await resolver.ResolveUserIdsAsync(job.Id);

            result.Should().Contain(recruiter.Id);
        }

        // ── Test 2 — Admin default OFF ─────────────────────────────────────────

        [Fact]
        public async Task ResolveUserIds_AdminWithNoSubscription_IsNotIncluded()
        {
            var (context, resolver, userManager) = await CreateTestEnvironmentAsync("admin_default");

            var admin = await CreateUserAsync(userManager, "admin1@test.com", "Admin");

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            var result = await resolver.ResolveUserIdsAsync(job.Id);

            result.Should().NotContain(admin.Id);
        }

        // ── Test 3 — Admin explicitly enabled ──────────────────────────────────

        [Fact]
        public async Task ResolveUserIds_AdminWithSubscriptionEnabled_IsIncluded()
        {
            var (context, resolver, userManager) = await CreateTestEnvironmentAsync("admin_enabled");

            var admin = await CreateUserAsync(userManager, "admin2@test.com", "Admin");

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            context.JobAlertSubscriptions.Add(new JobAlertSubscription
            {
                JobId = job.Id,
                UserId = admin.Id,
                IsEnabled = true
            });
            await context.SaveChangesAsync();

            var result = await resolver.ResolveUserIdsAsync(job.Id);

            result.Should().Contain(admin.Id);
        }

        // ── Test 4 — Recruiter explicitly disabled ─────────────────────────────

        [Fact]
        public async Task ResolveUserIds_RecruiterWithSubscriptionDisabled_IsNotIncluded()
        {
            var (context, resolver, userManager) = await CreateTestEnvironmentAsync("recruiter_disabled");

            var recruiter = await CreateUserAsync(userManager, "recruiter2@test.com", "Recruiter");

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            context.JobUsers.Add(new JobUser
            {
                JobId = job.Id,
                UserId = recruiter.Id,
                Role = "Recruiter",
                IsActive = true
            });
            context.JobAlertSubscriptions.Add(new JobAlertSubscription
            {
                JobId = job.Id,
                UserId = recruiter.Id,
                IsEnabled = false
            });
            await context.SaveChangesAsync();

            var result = await resolver.ResolveUserIdsAsync(job.Id);

            result.Should().NotContain(recruiter.Id);
        }

        // ── Test 5 — No duplicates ─────────────────────────────────────────────

        [Fact]
        public async Task ResolveUserIds_UserInMultiplePaths_NoDuplicates()
        {
            var (context, resolver, userManager) = await CreateTestEnvironmentAsync("no_duplicates");

            // User who is both Admin and assigned as a recruiter on the job
            var user = await CreateUserAsync(userManager, "dual@test.com", "Admin");
            await userManager.AddToRoleAsync(user, "Recruiter");

            var job = new Job { Title = "Dev", Description = "Test" };
            context.Jobs.Add(job);
            await context.SaveChangesAsync();

            // Also assigned via JobUser
            context.JobUsers.Add(new JobUser
            {
                JobId = job.Id,
                UserId = user.Id,
                Role = "Recruiter",
                IsActive = true
            });
            // Explicitly enabled via subscription
            context.JobAlertSubscriptions.Add(new JobAlertSubscription
            {
                JobId = job.Id,
                UserId = user.Id,
                IsEnabled = true
            });
            await context.SaveChangesAsync();

            var result = await resolver.ResolveUserIdsAsync(job.Id);

            result.Should().OnlyHaveUniqueItems();
            result.Should().Contain(user.Id);
        }
    }
}
