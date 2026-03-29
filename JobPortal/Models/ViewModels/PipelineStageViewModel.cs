namespace JobPortal.Models.ViewModels
{
    public class PipelineStageViewModel
    {
        public string Name { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
    }
}
