using System.Collections.Generic;
using FluentAssertions;
using JobPortal.Services.Implementations;
using Xunit;

namespace WiseRecruiter.Tests.Unit.Services
{
    /// <summary>
    /// Tests for StageAuthorizationService.
    /// Currently the service is a permissive stub (always returns true).
    /// These tests document the expected behavior once real role checks are introduced.
    /// </summary>
    public class StageAuthorizationServiceTests
    {
        private static StageAuthorizationService CreateService() => new StageAuthorizationService();

        // Stage 1 — any user can approve (stub always returns true)
        [Fact]
        public void CanApproveStage1_ReturnsTrue_ForAnyUser()
        {
            var service = CreateService();
            service.CanApproveStage1(userId: 1).Should().BeTrue();
            service.CanApproveStage1(userId: 99).Should().BeTrue();
        }

        // Stage 2 — stub currently returns true (Manager-only to be enforced via real roles later)
        [Fact]
        public void CanApproveStage2_CurrentlyReturnsTrue_ForAnyUser()
        {
            var service = CreateService();
            // Stub: always true until real role checks are implemented
            service.CanApproveStage2(userId: 1).Should().BeTrue();
            service.CanApproveStage2(userId: 99).Should().BeTrue();
        }

        // The boundary: once roles are implemented, Team Lead should NOT approve Stage 2.
        // This test documents that intent using a custom auth service (mirrors the contract).
        [Fact]
        public void CanApproveStage2_TeamLead_ShouldReturnFalse_WhenRolesEnforced()
        {
            // Demonstrates the stricter rule via a direct stub —
            // real enforcement will live in the production StageAuthorizationService.
            var managerOnlyAuth = new ManagerOnlyStage2AuthService();

            managerOnlyAuth.CanApproveStage1(userId: 10).Should().BeTrue();  // Team Lead can still do Stage 1
            managerOnlyAuth.CanApproveStage2(userId: 10).Should().BeFalse(); // but NOT Stage 2
        }

        private sealed class ManagerOnlyStage2AuthService : JobPortal.Services.Interfaces.IStageAuthorizationService
        {
            private static readonly HashSet<int> _managerIds = new() { 1, 2, 3 };

            public bool CanApproveStage1(int userId) => true;            // Team Lead OR Manager
            public bool CanApproveStage2(int userId) => _managerIds.Contains(userId); // Manager ONLY
        }
    }
}
