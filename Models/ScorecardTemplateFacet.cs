namespace JobPortal.Models
{
    public class ScorecardTemplateFacet
    {
        public int Id { get; set; }

        public int ScorecardTemplateId { get; set; }

        public int ScorecardFacetId { get; set; }

        public int DisplayOrder { get; set; }

        public ScorecardTemplate? ScorecardTemplate { get; set; }

        public ScorecardFacet? ScorecardFacet { get; set; }
    }
}