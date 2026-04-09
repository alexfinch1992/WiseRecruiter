using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IHiringRequestService
    {
        Task<List<HiringRequest>> GetAllAsync();
        /// <summary>Returns the request (with comments) if the caller is authorized to view it, otherwise null.</summary>
        Task<HiringRequest?> GetByIdAsync(int id, string userId, string userRole);

        /// <summary>Creates a new HiringRequest in Draft and returns it.</summary>
        Task<HiringRequest> CreateDraftAsync(string userId, HiringRequestViewModel vm);

        /// <summary>Overwrites content fields on a Draft or MoreInfoRequested request. Only the request creator may edit.</summary>
        Task<TransitionResult> SaveDraftAsync(int id, HiringRequestViewModel vm, string userId);

        /// <summary>Submits a Draft or MoreInfoRequested request for Stage 1 (Senior Talent Lead) review.</summary>
        Task<TransitionResult> SubmitAsync(int id, string userId);

        // Stage 1 — Senior Talent Lead actions
        Task<TransitionResult> ApproveStage1Async(int id, string userId, string notes);
        Task<TransitionResult> RejectStage1Async(int id, string userId, string reason);
        Task<TransitionResult> RequestMoreInfoStage1Async(int id, string userId, string notes);

        // Stage 2 — Senior Executive actions
        Task<TransitionResult> ApproveStage2Async(int id, string userId, string notes);
        Task<TransitionResult> RejectStage2Async(int id, string userId, string reason);
        Task<TransitionResult> RequestMoreInfoStage2Async(int id, string userId, string notes);
    }
}
