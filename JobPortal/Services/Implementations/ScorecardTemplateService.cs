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
                .ThenInclude(tf => tf.ScorecardFacet)
                .ThenInclude(sf => sf!.Category)
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

            if (facets.Any(f => f.DisplayOrder <= 0))
                throw new InvalidOperationException("Display order must be a positive integer.");

            if (facets.GroupBy(f => f.FacetId).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Duplicate facet assignments are not allowed.");

            if (facets.GroupBy(f => f.DisplayOrder).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Duplicate display order values are not allowed within a template.");

            var requestedFacetIds = facets
                .Select(f => f.FacetId)
                .Distinct()
                .ToList();

            if (requestedFacetIds.Count > 0)
            {
                var existingFacetIds = await _context.ScorecardFacets
                    .Where(f => requestedFacetIds.Contains(f.Id))
                    .Select(f => f.Id)
                    .ToListAsync();

                if (existingFacetIds.Count != requestedFacetIds.Count)
                    throw new InvalidOperationException("One or more scorecard facets were not found.");
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
                    ScorecardFacetId = facet.FacetId,
                    DisplayOrder = facet.DisplayOrder
                    // Description, NotesPlaceholder, CategoryId are now stored on ScorecardFacet (global)
                });

                _context.ScorecardTemplateFacets.AddRange(replacements);
            }

            // Apply facet-level configuration globally to each ScorecardFacet
            var facetsWithFields = facets
                .Where(f => f.Description != null || f.NotesPlaceholder != null || f.CategoryId.HasValue)
                .ToList();

            if (facetsWithFields.Count > 0)
            {
                var facetIds = facetsWithFields.Select(f => f.FacetId).Distinct().ToList();
                var facetEntities = await _context.ScorecardFacets
                    .Where(f => facetIds.Contains(f.Id))
                    .ToDictionaryAsync(f => f.Id);

                foreach (var input in facetsWithFields)
                {
                    if (facetEntities.TryGetValue(input.FacetId, out var facetEntity))
                    {
                        if (input.Description != null)
                            facetEntity.Description = input.Description;
                        if (input.NotesPlaceholder != null)
                            facetEntity.NotesPlaceholder = input.NotesPlaceholder;
                        if (input.CategoryId.HasValue)
                            facetEntity.CategoryId = input.CategoryId;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<ScorecardTemplateFacet> AddFacetToTemplate(int templateId, int facetId, int displayOrder)
        {
            var templateExists = await _context.ScorecardTemplates.AnyAsync(t => t.Id == templateId);
            if (!templateExists)
                throw new InvalidOperationException("Scorecard template not found.");

            var facetExists = await _context.ScorecardFacets.AnyAsync(f => f.Id == facetId);
            if (!facetExists)
                throw new InvalidOperationException("Scorecard facet not found.");

            var duplicateExists = await _context.ScorecardTemplateFacets
                .AnyAsync(tf => tf.ScorecardTemplateId == templateId && tf.ScorecardFacetId == facetId);
            if (duplicateExists)
                throw new InvalidOperationException("Facet is already assigned to this template.");

            var templateFacet = new ScorecardTemplateFacet
            {
                ScorecardTemplateId = templateId,
                ScorecardFacetId = facetId,
                DisplayOrder = displayOrder
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

            var facetExists = await _context.ScorecardFacets.AnyAsync(f => f.Id == facetId);
            if (!facetExists)
                throw new InvalidOperationException("Scorecard facet not found.");

            var templateFacet = await _context.ScorecardTemplateFacets
                .FirstOrDefaultAsync(tf => tf.ScorecardTemplateId == templateId && tf.ScorecardFacetId == facetId);

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
                .Include(tf => tf.ScorecardFacet)
                .ThenInclude(sf => sf!.Category)
                .Where(tf => tf.ScorecardTemplateId == templateId)
                .OrderBy(tf => tf.ScorecardFacet!.Name)
                .ToListAsync();
        }
    }
}