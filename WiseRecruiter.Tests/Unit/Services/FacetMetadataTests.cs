using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    /// <summary>
    /// Behaviour guards: Facet is the single source of truth for metadata.
    /// These tests will fail if someone moves metadata back to templates.
    /// </summary>
    public class FacetMetadataTests
    {
        private AppDbContext CreateInMemoryContext() =>
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("facet_metadata_" + Guid.NewGuid())
                .Options);

        // -------------------------------------------------------
        // Suite 1 — Facet Creation & Persistence
        // -------------------------------------------------------

        [Fact]
        public async Task CreateFacet_Persists_All_Metadata()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var category = new Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = await service.CreateFacet("Problem Solving");
            await service.UpdateFacet(facet.Id, facet.Name,
                "Assess structured approach", "e.g. used divide and conquer", category.Id);

            var loaded = await context.Facets.Include(f => f.Category).FirstAsync(f => f.Id == facet.Id);

            loaded.Description.Should().Be("Assess structured approach");
            loaded.NotesPlaceholder.Should().Be("e.g. used divide and conquer");
            loaded.CategoryId.Should().Be(category.Id);
            loaded.Category!.Name.Should().Be("Technical");
        }

        [Fact]
        public async Task CreateFacet_Allows_Null_Optional_Fields()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var facet = await service.CreateFacet("Communication");
            // Do NOT call UpdateFacet — leave all optional fields at their defaults

            var loaded = await context.Facets.FirstAsync(f => f.Id == facet.Id);

            loaded.Description.Should().BeNull();
            loaded.NotesPlaceholder.Should().BeNull();
            loaded.CategoryId.Should().BeNull();
        }

        [Fact]
        public async Task CreateFacet_ExplicitlyNull_Optional_Fields_Persist_As_Null()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var facet = await service.CreateFacet("Leadership");
            await service.UpdateFacet(facet.Id, facet.Name, null, null, null);

            var loaded = await context.Facets.FirstAsync(f => f.Id == facet.Id);

            loaded.Description.Should().BeNull();
            loaded.NotesPlaceholder.Should().BeNull();
            loaded.CategoryId.Should().BeNull();
        }

        // -------------------------------------------------------
        // Suite 2 — Facet Editing
        // -------------------------------------------------------

        [Fact]
        public async Task UpdateFacet_Updates_Metadata_Fields()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var category = new Category { Name = "Soft Skills" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = await service.CreateFacet("Communication");
            await service.UpdateFacet(facet.Id, facet.Name,
                "Assess clarity", "e.g. explained concisely", category.Id);

            var loaded = await context.Facets.FirstAsync(f => f.Id == facet.Id);

            loaded.Description.Should().Be("Assess clarity");
            loaded.NotesPlaceholder.Should().Be("e.g. explained concisely");
            loaded.CategoryId.Should().Be(category.Id);
        }

        [Fact]
        public async Task UpdateFacet_DoesNotAffect_OtherFacets()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var facetA = await service.CreateFacet("Communication");
            var facetB = await service.CreateFacet("Quality");

            await service.UpdateFacet(facetA.Id, facetA.Name,
                "Updated description", "Updated placeholder", null);

            var loadedB = await context.Facets.FirstAsync(f => f.Id == facetB.Id);

            loadedB.Description.Should().BeNull();
            loadedB.NotesPlaceholder.Should().BeNull();
            loadedB.CategoryId.Should().BeNull();
        }

        [Fact]
        public async Task UpdateFacet_CanClearCategory_By_Setting_Null()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var category = new Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = await service.CreateFacet("Analysis");
            await service.UpdateFacet(facet.Id, facet.Name, "desc", "placeholder", category.Id);
            await service.UpdateFacet(facet.Id, facet.Name, null, null, null);

            var loaded = await context.Facets.FirstAsync(f => f.Id == facet.Id);

            loaded.CategoryId.Should().BeNull();
            loaded.Description.Should().BeNull();
            loaded.NotesPlaceholder.Should().BeNull();
        }
    }
}
