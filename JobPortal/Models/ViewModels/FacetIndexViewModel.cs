namespace JobPortal.Models.ViewModels
{
    public class FacetIndexViewModel
    {
        public IEnumerable<Facet> Facets { get; set; } = [];
        public Dictionary<int, List<string>> TemplateNamesByFacetId { get; set; } = new();
    }
}
