using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    public class HiringRequestViewModel
    {
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int Headcount { get; set; } = 1;
        public string? Justification { get; set; }
        public string? SalaryBand { get; set; }
        public DateTime? TargetStartDate { get; set; }
        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
        public HiringPriority Priority { get; set; } = HiringPriority.Medium;
    }
}
