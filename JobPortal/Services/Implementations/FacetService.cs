using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class FacetService : IFacetService
    {
        private readonly AppDbContext _context;

        public FacetService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Facet>> GetAllFacets()
        {
            return await _context.Facets
                .Include(f => f.Category)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }

        public async Task<Facet?> GetFacetById(int id)
        {
            return await _context.Facets
                .Include(f => f.Category)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<Facet> CreateFacet(string name)
        {
            var normalizedName = NormalizeName(name);

            if (await _context.Facets.AnyAsync(f => f.Name.ToLower() == normalizedName.ToLower()))
                throw new InvalidOperationException("A facet with this name already exists.");

            var facet = new Facet { Name = normalizedName };
            _context.Facets.Add(facet);
            await _context.SaveChangesAsync();
            return facet;
        }

        public async Task<Facet> UpdateFacet(int id, string name, string? description, string? notesPlaceholder, int? categoryId)
        {
            var facet = await _context.Facets.FirstOrDefaultAsync(f => f.Id == id)
                ?? throw new InvalidOperationException("Facet not found.");

            var normalizedName = NormalizeName(name);

            if (await _context.Facets.AnyAsync(f => f.Id != id && f.Name.ToLower() == normalizedName.ToLower()))
                throw new InvalidOperationException("A facet with this name already exists.");

            facet.Name = normalizedName;
            facet.Description = description;
            facet.NotesPlaceholder = notesPlaceholder;
            facet.CategoryId = categoryId;
            await _context.SaveChangesAsync();
            return facet;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Facet name is required.", nameof(name));
            return name.Trim();
        }
    }
}