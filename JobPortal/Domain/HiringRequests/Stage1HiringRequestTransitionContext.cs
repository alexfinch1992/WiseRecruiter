using JobPortal.Models;

namespace JobPortal.Domain.HiringRequests
{
    /// <summary>Carries the data needed for a Stage 1 (Senior Talent Lead) transition.</summary>
    public sealed class Stage1HiringRequestTransitionContext
    {
        // Content fields — populated only for draft saves
        public string? JobTitle { get; }
        public string? Department { get; }
        public int? Headcount { get; }
        public string? Justification { get; }
        public string? SalaryBand { get; }
        public DateTime? TargetStartDate { get; }
        public EmploymentType? EmploymentType { get; }
        public HiringPriority? Priority { get; }

        // Actor and decision fields
        public int? UserId { get; }
        public string? Feedback { get; }
        public string? RejectionReason { get; }

        private Stage1HiringRequestTransitionContext(
            string? jobTitle, string? department, int? headcount, string? justification,
            string? salaryBand, DateTime? targetStartDate, EmploymentType? employmentType,
            HiringPriority? priority, int? userId, string? feedback, string? rejectionReason)
        {
            JobTitle = jobTitle;
            Department = department;
            Headcount = headcount;
            Justification = justification;
            SalaryBand = salaryBand;
            TargetStartDate = targetStartDate;
            EmploymentType = employmentType;
            Priority = priority;
            UserId = userId;
            Feedback = feedback;
            RejectionReason = rejectionReason;
        }

        public static Stage1HiringRequestTransitionContext ForDraftSave(
            string? jobTitle, string? department, int? headcount, string? justification,
            string? salaryBand, DateTime? targetStartDate, EmploymentType? employmentType,
            HiringPriority? priority)
            => new(jobTitle, department, headcount, justification, salaryBand, targetStartDate,
                   employmentType, priority, userId: null, feedback: null, rejectionReason: null);

        public static Stage1HiringRequestTransitionContext ForSubmit(int userId)
            => new(null, null, null, null, null, null, null, null, userId, feedback: null, rejectionReason: null);

        public static Stage1HiringRequestTransitionContext ForApproval(int userId, string? feedback = null)
            => new(null, null, null, null, null, null, null, null, userId, feedback, rejectionReason: null);

        public static Stage1HiringRequestTransitionContext ForRejection(int userId, string? reason = null)
            => new(null, null, null, null, null, null, null, null, userId, feedback: null, rejectionReason: reason);

        public static Stage1HiringRequestTransitionContext ForNeedsRevision(int userId, string? feedback = null)
            => new(null, null, null, null, null, null, null, null, userId, feedback, rejectionReason: null);
    }
}
