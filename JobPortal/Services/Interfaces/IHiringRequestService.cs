using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IHiringRequestService
    {
        Task<List<HiringRequest>> GetAllAsync();
        Task<HiringRequest?> GetByIdAsync(int id);

        /// <summary>Creates a new HiringRequest in Draft and returns it.</summary>
        Task<HiringRequest> CreateDraftAsync(int createdByUserId, HiringRequestViewModel vm);

        /// <summary>Overwrites content fields on a Draft or NeedsRevision request.</summary>
        Task<TransitionResult> SaveDraftAsync(int id, HiringRequestViewModel vm);

        /// <summary>Submits a Draft request for Stage 1 (Senior Talent Lead) review.</summary>
        Task<TransitionResult> SubmitAsync(int id, int userId);

        // Stage 1 — Senior Talent Lead actions
        Task<TransitionResult> ApproveStage1Async(int id, int userId, string? feedback = null);
        Task<TransitionResult> RejectStage1Async(int id, int userId, string? reason = null);
        Task<TransitionResult> RequestRevisionStage1Async(int id, int userId, string? feedback = null);

        // Stage 2 — Senior Executive actions
        Task<TransitionResult> SubmitStage2Async(int id, int userId);
        Task<TransitionResult> ApproveStage2Async(int id, int userId, string? feedback = null);
        Task<TransitionResult> RejectStage2Async(int id, int userId, string? reason = null);
        Task<TransitionResult> RequestRevisionStage2Async(int id, int userId, string? feedback = null);
    }
}
