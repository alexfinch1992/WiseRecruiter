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
        private AppDbContext CreateInMemoryContext(string dbName = "facet_db")
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + Guid.NewGuid())
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateFacet_CanCreateFacet()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            var facet = await service.CreateFacet("Communication", 1);

            facet.Should().NotBeNull();
            facet.Id.Should().BeGreaterThan(0);
            facet.Name.Should().Be("Communication");
            facet.DisplayOrder.Should().Be(1);
            facet.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task CreateFacet_WithEmptyName_ThrowsArgumentException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);

            Func<Task> act = async () => await service.CreateFacet("   ", 1);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task CreateFacet_WithDuplicateName_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            await service.CreateFacet("Communication", 1);

            Func<Task> act = async () => await service.CreateFacet(" communication ", 2);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task CreateFacet_WithDuplicateDisplayOrder_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            await service.CreateFacet("Communication", 1);

            Func<Task> act = async () => await service.CreateFacet("Quality", 1);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*display order*");
        }

        [Fact]
        public async Task CreateFacet_WithUniqueDisplayOrder_Succeeds()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            await service.CreateFacet("Communication", 1);

            var created = await service.CreateFacet("Quality", 2);

            created.DisplayOrder.Should().Be(2);
            created.Name.Should().Be("Quality");

            var all = await service.GetAllFacets();
            all.Select(f => f.Name).Should().Equal("Communication", "Quality");
            all.Select(f => f.DisplayOrder).Should().Equal(1, 2);
        }

        [Fact]
        public async Task GetActiveFacets_ReturnsOnlyActiveInDisplayOrder()
        {
            using var context = CreateInMemoryContext();
            context.ScorecardFacets.AddRange(
                new ScorecardFacet { Name = "Quality", DisplayOrder = 2, IsActive = true },
                new ScorecardFacet { Name = "Archived", DisplayOrder = 1, IsActive = false },
                new ScorecardFacet { Name = "Communication", DisplayOrder = 1, IsActive = true });
            await context.SaveChangesAsync();

            var service = new FacetService(context);
            var active = await service.GetActiveFacets();

            active.Should().HaveCount(2);
            active.Select(f => f.Name).Should().Equal("Communication", "Quality");
        }

        [Fact]
        public async Task UpdateFacet_UpdatesAllEditableFields()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var facet = await service.CreateFacet("Communication", 1);

            var updated = await service.UpdateFacet(facet.Id, "Problem Solving", 3, false);

            updated.Name.Should().Be("Problem Solving");
            updated.DisplayOrder.Should().Be(3);
            updated.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeactivatedFacet_IsNotReturnedByGetActiveFacets()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var facet = await service.CreateFacet("Collaboration", 1);

            await service.UpdateFacet(facet.Id, facet.Name, facet.DisplayOrder, false);
            var active = await service.GetActiveFacets();

            active.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateFacet_ToDuplicateDisplayOrder_ThrowsInvalidOperationException()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var first = await service.CreateFacet("Communication", 1);
            var second = await service.CreateFacet("Quality", 2);

            Func<Task> act = async () => await service.UpdateFacet(second.Id, second.Name, 1, second.IsActive);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*display order*");
        }

        [Fact]
        public async Task UpdateFacet_ToUniqueDisplayOrder_Succeeds()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var first = await service.CreateFacet("Communication", 1);
            var second = await service.CreateFacet("Quality", 2);

            var updated = await service.UpdateFacet(second.Id, second.Name, 3, second.IsActive);

            updated.DisplayOrder.Should().Be(3);
            first.DisplayOrder.Should().Be(1);
        }

        [Fact]
        public async Task GetAllFacets_MaintainsOrderingAfterDisplayOrderUpdates()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var communication = await service.CreateFacet("Communication", 1);
            var quality = await service.CreateFacet("Quality", 2);
            var speed = await service.CreateFacet("Speed", 3);

            await service.UpdateFacet(quality.Id, quality.Name, 4, quality.IsActive);
            await service.UpdateFacet(speed.Id, speed.Name, 2, speed.IsActive);

            var ordered = await service.GetAllFacets();

            ordered.Select(f => f.Name).Should().Equal("Communication", "Speed", "Quality");
            ordered.Select(f => f.DisplayOrder).Should().Equal(1, 2, 4);
        }

        [Fact]
        public async Task GetActiveFacets_AfterDeactivationAndReorder_ExcludesInactiveAndKeepsOrder()
        {
            using var context = CreateInMemoryContext();
            var service = new FacetService(context);
            var communication = await service.CreateFacet("Communication", 1);
            var quality = await service.CreateFacet("Quality", 2);
            var speed = await service.CreateFacet("Speed", 3);

            await service.UpdateFacet(quality.Id, quality.Name, 4, false);
            await service.UpdateFacet(speed.Id, speed.Name, 2, true);

            var active = await service.GetActiveFacets();

            active.Select(f => f.Name).Should().Equal("Communication", "Speed");
            active.Select(f => f.DisplayOrder).Should().Equal(1, 2);
        }
    }
}
