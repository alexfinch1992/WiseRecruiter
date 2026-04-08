namespace JobPortal.Domain.HiringRequests
{
    /// <summary>Carries the data needed for a Stage 2 (Senior Executive) transition.</summary>
    public sealed class Stage2HiringRequestTransitionContext
    {
        public int? UserId { get; }
        public string? Feedback { get; }
        public string? RejectionReason { get; }

        private Stage2HiringRequestTransitionContext(int? userId, string? feedback, string? rejectionReason)
        {
            UserId = userId;
            Feedback = feedback;
            RejectionReason = rejectionReason;
        }

        public static Stage2HiringRequestTransitionContext ForSubmit(int userId)
            => new(userId, feedback: null, rejectionReason: null);

        public static Stage2HiringRequestTransitionContext ForApproval(int userId, string? feedback = null)
            => new(userId, feedback, rejectionReason: null);

        public static Stage2HiringRequestTransitionContext ForRejection(int userId, string? reason = null)
            => new(userId, feedback: null, rejectionReason: reason);

        public static Stage2HiringRequestTransitionContext ForNeedsRevision(int userId, string? feedback = null)
            => new(userId, feedback, rejectionReason: null);
    }
}
