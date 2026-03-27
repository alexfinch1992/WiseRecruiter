namespace JobPortal.Models
{
    public class Interview
    {
        public int Id { get; set; }

        public int CandidateId { get; set; }

        public int ApplicationId { get; set; }

        public int JobStageId { get; set; }

        public DateTime ScheduledAt { get; set; }

        public bool IsCancelled { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public Candidate? Candidate { get; set; }
        public Application? Application { get; set; }
        public JobStage? JobStage { get; set; }
        public ICollection<InterviewInterviewer> InterviewInterviewers { get; set; } = new List<InterviewInterviewer>();
        public ICollection<Scorecard> Scorecards { get; set; } = new List<Scorecard>();
    }
}
