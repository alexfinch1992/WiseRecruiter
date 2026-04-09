using Microsoft.AspNetCore.Identity;

namespace JobPortal.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsApprovingExecutive { get; set; } = false;
    }
}
