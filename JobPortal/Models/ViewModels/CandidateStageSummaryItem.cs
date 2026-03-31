namespace JobPortal.Models.ViewModels
{
    /// <summary>
    /// Represents one row in the "Candidates" summary box on the JobDetail page:
    /// stage label → how many candidates are currently in that stage.
    /// </summary>
    public sealed record CandidateStageSummaryItem(string StageName, int Count);
}
