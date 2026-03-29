namespace JobPortal.Models.ViewModels
{
    public class GlobalSearchResult
    {
        public string Type { get; set; } = string.Empty; // "Candidate" or "Job"
        public int Id { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
