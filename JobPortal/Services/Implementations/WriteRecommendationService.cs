using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class WriteRecommendationService : IWriteRecommendationService
    {
        private readonly AppDbContext _context;

        public WriteRecommendationService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<WriteRecommendationViewModel?> GetStage1ViewModelAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            return new WriteRecommendationViewModel
            {
                Id          = application.Id,
                JobId       = application.JobId,
                Name        = application.Name,
                JobTitle    = application.Job?.Title,
                StageNumber = 1,
                RecStatus   = rec?.Status.ToString() ?? "None",
            };
        }

        public async Task<WriteRecommendationViewModel?> GetStage2ViewModelAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);

            return new WriteRecommendationViewModel
            {
                Id          = application.Id,
                JobId       = application.JobId,
                Name        = application.Name,
                JobTitle    = application.Job?.Title,
                StageNumber = 2,
                RecStatus   = rec?.Status.ToString() ?? "None",
            };
        }
    }
}
