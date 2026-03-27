using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class ScorecardTemplateServiceTests
    {
        private AppDbContext CreateInMemoryContext(string dbName = "template_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateTemplate_CanCreateTemplate()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);

            var template = await service.CreateTemplate("Default Scorecard");

            template.Should().NotBeNull();
            template.Id.Should().BeGreaterThan(0);
            template.Name.Should().Be("Default Scorecard");
        }

        [Fact]
        public async Task CreateTemplate_WithEmptyName_ThrowsArgumentException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);

            Func<Task> act = async () => await service.CreateTemplate("  ");

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task GetDefaultTemplate_WhenMissing_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);

            Func<Task> act = async () => await service.GetDefaultTemplate();

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task AddFacetToTemplate_CanAddFacetToTemplate()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var facet = new Facet { Name = "Communication" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            var link = await service.AddFacetToTemplate(template.Id, facet.Id, 1);

            link.Should().NotBeNull();
            link.ScorecardTemplateId.Should().Be(template.Id);
            link.FacetId.Should().Be(facet.Id);
            link.DisplayOrder.Should().Be(1);

            var assigned = await service.GetFacetsForTemplate(template.Id);
            assigned.Should().HaveCount(1);
            assigned.Single().Facet!.Name.Should().Be("Communication");
        }

        [Fact]
        public async Task AddFacetToTemplate_CannotAddSameFacetTwice()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var facet = new Facet { Name = "Communication" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();
            await service.AddFacetToTemplate(template.Id, facet.Id, 1);

            Func<Task> act = async () => await service.AddFacetToTemplate(template.Id, facet.Id, 2);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task RemoveFacetFromTemplate_CanRemoveFacetFromTemplate()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var facet = new Facet { Name = "Communication" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();
            await service.AddFacetToTemplate(template.Id, facet.Id, 1);

            await service.RemoveFacetFromTemplate(template.Id, facet.Id);

            var remaining = await service.GetFacetsForTemplate(template.Id);
            remaining.Should().BeEmpty();
        }

        [Fact]
        public async Task GetFacetsForTemplate_ReturnsOrderedFacets()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var facetA = new Facet { Name = "A" };
            var facetB = new Facet { Name = "B" };
            context.Facets.AddRange(facetA, facetB);
            await context.SaveChangesAsync();

            await service.AddFacetToTemplate(template.Id, facetB.Id, 2);
            await service.AddFacetToTemplate(template.Id, facetA.Id, 1);

            var facets = await service.GetFacetsForTemplate(template.Id);

            facets.Select(f => f.Facet!.Name).Should().Equal("A", "B");
        }

        [Fact]
        public async Task AddFacetToTemplate_WithInvalidTemplateId_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var facet = new Facet { Name = "Communication" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            Func<Task> act = async () => await service.AddFacetToTemplate(9999, facet.Id, 1);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task AddFacetToTemplate_WithInvalidFacetId_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");

            Func<Task> act = async () => await service.AddFacetToTemplate(template.Id, 9999, 1);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateTemplateName_CanUpdateTemplateName()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");

            var updated = await service.UpdateTemplateName(template.Id, "Technical Scorecard");

            updated.Name.Should().Be("Technical Scorecard");
            var fromDb = await service.GetTemplateById(template.Id);
            fromDb.Should().NotBeNull();
            fromDb!.Name.Should().Be("Technical Scorecard");
        }

        [Fact]
        public async Task UpdateTemplateFacets_CanAssignMultipleFacets()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            var quality = new Facet { Name = "Quality" };
            context.Facets.AddRange(communication, quality);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = quality.Id, DisplayOrder = 2 },
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 }
            });

            var assigned = await service.GetFacetsForTemplate(template.Id);
            assigned.Should().HaveCount(2);
            assigned.Select(x => x.FacetId).Should().Equal(communication.Id, quality.Id);
        }

        [Fact]
        public async Task UpdateTemplateFacets_ReplacesExistingAssignments()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            var quality = new Facet { Name = "Quality" };
            var speed = new Facet { Name = "Speed" };
            context.Facets.AddRange(communication, quality, speed);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 },
                new TemplateFacetInput { FacetId = quality.Id, DisplayOrder = 2 }
            });

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = speed.Id, DisplayOrder = 1 }
            });

            var assigned = await service.GetFacetsForTemplate(template.Id);
            assigned.Should().HaveCount(1);
            assigned[0].FacetId.Should().Be(speed.Id);
            assigned[0].DisplayOrder.Should().Be(1);
        }

        [Fact]
        public async Task UpdateTemplateFacets_ReorderAndRemoveFacet_UsesNewSetExactly()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            var quality = new Facet { Name = "Quality" };
            var speed = new Facet { Name = "Speed" };
            context.Facets.AddRange(communication, quality, speed);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 },
                new TemplateFacetInput { FacetId = quality.Id, DisplayOrder = 2 },
                new TemplateFacetInput { FacetId = speed.Id, DisplayOrder = 3 }
            });

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = speed.Id, DisplayOrder = 1 },
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 2 }
            });

            var assigned = await service.GetFacetsForTemplate(template.Id);

            // Ordering is by facet name (alphabetical), not DisplayOrder
            assigned.Select(x => x.Facet!.Name).Should().Equal("Communication", "Speed");
            assigned.Select(x => x.DisplayOrder).Should().Equal(2, 1);
            assigned.Select(x => x.Facet!.Name).Should().NotContain("Quality");
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithDuplicateFacetIds_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            context.Facets.Add(communication);
            await context.SaveChangesAsync();

            Func<Task> act = async () => await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 },
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 2 }
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithDuplicateDisplayOrders_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            var quality = new Facet { Name = "Quality" };
            context.Facets.AddRange(communication, quality);
            await context.SaveChangesAsync();

            Func<Task> act = async () => await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 },
                new TemplateFacetInput { FacetId = quality.Id, DisplayOrder = 1 }
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithInvalidTemplateId_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);

            Func<Task> act = async () => await service.UpdateTemplateFacets(9999, new());

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithInvalidFacetId_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");

            Func<Task> act = async () => await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = 9999, DisplayOrder = 1 }
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task UpdateTemplateFacets_PreservesOrderingWhenRetrieved()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            var quality = new Facet { Name = "Quality" };
            context.Facets.AddRange(communication, quality);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = quality.Id, DisplayOrder = 2 },
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 }
            });

            var assigned = await service.GetFacetsForTemplate(template.Id);
            assigned.Select(x => x.DisplayOrder).Should().Equal(1, 2);
            assigned.Select(x => x.FacetId).Should().Equal(communication.Id, quality.Id);
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithEmptyList_ThrowsAndPreservesExistingAssignments()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            context.Facets.Add(communication);
            await context.SaveChangesAsync();
            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 1 }
            });

            Func<Task> act = async () => await service.UpdateTemplateFacets(template.Id, new());

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("A scorecard template must have at least one facet.");

            var assigned = await service.GetFacetsForTemplate(template.Id);
            assigned.Should().HaveCount(1);
            assigned[0].FacetId.Should().Be(communication.Id);
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithNonPositiveDisplayOrder_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Default Scorecard");
            var communication = new Facet { Name = "Communication" };
            context.Facets.Add(communication);
            await context.SaveChangesAsync();

            Func<Task> act = async () => await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = communication.Id, DisplayOrder = 0 }
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // --- Category seeding tests ---

        [Fact]
        public async Task Categories_SeedData_ExistsInDatabase()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "category_seed_db_" + Guid.NewGuid())
                .Options;

            // Seed data from OnModelCreating is not applied by InMemory provider,
            // so we seed manually to match the same data that the migration applies.
            using var context = new AppDbContext(options);
            context.Categories.AddRange(
                new Category { Id = 1, Name = "Technical" },
                new Category { Id = 2, Name = "Soft Skills" },
                new Category { Id = 3, Name = "Leadership" }
            );
            await context.SaveChangesAsync();

            var categories = await context.Categories.OrderBy(c => c.Id).ToListAsync();
            categories.Should().HaveCount(3);
            categories[0].Name.Should().Be("Technical");
            categories[1].Name.Should().Be("Soft Skills");
            categories[2].Name.Should().Be("Leadership");
        }

        // --- Extended TemplateFacet fields tests ---

        [Fact]
        public async Task UpdateTemplateFacets_WithDescriptionAndNotesPlaceholder_PersistsValues()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Engineering");
            var facet = new Facet { Name = "Technical Skill" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput
                {
                    FacetId = facet.Id,
                    DisplayOrder = 1,
                    Description = "Rate the candidate's core technical ability.",
                    NotesPlaceholder = "e.g. Solved the algorithm problem in O(n log n)"
                }
            });

            // Description and NotesPlaceholder are stored on Facet (globally)
            var facetEntity = await context.Facets.FirstAsync(f => f.Name == "Technical Skill");

            facetEntity.Description.Should().Be("Rate the candidate's core technical ability.");
            facetEntity.NotesPlaceholder.Should().Be("e.g. Solved the algorithm problem in O(n log n)");
            facetEntity.CategoryId.Should().BeNull();
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithCategoryId_PersistsCategoryId()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Engineering");
            var facet = new Facet { Name = "Communication" };
            var category = new Category { Name = "Soft Skills" };
            context.Facets.Add(facet);
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = facet.Id, DisplayOrder = 1, CategoryId = category.Id }
            });

            // CategoryId is stored on Facet (globally)
            var facetEntity = await context.Facets.FirstAsync(f => f.Name == "Communication");

            facetEntity.CategoryId.Should().Be(category.Id);
        }

        [Fact]
        public async Task UpdateTemplateFacets_WithoutNewFields_LeavesFieldsNull()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Minimal");
            var facet = new Facet { Name = "Ownership" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            await service.UpdateTemplateFacets(template.Id, new()
            {
                new TemplateFacetInput { FacetId = facet.Id, DisplayOrder = 1 }
            });

            // Fields with no input values should remain null on Facet
            var facetEntity = await context.Facets.FirstAsync(f => f.Name == "Ownership");

            facetEntity.Description.Should().BeNull();
            facetEntity.NotesPlaceholder.Should().BeNull();
            facetEntity.CategoryId.Should().BeNull();
        }

        // --- Part 7: Facet-level field and ordering tests ---

        [Fact]
        public async Task Facet_StoresDescription_NotesPlaceholder_CategoryId()
        {
            using var context = CreateInMemoryContext();
            var category = new Category { Name = "Technical" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var facet = new Facet {
                Name = "Problem Solving",
                Description = "Assess systematic approach to problems.",
                NotesPlaceholder = "e.g. Used divide and conquer",
                CategoryId = category.Id
            };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            var loaded = await context.Facets
                .Include(f => f.Category)
                .FirstAsync(f => f.Id == facet.Id);

            loaded.Description.Should().Be("Assess systematic approach to problems.");
            loaded.NotesPlaceholder.Should().Be("e.g. Used divide and conquer");
            loaded.CategoryId.Should().Be(category.Id);
            loaded.Category!.Name.Should().Be("Technical");
        }

        [Fact]
        public async Task GetFacetsForTemplate_UsesFacetValues_NotTemplateFacetValues()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var category = new Category { Name = "Soft Skills" };
            context.Categories.Add(category);

            var facet = new Facet
            {
                Name = "Communication",
                Description = "Global description from Facet.",
                NotesPlaceholder = "Global placeholder.",
                CategoryId = category.Id
            };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();

            var template = await service.CreateTemplate("Interview");
            await service.AddFacetToTemplate(template.Id, facet.Id, 1);

            var templateFacets = await service.GetFacetsForTemplate(template.Id);

            templateFacets.Should().HaveCount(1);
            var loaded = templateFacets.Single();
            // Values should come from the Facet entity, not ScorecardTemplateFacet
            loaded.Facet!.Description.Should().Be("Global description from Facet.");
            loaded.Facet.NotesPlaceholder.Should().Be("Global placeholder.");
            loaded.Facet.CategoryId.Should().Be(category.Id);
            loaded.Facet.Category!.Name.Should().Be("Soft Skills");
        }

        [Fact]
        public async Task GetFacetsForTemplate_ReturnsAlphabeticalOrder()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);
            var template = await service.CreateTemplate("Hiring");

            var zebra = new Facet { Name = "Zebra" };
            var apple = new Facet { Name = "Apple" };
            var mango = new Facet { Name = "Mango" };
            context.Facets.AddRange(zebra, apple, mango);
            await context.SaveChangesAsync();

            await service.AddFacetToTemplate(template.Id, zebra.Id, 1);
            await service.AddFacetToTemplate(template.Id, mango.Id, 3);
            await service.AddFacetToTemplate(template.Id, apple.Id, 2);

            var facets = await service.GetFacetsForTemplate(template.Id);

            facets.Select(f => f.Facet!.Name).Should().Equal("Apple", "Mango", "Zebra");
        }

        [Fact]
        public async Task ExistingTemplates_LoadWithoutError_BackwardCompatibility()
        {
            using var context = CreateInMemoryContext();
            var service = new ScorecardTemplateService(context);

            // Simulate an existing template with facets that have no new fields set
            var template = await service.CreateTemplate("Legacy Template");
            var facet = new Facet { Name = "Legacy Facet" };
            context.Facets.Add(facet);
            await context.SaveChangesAsync();
            await service.AddFacetToTemplate(template.Id, facet.Id, 1);

            Func<Task> act = async () => await service.GetFacetsForTemplate(template.Id);

            await act.Should().NotThrowAsync();

            var facets = await service.GetFacetsForTemplate(template.Id);
            facets.Should().HaveCount(1);
            facets.Single().Facet!.Description.Should().BeNull();
            facets.Single().Facet!.NotesPlaceholder.Should().BeNull();
            facets.Single().Facet!.CategoryId.Should().BeNull();
        }
    }
}
