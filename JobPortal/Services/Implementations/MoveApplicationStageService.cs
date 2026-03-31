using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;

namespace JobPortal.Services.Implementations
{
    public class MoveApplicationStageService : IMoveApplicationStageService
    {
        private readonly AppDbContext            _context;
        private readonly IApplicationStageService _applicationStageService;
        private readonly IAuditService           _auditService;

        public MoveApplicationStageService(
            AppDbContext             context,
            IApplicationStageService applicationStageService,
            IAuditService            auditService)
        {
            _context                 = context                 ?? throw new ArgumentNullException(nameof(context));
            _applicationStageService = applicationStageService ?? throw new ArgumentNullException(nameof(applicationStageService));
            _auditService            = auditService            ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task<MoveStageResult> MoveAsync(
            int    applicationId,
            string selectedStage,
            bool   proceedWithoutApproval,
            string userId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                return new MoveStageResult { Success = true };

            ApplicationStage newStage;
            int? jobStageId = null;

            if (selectedStage.StartsWith("stage:"))
            {
                if (!int.TryParse(selectedStage["stage:".Length..], out var sid))
                    return new MoveStageResult { Success = true };

                var jobStage = await _context.JobStages.FindAsync(sid);
                if (jobStage == null || jobStage.JobId != application.JobId)
                    return new MoveStageResult { Success = true };

                newStage   = ApplicationStage.Interview;
                jobStageId = sid;
            }
            else if (selectedStage.StartsWith("enum:"))
            {
                if (!Enum.TryParse<ApplicationStage>(selectedStage["enum:".Length..], out newStage))
                    return new MoveStageResult { Success = true };
            }
            else
            {
                return new MoveStageResult { Success = true };
            }

            var oldStage = application.Stage;
            var result   = await _applicationStageService.UpdateStageAsync(
                applicationId, newStage, proceedWithoutApproval, userId, jobStageId);

            if (!result.RequiresApprovalWarning)
            {
                await _context.SaveChangesAsync();
                var action  = proceedWithoutApproval ? "Override" : "StageMove";
                var changes = "Old: " + oldStage + " -> New: " + newStage + "; Override: " + proceedWithoutApproval;
                await _auditService.LogAsync("Application", applicationId, action, changes, userId);
            }

            return new MoveStageResult
            {
                Success          = true,
                RequiresApproval = result.RequiresApprovalWarning,
                NewStageName     = newStage.ToString(),
            };
        }

        public async Task<MoveStageResult?> MoveForFormAsync(
            int    applicationId,
            string selectedStage,
            bool   proceedWithoutApproval,
            string userId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                return null;

            if (selectedStage.StartsWith("stage:"))
            {
                if (!int.TryParse(selectedStage["stage:".Length..], out var jobStageId))
                    return new MoveStageResult { Success = false };

                var jobStage = await _context.JobStages.FindAsync(jobStageId);
                if (jobStage == null || jobStage.JobId != application.JobId)
                    return new MoveStageResult { Success = false };

                var result = await _applicationStageService.UpdateStageAsync(
                    applicationId, ApplicationStage.Interview, proceedWithoutApproval, userId, jobStageId);

                if (result.RequiresApprovalWarning)
                    return new MoveStageResult { Success = true, RequiresApproval = true };

                await _context.SaveChangesAsync();
                return new MoveStageResult { Success = true, RequiresApproval = false };
            }
            else if (selectedStage.StartsWith("enum:"))
            {
                var enumName = selectedStage["enum:".Length..];
                if (!Enum.TryParse<ApplicationStage>(enumName, out var parsedStage))
                    return new MoveStageResult { Success = false };

                var result = await _applicationStageService.UpdateStageAsync(
                    applicationId, parsedStage, proceedWithoutApproval, userId);

                if (result.RequiresApprovalWarning)
                    return new MoveStageResult { Success = true, RequiresApproval = true };

                await _context.SaveChangesAsync();
                return new MoveStageResult { Success = true, RequiresApproval = false };
            }
            else
            {
                return new MoveStageResult { Success = false };
            }
        }
    }
}
