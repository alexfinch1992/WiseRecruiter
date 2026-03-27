namespace JobPortal.Models
{
    public class InterviewInterviewer
    {
        public int Id { get; set; }

        public int InterviewId { get; set; }

        public int AdminUserId { get; set; }

        // Navigation properties
        public Interview? Interview { get; set; }
        public AdminUser? AdminUser { get; set; }
    }
}
