namespace JobPortal.Domain.Recommendations
{
    public sealed class Stage2TransitionContext
    {
        public string? Notes { get; }
        public string? Strengths { get; }
        public string? Concerns { get; }
        public bool? HireRecommendation { get; }
        public int? UserId { get; }

        private Stage2TransitionContext(
            string? notes, string? strengths, string? concerns, bool? hireRecommendation, int? userId)
        {
            Notes = notes;
            Strengths = strengths;
            Concerns = concerns;
            HireRecommendation = hireRecommendation;
            UserId = userId;
        }

        public static Stage2TransitionContext ForDraftSave(
            string? notes, string? strengths, string? concerns, bool? hireRecommendation)
            => new(notes, strengths, concerns, hireRecommendation, userId: null);

        public static Stage2TransitionContext ForSubmit(int userId)
            => new(null, null, null, null, userId);

        public static Stage2TransitionContext ForApproval(int userId)
            => new(null, null, null, null, userId);
    }
}
