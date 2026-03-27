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
    public class FacetServiceTests
    {
        private AppDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "facet_db_" + Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateFacet_CanCreateFacet()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var facet = await service.CreateFacet("Communication");

            facet.Should().NotBeNull();
            facet.Id.Should().BeGreaterThan(0);
            facet.Name.Should().Be("Communication");
        }

        [Fact]
        public async Task CreateFacet_WithEmptyName_ThrowsArgumentException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            Func<Task> act = async () => await service.CreateFacet("   ");

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task CreateFacet_WithDuplicateName_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            await service.CreateFacet("Communication");

            Func<Task> act = async () => await service.CreateFacet(" communication ");

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task CreateFacet_PersistsDescriptionNotesPlaceholderAndCategory()
        {
            using var context = CreateInMemoryContext();
            var category = new Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var service = new FacetService(context);
            var facet = await service.CreateFacet("Problem Solving");
            await service.UpdateFacet(facet.Id, facet.Name,
                "Assess structured thinking", "e.g. used divide-and-conquer", category.Id);

            var loaded = await service.GetFacetById(facet.Id);

            loaded.Should().NotBeNull();
            loaded!.Description.Should().Be("Assess structured thinking");
            loaded.NotesPlaceholder.Should().Be("e.g. used divide-and-conquer");
            loaded.CategoryId.Should().Be(category.Id);
            loaded.Category!.Name.Should().Be("Technical");
        }

        [Fact]
        public async Task GetAllFacets_ReturnsAllFacetsAlphabetically()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            await service.CreateFacet("Quality");
            await service.CreateFacet("Communication");
            await service.CreateFacet("Speed");

            var all = await service.GetAllFacets();

            all.Select(f => f.Name).Should().Equal("Communication", "Quality", "Speed");
        }

        [Fact]
        public async Task GetFacetById_ReturnsCorrectFacet()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var created = await service.CreateFacet("Communication");

            var found = await service.GetFacetById(created.Id);

            found.Should().NotBeNull();
            found!.Name.Should().Be("Communication");
        }

        [Fact]
        public async Task GetFacetById_WithInvalidId_ReturnsNull()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var result = await service.GetFacetById(9999);

            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateFacet_UpdatesNameAndFields()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var facet = await service.CreateFacet("Communication");

            var updated = await service.UpdateFacet(facet.Id, "Problem Solving",
                "Rate their approach", "e.g. clear steps", null);

            updated.Name.Should().Be("Problem Solving");
            updated.Description.Should().Be("Rate their approach");
            updated.NotesPlaceholder.Should().Be("e.g. clear steps");
            updated.CategoryId.Should().BeNull();
        }

        [Fact]
        public async Task UpdateFacet_WithDuplicateName_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            await service.CreateFacet("Communication");
            var second = await service.CreateFacet("Quality");

            Func<Task> act = async () =>
                await service.UpdateFacet(second.Id, "Communication", null, null, null);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateFacet_WithInvalidId_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            Func<Task> act = async () =>
                await service.UpdateFacet(9999, "Name", null, null, null);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task GetAllFacets_WithNoFacets_ReturnsEmpty()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var facets = await service.GetAllFacets();

            facets.Should().BeEmpty();
        }
    }
}
