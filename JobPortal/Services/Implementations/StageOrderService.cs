using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Default stage ordering service.
    /// Defines the canonical application pipeline: Applied → Screen → Interview → Offer → Hired.
    /// Rejected is intentionally excluded — it is a terminal state, not a progression step.
    /// </summary>
    public class StageOrderService : IStageOrderService
    {
        private static readonly List<ApplicationStage> _orderedStages = new()
        {
            ApplicationStage.Applied,
            ApplicationStage.Screen,
            ApplicationStage.Interview,
            ApplicationStage.Offer,
            ApplicationStage.Hired,
        };

        public IReadOnlyList<ApplicationStage> GetOrderedStages() => _orderedStages;

        public ApplicationStage? GetNextStage(ApplicationStage current)
        {
            var index = _orderedStages.IndexOf(current);
            if (index < 0 || index >= _orderedStages.Count - 1)
                return null;

            return _orderedStages[index + 1];
        }
    }
}
