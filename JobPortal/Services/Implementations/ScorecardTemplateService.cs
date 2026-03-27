using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ScorecardTemplateService : IScorecardTemplateService
    {
        private const string AtLeastOneFacetMessage = "A scorecard template must have at least one facet.";

        private readonly AppDbContext _context;

        public ScorecardTemplateService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<ScorecardTemplate>> GetAllTemplates()
        {
            return await _context.ScorecardTemplates
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<ScorecardTemplate?> GetTemplateById(int id)
        {
            return await _context.ScorecardTemplates
                .Include(t => t.TemplateFacets)
                .ThenInclude(tf => tf.Facet)
                .ThenInclude(f => f!.Category)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<ScorecardTemplate> GetDefaultTemplate()
        {
            var template = await _context.ScorecardTemplates
                .FirstOrDefaultAsync(t => t.Name == "Default Scorecard");

            if (template == null)
                throw new InvalidOperationException("Default scorecard template is missing.");

            return template;
        }

        public async Task<ScorecardTemplate> CreateTemplate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Template name is required.", nameof(name));

            var template = new ScorecardTemplate
            {
                Name = name.Trim()
            };

            _context.ScorecardTemplates.Add(template);
            await _context.SaveChangesAsync();
            return template;
        }

        public async Task<ScorecardTemplate> UpdateTemplateName(int id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Template name is required.", nameof(name));

            var template = await _context.ScorecardTemplates.FirstOrDefaultAsync(t => t.Id == id);
            if (template == null)
                throw new InvalidOperationException("Scorecard template not found.");

            template.Name = name.Trim();

            await _context.SaveChangesAsync();
            return template;
        }

        public async Task UpdateTemplateFacets(int templateId, List<TemplateFacetInput> facets)
        {
            var templateExists = await _context.ScorecardTemplates.AnyAsync(t => t.Id == templateId);
            if (!templateExists)
                throw new InvalidOperationException("Scorecard template not found.");

            facets ??= new List<TemplateFacetInput>();

            if (facets.Count == 0)
                throw new InvalidOperationException(AtLeastOneFacetMessage);

            if (facets.GroupBy(f => f.FacetId).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Duplicate facet assignments are not allowed.");

            var requestedFacetIds = facets
                .Select(f => f.FacetId)
                .Distinct()
                .ToList();

            if (requestedFacetIds.Count > 0)
            {
                var existingFacetIds = await _context.Facets
                    .Where(f => requestedFacetIds.Contains(f.Id))
                    .Select(f => f.Id)
                    .ToListAsync();

                if (existingFacetIds.Count != requestedFacetIds.Count)
                    throw new InvalidOperationException("One or more facets were not found.");
            }

            var existingAssignments = await _context.ScorecardTemplateFacets
                .Where(tf => tf.ScorecardTemplateId == templateId)
                .ToListAsync();

            _context.ScorecardTemplateFacets.RemoveRange(existingAssignments);

            if (facets.Count > 0)
            {
                var replacements = facets.Select(facet => new ScorecardTemplateFacet
                {
                    ScorecardTemplateId = templateId,
                    FacetId = facet.FacetId,
                    ScorecardFacetId = facet.FacetId // Legacy column — kept in sync for DB compatibility
                });

                _context.ScorecardTemplateFacets.AddRange(replacements);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<ScorecardTemplateFacet> AddFacetToTemplate(int templateId, int facetId)
        {
            var templateExists = await _context.ScorecardTemplates.AnyAsync(t => t.Id == templateId);
            if (!templateExists)
                throw new InvalidOperationException("Scorecard template not found.");

            var facetExists = await _context.Facets.AnyAsync(f => f.Id == facetId);
            if (!facetExists)
                throw new InvalidOperationException("Facet not found.");

            var duplicateExists = await _context.ScorecardTemplateFacets
                .AnyAsync(tf => tf.ScorecardTemplateId == templateId && tf.FacetId == facetId);
            if (duplicateExists)
                throw new InvalidOperationException("Facet is already assigned to this template.");

            var templateFacet = new ScorecardTemplateFacet
            {
                ScorecardTemplateId = templateId,
                FacetId = facetId,
                ScorecardFacetId = facetId // Legacy column — kept in sync
            };

            _context.ScorecardTemplateFacets.Add(templateFacet);
            await _context.SaveChangesAsync();
            return templateFacet;
        }

        public async Task RemoveFacetFromTemplate(int templateId, int facetId)
        {
            var templateExists = await _context.ScorecardTemplates.AnyAsync(t => t.Id == templateId);
            if (!templateExists)
                throw new InvalidOperationException("Scorecard template not found.");

            var facetExists = await _context.Facets.AnyAsync(f => f.Id == facetId);
            if (!facetExists)
                throw new InvalidOperationException("Facet not found.");

            var templateFacet = await _context.ScorecardTemplateFacets
                .FirstOrDefaultAsync(tf => tf.ScorecardTemplateId == templateId && tf.FacetId == facetId);

            if (templateFacet == null)
                return;

            _context.ScorecardTemplateFacets.Remove(templateFacet);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ScorecardTemplateFacet>> GetFacetsForTemplate(int templateId)
        {
            var templateExists = await _context.ScorecardTemplates.AnyAsync(t => t.Id == templateId);
            if (!templateExists)
                throw new InvalidOperationException("Scorecard template not found.");

            return await _context.ScorecardTemplateFacets
                .Include(tf => tf.Facet)
                .ThenInclude(f => f!.Category)
                .Where(tf => tf.ScorecardTemplateId == templateId)
                .OrderBy(tf => tf.Facet!.Name)
                .ToListAsync();
        }
    }
}