using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IHiringRequestService
    {
        Task<List<HiringRequest>> GetAllAsync();
        /// <summary>Returns the request if the caller is authorized to view it, otherwise null.</summary>
        Task<HiringRequest?> GetByIdAsync(int id, string userId, string userRole);

        /// <summary>Creates a new HiringRequest in Draft and returns it.</summary>
        Task<HiringRequest> CreateDraftAsync(string userId, HiringRequestViewModel vm);

        /// <summary>Overwrites content fields on a Draft request. Only the request creator may edit.</summary>
        Task<TransitionResult> SaveDraftAsync(int id, HiringRequestViewModel vm, string userId);

        /// <summary>Submits a Draft request for Stage 1 (Senior Talent Lead) review.</summary>
        Task<TransitionResult> SubmitAsync(int id, string userId);

        // Stage 1 — Senior Talent Lead actions
        Task<TransitionResult> ApproveStage1Async(int id, string userId, string? notes = null);
        Task<TransitionResult> RejectStage1Async(int id, string userId, string? reason = null);

        // Stage 2 — Senior Executive actions
        Task<TransitionResult> ApproveStage2Async(int id, string userId, string? notes = null);
        Task<TransitionResult> RejectStage2Async(int id, string userId, string? reason = null);
    }
}
