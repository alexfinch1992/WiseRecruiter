using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using JobPortal.Models;
using JobPortal.Data;

namespace JobPortal.Services.Auth
{
    /// <summary>
    /// Injects an "AdminId" claim (integer PK from the AdminUsers table) into the
    /// cookie principal at sign-in. The match is done by comparing the local part
    /// of the Identity user's email against AdminUser.Username
    /// (e.g. "admin@wiserecruiter.com" → "admin").
    /// </summary>
    public class AdminClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly AppDbContext _context;

        public AdminClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            AppDbContext context)
            : base(userManager, roleManager, optionsAccessor)
        {
            _context = context;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // Derive the legacy AdminUser username from the email local-part
            var localPart = user.Email?.Split('@')[0];
            if (!string.IsNullOrEmpty(localPart))
            {
                var admin = _context.AdminUsers
                    .FirstOrDefault(a => a.Username == localPart);

                if (admin != null)
                {
                    identity.AddClaim(new Claim("AdminId", admin.Id.ToString()));
                }
            }

            return identity;
        }
    }
}
