namespace JobPortal.Models.ViewModels
{
    public class CandidateListItemDto
    {
        public int ApplicationId { get; set; }

        public string Name { get; set; } = "";
        public string Email { get; set; } = "";

        public string? City { get; set; }

        public string Stage { get; set; } = "";

        public int JobId { get; set; }
        public string JobTitle { get; set; } = "";

        public DateTime AppliedDate { get; set; }
    }
}
