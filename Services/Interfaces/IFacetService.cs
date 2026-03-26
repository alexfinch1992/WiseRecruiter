using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IFacetService
    {
        Task<List<ScorecardFacet>> GetActiveFacets();
        Task<List<ScorecardFacet>> GetAllFacets();
        Task<ScorecardFacet> CreateFacet(string name, int displayOrder);
        Task<ScorecardFacet> UpdateFacet(int id, string name, int displayOrder, bool isActive);
    }
}