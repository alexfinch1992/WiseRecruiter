using System.Threading.Tasks;
using System.Collections.Generic;

namespace JobPortal.Services.Interfaces
{
    public interface IJobAssignmentService
    {
        Task AssignOwnerAsync(int jobId, string? userId);
        Task AssignReviewersAsync(int jobId, List<string>? reviewerIds);
    }
}
