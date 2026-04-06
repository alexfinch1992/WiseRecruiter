namespace JobPortal.Models.ViewModels
{
    public class SearchCandidatesViewModel
    {
        public List<Application> Applications { get; set; } = [];
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
    }
}
