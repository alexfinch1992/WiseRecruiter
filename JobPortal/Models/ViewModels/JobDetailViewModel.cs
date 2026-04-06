using Microsoft.AspNetCore.Identity;

namespace JobPortal.Models.ViewModels
{
    public class JobDetailViewModel
    {
        public required Job Job { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }

        // Metadata
        public bool HasApplicantsToReview { get; set; }
        public IReadOnlyList<CandidateStageSummaryItem> StageSummary { get; set; } = [];

        // Reviewer assignment (Admin-only)
        public List<ApplicationUser> ReviewerEligibleUsers { get; set; } = [];
    }
}
