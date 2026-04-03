using JobPortal.Data;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Alerts
{
    public class ReviewerResolver
    {
        private readonly AppDbContext _context;

        public ReviewerResolver(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> ResolveReviewerUserIdsAsync(int jobId)
        {
            return await _context.JobUsers
                .Where(x => x.JobId == jobId &&
                            x.Role == "Reviewer" &&
                            x.IsActive)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync();
        }
    }
}
