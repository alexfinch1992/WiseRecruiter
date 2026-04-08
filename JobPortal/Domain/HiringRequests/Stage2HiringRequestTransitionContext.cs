namespace JobPortal.Domain.HiringRequests
{
    /// <summary>Carries the data needed for a Stage 2 (Senior Executive) transition.</summary>
    public sealed class Stage2HiringRequestTransitionContext
    {
        public string? UserId { get; }
        public string? Notes { get; }
        public string? RejectionReason { get; }

        private Stage2HiringRequestTransitionContext(string? userId, string? notes, string? rejectionReason)
        {
            UserId = userId;
            Notes = notes;
            RejectionReason = rejectionReason;
        }

        public static Stage2HiringRequestTransitionContext ForApproval(string userId, string? notes = null)
            => new(userId, notes, rejectionReason: null);

        public static Stage2HiringRequestTransitionContext ForRejection(string userId, string? reason = null)
            => new(userId, notes: null, rejectionReason: reason);
    }
}
