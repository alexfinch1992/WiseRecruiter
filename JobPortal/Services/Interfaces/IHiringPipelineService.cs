using JobPortal.Models;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    public interface IHiringPipelineService
    {
        List<PipelineStageViewModel> GetPipeline(Application application, List<JobStage> jobStages);
    }
}
