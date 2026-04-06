namespace JobPortal.Models.ViewModels
{
    public class CandidateSearchResultViewModel
    {
        public List<CandidateListItemDto> Applications { get; set; } = new();
        public int TotalCount { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }

        public List<FacetItem> LocationFacets { get; set; } = new();
        public List<FacetItem> JobFacets { get; set; } = new();

        public CandidateSearchParams Params { get; set; } = new();
    }

    public class FacetItem
    {
        public string Value { get; set; } = "";
        public string? Label { get; set; }
        public int Count { get; set; }
    }
}
