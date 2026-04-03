namespace JobPortal.Models.ViewModels
{
    /// <summary>
    /// Returned by GET /Admin/GetCandidatesJson after the Candidate Unification
    /// refactor. One row per unique email address; multiple applications are
    /// collapsed into ApplicationIds / ActiveApplicationCount.
    /// </summary>
    public class UnifiedCandidateDto
    {
        /// <summary>The candidate's email address (the unification key).</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Full name from the most recent application.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Application ID used for navigation links (most recent application).</summary>
        public int PrimaryApplicationId { get; set; }

        /// <summary>All application IDs associated with this email.</summary>
        public List<int> ApplicationIds { get; set; } = new();

        /// <summary>Count of applications that are not in a terminal stage (Rejected/Hired).</summary>
        public int ActiveApplicationCount { get; set; }

        /// <summary>Stage of the most recent non-rejected application, for at-a-glance display.</summary>
        public string CurrentStage { get; set; } = string.Empty;

        /// <summary>Applied date of the most recent application.</summary>
        public DateTime LatestAppliedDate { get; set; }
    }
}
