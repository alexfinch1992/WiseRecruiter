using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    public class EditTemplateFacetsViewModel
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public List<TemplateFacetSelectionViewModel> Facets { get; set; } = new();
        // Categories loaded from DB for the category dropdown
        public List<Category> Categories { get; set; } = new();
    }

    public class TemplateFacetSelectionViewModel
    {
        public int FacetId { get; set; }
        public string FacetName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public int DisplayOrder { get; set; }
        // Optional extended fields — all nullable for backward compatibility
        public string? Description { get; set; }
        public string? NotesPlaceholder { get; set; }
        public int? CategoryId { get; set; }
    }
}
