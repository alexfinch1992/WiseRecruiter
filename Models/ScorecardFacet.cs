using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class ScorecardFacet
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }

        public ICollection<ScorecardTemplateFacet> TemplateFacets { get; set; } = new List<ScorecardTemplateFacet>();
    }
}