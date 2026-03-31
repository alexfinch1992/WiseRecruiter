namespace JobPortal.Services.Models
{
    public enum InterviewCreateError
    {
        None,
        InvalidApplication,
        InvalidStageFormat,
        InvalidInterviewer,
        Unknown
    }

    public class InterviewCreateResult
    {
        public bool                 Success       { get; init; }
        public InterviewCreateError Error         { get; init; }
        public int                  ApplicationId { get; init; }  // meaningful when Success = true
    }
}
