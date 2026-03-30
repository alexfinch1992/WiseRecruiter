namespace JobPortal.Models
{
    public class JobAssignment
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string UserId { get; set; } = string.Empty;

        public Job? Job { get; set; }
    }
}
