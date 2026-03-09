namespace JobPortal.Models
{
    public class JobStage
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }

        // Navigation properties
        public Job? Job { get; set; }
    }
}
