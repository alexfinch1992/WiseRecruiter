namespace JobPortal.Models
{
    public class ScorecardTemplateFacet
    {
        public int Id { get; set; }

        public int ScorecardTemplateId { get; set; }

        public int FacetId { get; set; }

        // Legacy column — kept for DB compatibility, no longer used in code
        public int ScorecardFacetId { get; set; }

        // Legacy column — kept for DB compatibility; ordering now uses Facet.Name
        public int DisplayOrder { get; set; }

        public ScorecardTemplate? ScorecardTemplate { get; set; }

        public Facet? Facet { get; set; }
    }
}