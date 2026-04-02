using JobPortal.Services.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/debug/alerts")]
    public class DebugAlertsController : Controller
    {
        private readonly AlertRecipientResolver _resolver;

        public DebugAlertsController(AlertRecipientResolver resolver)
        {
            _resolver = resolver;
        }

        [HttpGet("recipients/{jobId}")]
        public async Task<IActionResult> GetRecipients(int jobId)
        {
            var userIds = await _resolver.ResolveUserIdsAsync(jobId);
            return Ok(userIds);
        }
    }
}
