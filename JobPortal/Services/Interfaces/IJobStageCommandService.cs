using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IJobStageCommandService
    {
        /// <summary>
        /// Adds a new custom stage to a job, placed after all existing stages.
        /// Returns StageAlreadyExists if a stage with the same name already exists for the job.
        /// </summary>
        Task<JobStageCommandResult> AddStageAsync(int jobId, string stageName);

        /// <summary>
        /// Removes a custom stage from a job.
        /// </summary>
        Task<JobStageCommandResult> RemoveStageAsync(int stageId);

        /// <summary>
        /// Moves a stage up or down within a job's ordered stage list by swapping Order values.
        /// </summary>
        Task<JobStageCommandResult> MoveStageAsync(int stageId, int jobId, string direction);
    }
}
