using JobPortal.Models;

namespace JobPortal.Services.Models
{
    public class StageUpdateResult
    {
        public bool RequiresApprovalWarning { get; set; }
        public ApplicationStage? PendingStage { get; set; }
    }
}
