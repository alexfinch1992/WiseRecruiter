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

        public async Task<List<ScorecardFacet>> GetActiveFacets()
        {
            return await _context.ScorecardFacets
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
        }

        public async Task<List<ScorecardFacet>> GetAllFacets()
        {
            return await _context.ScorecardFacets
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
        }

        public async Task<ScorecardFacet> CreateFacet(string name, int displayOrder)
        {
            var normalizedName = NormalizeName(name);

            var duplicateExists = await _context.ScorecardFacets
                .AnyAsync(f => f.Name.ToLower() == normalizedName.ToLower());
            if (duplicateExists)
                throw new InvalidOperationException("A scorecard facet with this name already exists.");

            var duplicateDisplayOrderExists = await _context.ScorecardFacets
                .AnyAsync(f => f.DisplayOrder == displayOrder);
            if (duplicateDisplayOrderExists)
                throw new InvalidOperationException("A scorecard facet with this display order already exists.");

            var facet = new ScorecardFacet
            {
                Name = normalizedName,
                DisplayOrder = displayOrder,
                IsActive = true
            };

            _context.ScorecardFacets.Add(facet);
            await _context.SaveChangesAsync();
            return facet;
        }

        public async Task<ScorecardFacet> UpdateFacet(int id, string name, int displayOrder, bool isActive)
        {
            var facet = await _context.ScorecardFacets.FirstOrDefaultAsync(f => f.Id == id);
            if (facet == null)
                throw new InvalidOperationException("Scorecard facet not found.");

            var normalizedName = NormalizeName(name);

            var duplicateExists = await _context.ScorecardFacets
                .AnyAsync(f => f.Id != id && f.Name.ToLower() == normalizedName.ToLower());
            if (duplicateExists)
                throw new InvalidOperationException("A scorecard facet with this name already exists.");

            var duplicateDisplayOrderExists = await _context.ScorecardFacets
                .AnyAsync(f => f.Id != id && f.DisplayOrder == displayOrder);
            if (duplicateDisplayOrderExists)
                throw new InvalidOperationException("A scorecard facet with this display order already exists.");

            facet.Name = normalizedName;
            facet.DisplayOrder = displayOrder;
            facet.IsActive = isActive;

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