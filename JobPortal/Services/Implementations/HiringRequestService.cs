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

        public Task<HiringRequest?> GetByIdAsync(int id) =>
            _context.HiringRequests.FirstOrDefaultAsync(r => r.Id == id);

        public async Task<HiringRequest> CreateDraftAsync(int createdByUserId, HiringRequestViewModel vm)
        {
            var request = new HiringRequest { CreatedByUserId = createdByUserId };
            _context.HiringRequests.Add(request);

            _stage1Machine.ApplyTransition(request, HiringRequestStatus.Draft,
                Stage1HiringRequestTransitionContext.ForDraftSave(
                    vm.JobTitle, vm.Department, vm.Headcount, vm.Justification,
                    vm.SalaryBand, vm.TargetStartDate, vm.EmploymentType, vm.Priority));

            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<TransitionResult> SaveDraftAsync(int id, HiringRequestViewModel vm)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.Draft,
                Stage1HiringRequestTransitionContext.ForDraftSave(
                    vm.JobTitle, vm.Department, vm.Headcount, vm.Justification,
                    vm.SalaryBand, vm.TargetStartDate, vm.EmploymentType, vm.Priority));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> SubmitAsync(int id, int userId)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.Submitted,
                Stage1HiringRequestTransitionContext.ForSubmit(userId));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> ApproveStage1Async(int id, int userId, string? feedback = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.Approved,
                Stage1HiringRequestTransitionContext.ForApproval(userId, feedback));

            if (result != TransitionResult.Success)
                return result;

            // Advance to Stage 2
            request.Stage = HiringRequestStage.Stage2;
            request.Status = HiringRequestStatus.Draft;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> RejectStage1Async(int id, int userId, string? reason = null)
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

        public async Task<TransitionResult> RequestRevisionStage1Async(int id, int userId, string? feedback = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage1Machine.ApplyTransition(request, HiringRequestStatus.NeedsRevision,
                Stage1HiringRequestTransitionContext.ForNeedsRevision(userId, feedback));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> SubmitStage2Async(int id, int userId)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage2Machine.ApplyTransition(request, HiringRequestStatus.Submitted,
                Stage2HiringRequestTransitionContext.ForSubmit(userId));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> ApproveStage2Async(int id, int userId, string? feedback = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage2Machine.ApplyTransition(request, HiringRequestStatus.Approved,
                Stage2HiringRequestTransitionContext.ForApproval(userId, feedback));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> RejectStage2Async(int id, int userId, string? reason = null)
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

        public async Task<TransitionResult> RequestRevisionStage2Async(int id, int userId, string? feedback = null)
        {
            var request = await _context.HiringRequests.FindAsync(id);
            if (request == null)
                return TransitionResult.NotFound;

            var result = _stage2Machine.ApplyTransition(request, HiringRequestStatus.NeedsRevision,
                Stage2HiringRequestTransitionContext.ForNeedsRevision(userId, feedback));

            if (result != TransitionResult.Success)
                return result;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }
    }
}
