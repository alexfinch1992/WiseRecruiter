namespace JobPortal.Models
{
    public class JobAlertSubscription
    {
        public int Id { get; set; }

        public int JobId { get; set; }
        public Job Job { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public bool IsEnabled { get; set; }
    }
}
