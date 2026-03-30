namespace JobPortal.Models.ViewModels
{
    public class ManageTeamViewModel
    {
        public List<UserAssignmentRow> Users { get; set; } = new();
        public List<Job> AllJobs { get; set; } = new();
    }

    public class UserAssignmentRow
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<int> AssignedJobIds { get; set; } = new();
    }
}
