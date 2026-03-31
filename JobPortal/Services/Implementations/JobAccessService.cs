using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobPortal.Data;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class JobAccessService : IJobAccessService
    {
        private readonly AppDbContext _context;

        public JobAccessService(AppDbContext context)
        {
            _context = context;
        }

        public Task<List<int>> GetAssignedJobIdsAsync(string userId) =>
            _context.JobAssignments
                .Where(ja => ja.UserId == userId)
                .Select(ja => ja.JobId)
                .ToListAsync();

        public Task<bool> CanAccessJobAsync(string userId, int jobId) =>
            _context.JobAssignments
                .AnyAsync(ja => ja.UserId == userId && ja.JobId == jobId);
    }
}
