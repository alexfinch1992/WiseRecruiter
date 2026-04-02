namespace JobPortal.Models
{
    public class JobUser
    {
        public int Id { get; set; }

        public int JobId { get; set; }
        public Job Job { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        // Contextual role within the job (NOT Identity role)
        public string Role { get; set; } // "Owner" or "Recruiter"

        public bool IsActive { get; set; } = true;
    }
}
