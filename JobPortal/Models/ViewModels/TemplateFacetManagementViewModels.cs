using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    public class EditTemplateFacetsViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public List<TemplateFacetSelectionViewModel> Facets { get; set; } = new();
    }

    public class TemplateFacetSelectionViewModel
    {
        public int FacetId { get; set; }
        public string FacetName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
