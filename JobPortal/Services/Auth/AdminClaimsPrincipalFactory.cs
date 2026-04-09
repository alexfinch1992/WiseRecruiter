using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<AdminClaimsPrincipalFactory> _logger;

        public AdminClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            AppDbContext context,
            ILogger<AdminClaimsPrincipalFactory> logger)
            : base(userManager, roleManager, optionsAccessor)
        {
            _context = context;
            _logger = logger;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // Derive the legacy AdminUser username from the email local-part
            var localPart = user.Email?.Split('@')[0];
            var identityName = identity.Name;
            var adminUsernames = _context.AdminUsers
                .Select(a => a.Username)
                .Take(10)
                .ToList();

            _logger.LogInformation("Identity.Name: {name}", identityName);
            _logger.LogInformation("User.Email: {email}", user.Email);
            _logger.LogInformation("User.UserName: {userName}", user.UserName);
            _logger.LogInformation("localPart: {localPart}", localPart);
            _logger.LogInformation("AdminUser usernames (sample): {users}", string.Join(",", adminUsernames));

            if (!string.IsNullOrEmpty(localPart))
            {
                var admin = _context.AdminUsers
                    .FirstOrDefault(a => a.Username != null &&
                                         localPart != null &&
                                         a.Username.ToLower() == localPart.ToLower());

                _logger.LogInformation("Admin match found: {found}", admin != null);

                if (admin != null)
                {
                    _logger.LogInformation("Matched Admin Username: {username}", admin.Username);
                    identity.AddClaim(new Claim("AdminId", admin.Id.ToString()));
                }
            }

            // Grant ApprovingExecutive role claim to users flagged as approving executives,
            // so that [Authorize(Roles = "ApprovingExecutive")] works regardless of assigned role.
            if (user.IsApprovingExecutive)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, "ApprovingExecutive"));
            }

            return identity;
        }
    }
}
