using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IScorecardTemplateService
    {
        Task<List<ScorecardTemplate>> GetAllTemplates();
        Task<ScorecardTemplate?> GetTemplateById(int id);
        Task<ScorecardTemplate> GetDefaultTemplate();
        Task<ScorecardTemplate> CreateTemplate(string name);
        Task<ScorecardTemplate> UpdateTemplateName(int id, string name);
        Task UpdateTemplateFacets(int templateId, List<TemplateFacetInput> facets);
        Task<ScorecardTemplateFacet> AddFacetToTemplate(int templateId, int facetId, int displayOrder);
        Task RemoveFacetFromTemplate(int templateId, int facetId);
        Task<List<ScorecardTemplateFacet>> GetFacetsForTemplate(int templateId);
    }

    public class TemplateFacetInput
    {
        public int FacetId { get; set; }
        public int DisplayOrder { get; set; }
    }
}