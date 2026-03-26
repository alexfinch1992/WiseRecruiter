using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobPortal.Models;
using JobPortal.Data;

public class JobController : Controller
{
    private readonly AppDbContext _context;

    public JobController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()    
    {
        return View(await _context.Jobs.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var job = await _context.Jobs.FirstOrDefaultAsync(m => m.Id == id);
        return job == null ? NotFound() : View(job);
    }

    public async Task<IActionResult> Delete(int? id, string? returnUrl)
    {
        if (id == null)
            return NotFound();

        var job = await _context.Jobs.FirstOrDefaultAsync(m => m.Id == id);
        if (job == null)
            return NotFound();

        ViewBag.ReturnUrl = returnUrl;

        return View(job);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }
}
