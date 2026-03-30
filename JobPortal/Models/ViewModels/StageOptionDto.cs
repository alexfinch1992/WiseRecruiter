namespace JobPortal.Models.ViewModels
{
    public class StageOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsCustom { get; set; }
        public bool IsGated { get; set; }
        /// <summary>Sort weight — Applied=0, Screen=10, Custom=20+, Interview=50, Offer=80, Hired=90, Rejected=100</summary>
        public int Weight { get; set; }
        /// <summary>True when a Stage 1 Recommendation approval is required (or bypassed) to move here.</summary>
        public bool RequiresRecommendation { get; set; }
    }
}
