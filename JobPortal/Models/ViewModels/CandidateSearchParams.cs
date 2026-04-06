namespace JobPortal.Models.ViewModels
{
    public class CandidateSearchParams
    {
        public string? SearchQuery { get; set; }
        public List<int>? JobIds { get; set; }
        public List<string>? Locations { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string Sort { get; set; } = "date";
        public string Dir { get; set; } = "desc";
    }
}
