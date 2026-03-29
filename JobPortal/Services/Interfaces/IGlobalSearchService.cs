using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    public interface IGlobalSearchService
    {
        Task<List<GlobalSearchResult>> SearchAsync(string query);
    }
}
