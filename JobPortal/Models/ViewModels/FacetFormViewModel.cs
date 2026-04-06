namespace JobPortal.Models.ViewModels
{
    public class FacetFormViewModel
    {
        public Facet? Facet { get; set; }
        public List<Category> Categories { get; set; } = [];
    }
}
