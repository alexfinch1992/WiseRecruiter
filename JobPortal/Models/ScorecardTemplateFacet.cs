namespace JobPortal.Models
{
    public class ScorecardTemplateFacet
    {
        public int Id { get; set; }

        public int ScorecardTemplateId { get; set; }

        public int ScorecardFacetId { get; set; }

        public int DisplayOrder { get; set; }

        // Optional guidance text shown to reviewers (soft limit ~500 chars)
        public string? Description { get; set; }

        // Optional UI hint for the notes input field
        public string? NotesPlaceholder { get; set; }

        // Optional category for analytics grouping
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public ScorecardTemplate? ScorecardTemplate { get; set; }

        public ScorecardFacet? ScorecardFacet { get; set; }
    }
}