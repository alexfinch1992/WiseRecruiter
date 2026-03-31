namespace JobPortal.Services.Models
{
    public class MoveStageResult
    {
        public bool    Success          { get; init; }
        public bool    RequiresApproval { get; init; }
        public string? NewStageName     { get; init; }
    }
}
