using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    public class HiringPipelineService : IHiringPipelineService
    {
        private static readonly HashSet<string> SystemStageNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Applied", "Screen", "Offer", "Hired"
        };

        public List<PipelineStageViewModel> GetPipeline(Application application, List<JobStage> jobStages)
        {
            var isRejected = application.Stage == ApplicationStage.Rejected;
            var result = new List<PipelineStageViewModel>();

            result.Add(SystemStage("Applied", application, ApplicationStage.Applied, isRejected));
            result.Add(SystemStage("Screen", application, ApplicationStage.Screen, isRejected));

            var filteredJobStages = jobStages
                .Where(s => !SystemStageNames.Contains(s.Name))
                .OrderBy(s => s.Order)
                .ToList();
            int currentJobStageIndex = filteredJobStages.FindIndex(s => s.Id == application.CurrentJobStageId);

            for (int i = 0; i < filteredJobStages.Count; i++)
            {
                var jobStage = filteredJobStages[i];
                bool isCurrent, isCompleted;

                if (isRejected)
                {
                    isCurrent = false;
                    isCompleted = false;
                }
                else if (application.Stage > ApplicationStage.Interview)
                {
                    isCurrent = false;
                    isCompleted = true;
                }
                else if (application.Stage == ApplicationStage.Interview)
                {
                    isCurrent = jobStage.Id == application.CurrentJobStageId;
                    isCompleted = currentJobStageIndex != -1 && i < currentJobStageIndex;
                }
                else
                {
                    isCurrent = false;
                    isCompleted = false;
                }

                result.Add(new PipelineStageViewModel
                {
                    Name = jobStage.Name,
                    IsCurrent = isCurrent,
                    IsCompleted = isCompleted
                });
            }

            result.Add(SystemStage("Offer", application, ApplicationStage.Offer, isRejected));
            result.Add(SystemStage("Hired", application, ApplicationStage.Hired, isRejected));

            return result;
        }

        private static PipelineStageViewModel SystemStage(
            string name, Application application, ApplicationStage stage, bool isRejected) =>
            new PipelineStageViewModel
            {
                Name = name,
                IsCurrent = !isRejected && application.Stage == stage,
                IsCompleted = !isRejected && (int)application.Stage > (int)stage
            };
    }
}
