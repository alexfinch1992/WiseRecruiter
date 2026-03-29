using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Provides the authoritative ordering of ApplicationStage values.
    /// Replaces direct enum-ordering assumptions throughout the system,
    /// making stage ordering explicit and centralised for future customisation.
    /// </summary>
    public interface IStageOrderService
    {
        /// <summary>Returns the ordered pipeline stages (Rejected excluded).</summary>
        IReadOnlyList<ApplicationStage> GetOrderedStages();

        /// <summary>
        /// Returns the stage that follows <paramref name="current"/> in the pipeline,
        /// or <c>null</c> if <paramref name="current"/> is the last stage.
        /// </summary>
        ApplicationStage? GetNextStage(ApplicationStage current);
    }
}
