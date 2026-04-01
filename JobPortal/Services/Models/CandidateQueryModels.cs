namespace JobPortal.Services.Models
{
    /// <summary>One item in the JobDetailSearchApi response.</summary>
    public record JobSearchApiItem(int Id, string? Name, string? Email, string? City, string Stage);

    /// <summary>One item in the SearchCandidatesApi response.</summary>
    public record CandidateSearchApiItem(int Id, string? Name, string? Email, string? Job, string Stage);
}
