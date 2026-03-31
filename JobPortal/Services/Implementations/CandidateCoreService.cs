using System.Security.Claims;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class CandidateCoreService : ICandidateCoreService
    {
        private readonly AppDbContext _context;
        private readonly IJobAccessService _jobAccessService;

        public CandidateCoreService(AppDbContext context, IJobAccessService jobAccessService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _jobAccessService = jobAccessService ?? throw new ArgumentNullException(nameof(jobAccessService));
        }

        public async Task<Application?> LoadApplicationAsync(int applicationId, ClaimsPrincipal user)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .ThenInclude(j => j.Stages)
                .Include(a => a.CurrentStage)
                .Include(a => a.Documents)
                .Include(a => a.Candidate)
                .FirstOrDefaultAsync(m => m.Id == applicationId);

            if (application == null)
                return null;

            // RBAC: HiringManagers may only open applications for their assigned jobs
            if (user.IsInRole("HiringManager") && !user.IsInRole("Admin") && !user.IsInRole("Recruiter"))
            {
                var currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                if (!await _jobAccessService.CanAccessJobAsync(currentUserId, application.JobId))
                    throw new CandidateAccessForbiddenException();
            }

            return application;
        }
    }
}
