namespace JobPortal.Domain.HiringRequests
{
    /// <summary>Carries the data needed for a Stage 1 (Senior Talent Lead) transition.</summary>
    public sealed class Stage1HiringRequestTransitionContext
    {
        // Content fields — populated only for draft saves
        public string? RoleTitle { get; }
        public string? Department { get; }
        public string? LevelBand { get; }
        public string? Location { get; }
        public bool? IsReplacement { get; }
        public string? ReplacementReason { get; }
        public int? Headcount { get; }
        public string? Justification { get; }

        // Actor and decision fields
        public string? UserId { get; }
        public string? Notes { get; }
        public string? RejectionReason { get; }

        private Stage1HiringRequestTransitionContext(
            string? roleTitle, string? department, string? levelBand, string? location,
            bool? isReplacement, string? replacementReason, int? headcount, string? justification,
            string? userId, string? notes, string? rejectionReason)
        {
            RoleTitle = roleTitle;
            Department = department;
            LevelBand = levelBand;
            Location = location;
            IsReplacement = isReplacement;
            ReplacementReason = replacementReason;
            Headcount = headcount;
            Justification = justification;
            UserId = userId;
            Notes = notes;
            RejectionReason = rejectionReason;
        }

        public static Stage1HiringRequestTransitionContext ForDraftSave(
            string? roleTitle, string? department, string? levelBand, string? location,
            bool? isReplacement, string? replacementReason, int? headcount, string? justification)
            => new(roleTitle, department, levelBand, location, isReplacement, replacementReason,
                   headcount, justification, userId: null, notes: null, rejectionReason: null);

        public static Stage1HiringRequestTransitionContext ForSubmit(string userId)
            => new(null, null, null, null, null, null, null, null, userId, notes: null, rejectionReason: null);

        public static Stage1HiringRequestTransitionContext ForApproval(string userId, string? notes = null)
            => new(null, null, null, null, null, null, null, null, userId, notes, rejectionReason: null);

        public static Stage1HiringRequestTransitionContext ForRejection(string userId, string? reason = null)
            => new(null, null, null, null, null, null, null, null, userId, notes: null, rejectionReason: reason);
    }
}
