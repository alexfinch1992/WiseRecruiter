using JobPortal.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Controllers
{
    /// <summary>
    /// Development-only endpoint used by Playwright E2E tests to delete records
    /// whose names/emails/titles start with a given prefix (e.g. "E2E_1234567890").
    ///
    /// SAFETY:
    ///  - Returns 404 in any environment other than Development.
    ///  - Requires Admin role so it cannot be called anonymously even in dev.
    ///  - Deletes only well-scoped entity types; never touches seeded/real data
    ///    unless those records happen to carry the test prefix (they won't in
    ///    a normal dev DB because real job titles don't start with "E2E_").
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("test")]
    public class TestCleanupController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TestCleanupController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>
        /// POST /test/cleanup
        /// Body: { "prefix": "E2E_1234567890" }
        ///
        /// Deletes, in dependency order:
        ///   Interviews → CandidateRecommendations → Scorecards → Documents →
        ///   AuditLogs → Applications → Candidates → JobStages → Jobs
        ///
        /// All matching on Job.Title, Candidate.Email, or Application.Email
        /// starting with <paramref name="prefix"/>.
        /// </summary>
        [HttpPost("cleanup")]
        public async Task<IActionResult> Cleanup([FromBody] CleanupRequest request)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var prefix = request?.Prefix;
            if (string.IsNullOrWhiteSpace(prefix))
                return BadRequest("prefix is required");

            // ── 1. Identify matching job IDs ──────────────────────────────────
            var matchingJobIds = await _context.Jobs
                .Where(j => j.Title != null && j.Title.StartsWith(prefix))
                .Select(j => j.Id)
                .ToListAsync();

            // ── 2. Identify matching application IDs ─────────────────────────
            //    (either via job prefix OR application email prefix)
            var matchingAppIds = await _context.Applications
                .Where(a =>
                    (a.Email != null && a.Email.StartsWith(prefix)) ||
                    matchingJobIds.Contains(a.JobId))
                .Select(a => a.Id)
                .ToListAsync();

            // ── 3. Identify matching candidate IDs ───────────────────────────
            var matchingCandidateIds = await _context.Applications
                .Where(a => matchingAppIds.Contains(a.Id))
                .Select(a => a.CandidateId)
                .Distinct()
                .ToListAsync();

            // ── 4. Delete leaf entities first (FK dependencies) ───────────────

            // Interviews for matching applications
            var interviews = await _context.Interviews
                .Where(i => matchingAppIds.Contains(i.ApplicationId))
                .ToListAsync();
            _context.Interviews.RemoveRange(interviews);

            // InterviewInterviewers are cascade-deleted with Interviews (schema-level),
            // but remove explicitly to be safe with in-memory provider in tests.
            var interviewIds = interviews.Select(i => i.Id).ToList();
            var interviewInterviewers = await _context.InterviewInterviewers
                .Where(ii => interviewIds.Contains(ii.InterviewId))
                .ToListAsync();
            _context.InterviewInterviewers.RemoveRange(interviewInterviewers);

            // Candidate recommendations
            var recommendations = await _context.CandidateRecommendations
                .Where(r => matchingAppIds.Contains(r.ApplicationId))
                .ToListAsync();
            _context.CandidateRecommendations.RemoveRange(recommendations);

            // Scorecards + responses (responses cascade-deleted by EF config).
            // Scorecard links to Candidate (not Application directly); also catch
            // scorecards linked via an interview that belongs to a matching application.
            var scorecards = await _context.Scorecards
                .Include(s => s.Responses)
                .Where(s =>
                    matchingCandidateIds.Contains(s.CandidateId) ||
                    (s.InterviewId != null && interviewIds.Contains(s.InterviewId.Value)))
                .ToListAsync();
            _context.Scorecards.RemoveRange(scorecards);

            // Documents
            var documents = await _context.Documents
                .Where(d => matchingAppIds.Contains(d.ApplicationId))
                .ToListAsync();
            _context.Documents.RemoveRange(documents);

            // Audit logs referencing matching applications or candidates
            var auditLogs = await _context.AuditLogs
                .Where(al =>
                    (al.EntityName == "Application" && matchingAppIds.Contains(al.EntityId)) ||
                    (al.EntityName == "Candidate"   && matchingCandidateIds.Contains(al.EntityId)))
                .ToListAsync();
            _context.AuditLogs.RemoveRange(auditLogs);

            // Applications
            var applications = await _context.Applications
                .Where(a => matchingAppIds.Contains(a.Id))
                .ToListAsync();
            _context.Applications.RemoveRange(applications);

            // Candidates (only if ALL their applications are being deleted)
            var candidatesWithOtherApps = await _context.Applications
                .Where(a =>
                    matchingCandidateIds.Contains(a.CandidateId) &&
                    !matchingAppIds.Contains(a.Id))
                .Select(a => a.CandidateId)
                .Distinct()
                .ToListAsync();
            var candidatesToDelete = matchingCandidateIds
                .Except(candidatesWithOtherApps)
                .ToList();
            var candidates = await _context.Candidates
                .Where(c => candidatesToDelete.Contains(c.Id))
                .ToListAsync();
            _context.Candidates.RemoveRange(candidates);

            // Job stages
            var stages = await _context.JobStages
                .Where(s => matchingJobIds.Contains(s.JobId))
                .ToListAsync();
            _context.JobStages.RemoveRange(stages);

            // Jobs
            var jobs = await _context.Jobs
                .Where(j => matchingJobIds.Contains(j.Id))
                .ToListAsync();
            _context.Jobs.RemoveRange(jobs);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                prefix,
                deletedJobs         = jobs.Count,
                deletedApplications = applications.Count,
                deletedCandidates   = candidates.Count,
            });
        }
    }

    public class CleanupRequest
    {
        public string? Prefix { get; set; }
    }
}
