namespace JobPortal.Services.Models
{
    public enum JobStageCommandError
    {
        None,
        JobNotFound,
        StageNotFound,
        StageAlreadyExists,
        NameEmpty,
        CannotMove
    }

    public class JobStageCommandResult
    {
        public bool                 Success { get; init; }
        public JobStageCommandError Error   { get; init; }
    }
}
