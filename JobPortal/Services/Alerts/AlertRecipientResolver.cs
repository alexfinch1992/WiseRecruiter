using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Alerts
{
    public class AlertRecipientResolver
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AlertRecipientResolver(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<string>> ResolveUserIdsAsync(int jobId)
        {
            // Step 1 — Get explicit recruiters assigned to the job
            var jobUsers = await _context.JobUsers
                .Where(ju => ju.JobId == jobId && ju.IsActive)
                .ToListAsync();

            var recruiterIds = jobUsers.Select(ju => ju.UserId).ToList();

            // Step 1b — Include job owner
            var job = await _context.Jobs.FindAsync(jobId);
            if (job?.OwnerUserId != null)
                recruiterIds.Add(job.OwnerUserId);

            // Step 2 — Get all Admin users
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = admins.Select(a => a.Id).ToList();

            // Step 3 — Combine candidate users
            var candidateUserIds = recruiterIds
                .Union(adminIds)
                .ToList();

            // Step 4 — Load subscriptions for this job
            var subscriptions = await _context.JobAlertSubscriptions
                .Where(s => s.JobId == jobId)
                .ToListAsync();

            var subscriptionMap = subscriptions
                .ToDictionary(s => s.UserId, s => s.IsEnabled);

            // Step 5 & 6 — Apply rules and return only enabled users
            var adminIdSet = new HashSet<string>(adminIds);

            var enabledUserIds = candidateUserIds
                .Where(userId =>
                {
                    if (subscriptionMap.TryGetValue(userId, out var isEnabled))
                        return isEnabled;

                    // Default: Admins OFF, Recruiters ON
                    return !adminIdSet.Contains(userId);
                })
                .Distinct()
                .ToList();

            return enabledUserIds;
        }
    }
}
