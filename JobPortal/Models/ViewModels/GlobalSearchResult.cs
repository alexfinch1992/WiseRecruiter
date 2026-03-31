namespace JobPortal.Models.ViewModels
{
    public class GlobalSearchResult
    {
        public string Type { get; set; } = string.Empty; // "Candidate" or "Job"
        public int Id { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string SubText { get; set; } = string.Empty; // job title for Candidate results
    }
}
