using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class JobAssignmentService : IJobAssignmentService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public JobAssignmentService(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task AssignOwnerAsync(int jobId, string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Exception("Owner user not found");

            var isRecruiter = await _userManager.IsInRoleAsync(user, "Recruiter");
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isRecruiter && !isAdmin)
                throw new Exception("Invalid owner role");

            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                throw new Exception("Job not found");

            job.OwnerUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task AssignReviewersAsync(int jobId, List<string>? reviewerIds)
        {
            if (reviewerIds == null || !reviewerIds.Any())
                return;

            foreach (var reviewerId in reviewerIds.Distinct())
            {
                var user = await _userManager.FindByIdAsync(reviewerId);
                if (user == null)
                    continue;

                var isRecruiter = await _userManager.IsInRoleAsync(user, "Recruiter");
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                if (!isRecruiter && !isAdmin)
                    continue;

                var exists = await _context.JobUsers.AnyAsync(x =>
                    x.JobId == jobId &&
                    x.UserId == reviewerId &&
                    x.Role == "Reviewer");

                if (!exists)
                {
                    _context.JobUsers.Add(new JobUser
                    {
                        JobId = jobId,
                        UserId = reviewerId,
                        Role = "Reviewer",
                        IsActive = true
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
