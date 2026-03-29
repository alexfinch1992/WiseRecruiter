using FluentAssertions;
using JobPortal.Models;
using JobPortal.Services.Implementations;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    public class StageOrderServiceTests
    {
        private static StageOrderService CreateService() => new StageOrderService();

        [Fact]
        public void GetOrderedStages_ReturnsExpectedPipeline()
        {
            var service = CreateService();

            var stages = service.GetOrderedStages();

            stages.Should().ContainInOrder(
                ApplicationStage.Applied,
                ApplicationStage.Screen,
                ApplicationStage.Interview,
                ApplicationStage.Offer,
                ApplicationStage.Hired);
        }

        [Fact]
        public void GetOrderedStages_DoesNotContainRejected()
        {
            var service = CreateService();

            service.GetOrderedStages().Should().NotContain(ApplicationStage.Rejected);
        }

        [Theory]
        [InlineData(ApplicationStage.Applied,   ApplicationStage.Screen)]
        [InlineData(ApplicationStage.Screen,    ApplicationStage.Interview)]
        [InlineData(ApplicationStage.Interview, ApplicationStage.Offer)]
        [InlineData(ApplicationStage.Offer,     ApplicationStage.Hired)]
        public void GetNextStage_ReturnsExpectedNextStage(ApplicationStage current, ApplicationStage expected)
        {
            var service = CreateService();

            service.GetNextStage(current).Should().Be(expected);
        }

        [Fact]
        public void GetNextStage_WhenOnLastStage_ReturnsNull()
        {
            var service = CreateService();

            service.GetNextStage(ApplicationStage.Hired).Should().BeNull();
        }

        [Fact]
        public void GetNextStage_WhenRejected_ReturnsNull()
        {
            // Rejected is a terminal state and is not in the pipeline
            var service = CreateService();

            service.GetNextStage(ApplicationStage.Rejected).Should().BeNull();
        }

        [Fact]
        public void GetOrderedStages_HasFiveStages()
        {
            var service = CreateService();

            service.GetOrderedStages().Should().HaveCount(5);
        }
    }
}
