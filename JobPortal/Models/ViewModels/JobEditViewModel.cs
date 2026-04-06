using Microsoft.AspNetCore.Mvc.Rendering;

namespace JobPortal.Models.ViewModels
{
    public class JobEditViewModel
    {
        public required Job Job { get; set; }
        public List<SelectListItem> ScorecardTemplates { get; set; } = [];
    }
}
