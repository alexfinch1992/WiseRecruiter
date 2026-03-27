using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IFacetService
    {
        Task<List<Facet>> GetAllFacets();
        Task<Facet?> GetFacetById(int id);
        Task<Facet> CreateFacet(string name);
        Task<Facet> UpdateFacet(int id, string name, string? description, string? notesPlaceholder, int? categoryId);
    }
}