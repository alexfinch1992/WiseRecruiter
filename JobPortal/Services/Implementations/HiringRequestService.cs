using JobPortal.Data;
using JobPortal.Domain.HiringRequests;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class HiringRequestService : IHiringRequestService
    {
        private readonly AppDbContext _context;
        private readonly IHiringRequestStateMachine<Stage1HiringRequestTransitionContext> _stage1Machine;
        private readonly IHiringRequestStateMachine<Stage2HiringRequestTransitionContext> _stage2Machine;

        public HiringRequestService(
            AppDbContext context,
            IHiringRequestStateMachine<Stage1HiringRequestTransitionContext>? stage1Machine = null,
            IHiringRequestStateMachine<Stage2HiringRequestTransitionContext>? stage2Machine = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _stage1Machine = stage1Machine ?? new Stage1HiringRequestStateMachine();
            _stage2Machine = stage2Machine ?? new Stage2HiringRequestStateMachine();
        }

        public Task<List<HiringRequest>> GetAllAsync() =>
            _context.HiringRequests.OrderByDescending(r => r.CreatedUtc).ToListAsync();

        public Task<HiringRequest?> GetByIdAsync(int id, string userId, string userRole)
        {
            if (userRole == "Admin" || userRole == "TalentLead" || userRole == "ApprovingExecutive")
                return _context.HiringRequests.FirstOrDefaultAsync(r => r.Id == id);

            return _context.HiringRequests.FirstOrDefaultAsync(r => r.Id == id && r.RequestedByUserId == userId);
        }

        public async Task<HiringRequest> CreateDraftAsync(string userId, HiringRequestViewModel vm)
        {
            var request = new HiringRequest
            {
                RequestedByUserId = userId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            _context.HiringRequests.Add(request);

            _stage1Machine.ApplyTransition(request, HiringRequestStatus.Draft,
                Stage1HiringRequestTransitionContext.ForDraftSave(
                    vm.RoleTitle, vm.Department, vm.LevelBand, vm.Location,
                    vm.IsReplacement, vm.ReplacementReason, vm.Headcount, vm.Justification));

            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<TransitionResult> SaveDraftAsync(int id, HiringRequestViewModel vm, string userId)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            if (request.RequestedByUserId != userId)
                return TransitionResult.Unauthorized;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.Draft,
                Stage1HiringRequestTransitionContext.ForDraftSave(
                    vm.RoleTitle, vm.Department, vm.LevelBand, vm.Location,
                    vm.IsReplacement, vm.ReplacementReason, vm.Headcount, vm.Justification));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> SubmitAsync(int id, string userId)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            if (request.RequestedByUserId != userId)
                return TransitionResult.Unauthorized;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.Submitted,
                Stage1HiringRequestTransitionContext.ForSubmit(userId));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> ApproveStage1Async(int id, string userId, string? notes = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.TalentLeadApproved,
                Stage1HiringRequestTransitionContext.ForApproval(userId, notes));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> RejectStage1Async(int id, string userId, string? reason = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.Rejected,
                Stage1HiringRequestTransitionContext.ForRejection(userId, reason));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> ApproveStage2Async(int id, string userId, string? notes = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage2Machine.ApplyTransition(request, HiringRequestStatus.ExecutiveApproved,
                Stage2HiringRequestTransitionContext.ForApproval(userId, notes));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> RejectStage2Async(int id, string userId, string? reason = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage2Machine.ApplyTransition(request, HiringRequestStatus.Rejected,
                Stage2HiringRequestTransitionContext.ForRejection(userId, reason));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }
    }
}
