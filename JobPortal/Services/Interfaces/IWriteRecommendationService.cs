using JobPortal.Models;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    public interface IWriteRecommendationService
    {
        /// <summary>
        /// Loads the application and its Stage 1 recommendation, then projects the result
        /// into a <see cref="WriteRecommendationViewModel"/>.
        /// Returns <c>null</c> when the application does not exist.
        /// </summary>
        Task<WriteRecommendationViewModel?> GetStage1ViewModelAsync(int applicationId);

        /// <summary>
        /// Loads the application and its Stage 2 recommendation, then projects the result
        /// into a <see cref="WriteRecommendationViewModel"/>.
        /// Returns <c>null</c> when the application does not exist.
        /// </summary>
        Task<WriteRecommendationViewModel?> GetStage2ViewModelAsync(int applicationId);
    }
}
