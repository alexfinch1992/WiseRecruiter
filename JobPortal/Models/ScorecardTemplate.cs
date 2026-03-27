using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class ScorecardTemplate
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public ICollection<ScorecardTemplateFacet> TemplateFacets { get; set; } = new List<ScorecardTemplateFacet>();
        public ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}