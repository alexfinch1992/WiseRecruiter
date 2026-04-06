namespace JobPortal.Models.ViewModels
{
    public class JobDeleteViewModel
    {
        public required Job Job { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
