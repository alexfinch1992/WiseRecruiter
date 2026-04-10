using JobPortal.Data;
using JobPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/alerts")]
    public class AlertsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AlertsController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var alerts = await _context.Alerts
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync();

            var alertIds = alerts.Select(a => a.Id).ToList();
            await _context.Alerts
                .Where(a => alertIds.Contains(a.Id) && !a.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));

            return Ok(alerts.Select(a => new
            {
                a.Id,
                a.Type,
                a.Message,
                IsRead = true,
                a.CreatedAt,
                a.LinkUrl
            }));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var alerts = await _context.Alerts
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var alertIds = alerts.Select(a => a.Id).ToList();
            await _context.Alerts
                .Where(a => alertIds.Contains(a.Id) && !a.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true));

            return Ok(alerts.Select(a => new
            {
                a.Id,
                a.Type,
                a.Message,
                IsRead = true,
                a.CreatedAt,
                a.LinkUrl
            }));
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var count = await _context.Alerts
                .CountAsync(a => a.UserId == user.Id && !a.IsRead);

            return Ok(count);
        }

        [HttpPost("toggle-job/{jobId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleJobAlert(int jobId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var existing = await _context.JobAlertSubscriptions
                .FirstOrDefaultAsync(x => x.JobId == jobId && x.UserId == user.Id);

            if (existing != null)
            {
                existing.IsEnabled = !existing.IsEnabled;
            }
            else
            {
                _context.JobAlertSubscriptions.Add(new JobAlertSubscription
                {
                    JobId = jobId,
                    UserId = user.Id,
                    IsEnabled = true
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpGet("job/{jobId}")]
        public async Task<IActionResult> GetJobAlertState(int jobId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var subscription = await _context.JobAlertSubscriptions
                .FirstOrDefaultAsync(x => x.JobId == jobId && x.UserId == user.Id);

            if (subscription != null)
                return Ok(subscription.IsEnabled);

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            return Ok(!isAdmin);
        }
    }
}
