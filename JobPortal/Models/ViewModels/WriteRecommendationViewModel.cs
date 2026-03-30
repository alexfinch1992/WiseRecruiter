namespace JobPortal.Models.ViewModels
{
    /// <summary>
    /// Slim ViewModel for the dedicated Stage 1 and Stage 2 Recommendation editor pages.
    /// </summary>
    public class WriteRecommendationViewModel
    {
        public int     Id          { get; set; }
        public int     JobId       { get; set; }
        public string? Name        { get; set; }
        public string? JobTitle    { get; set; }
        /// <summary>1 = Stage 1, 2 = Stage 2</summary>
        public int     StageNumber { get; set; } = 1;
        /// <summary>None | Draft | Submitted | Approved | Rejected — for the relevant stage</summary>
        public string? RecStatus   { get; set; }
        /// <summary>Backwards-compat alias for Stage1Status used in existing view.</summary>
        public string? Stage1Status => StageNumber == 1 ? RecStatus : null;
    }
}
