using JobPortal.Models.ViewModels;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IScorecardCommandService
    {
        Task<CreateScorecardViewModel?> GetCreateScorecardModelAsync(int applicationId);

        Task<bool> PopulateCreateScorecardContextAsync(CreateScorecardViewModel model);

        /// <summary>
        /// Creates a scorecard from the submitted form, including interview linking and response
        /// persistence.  Encapsulates all business logic from the CreateScorecard POST action.
        /// </summary>
        Task<(CreateScorecardResult Result, int? ScorecardId)> CreateScorecardAsync(
            CreateScorecardViewModel model,
            string submittedBy);

        Task<(bool ScorecardFound, int? ApplicationId)> ArchiveScorecardAsync(int id);

        Task<bool> UpdateInterviewNotesAsync(int applicationId, string interviewNotes);
    }
}
