using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Helpers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

public class AccountController : Controller
{
    private readonly AppDbContext _context;

    public AccountController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Username and password are required.");
            return View();
        }

        var adminUser = await _context.AdminUsers.FirstOrDefaultAsync(a => a.Username == username);
        
        if (adminUser == null || string.IsNullOrEmpty(adminUser.PasswordHash) || !PasswordHasher.Verify(password, adminUser.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, adminUser.Username ?? ""),
            new Claim("AdminId", adminUser.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, "AdminAuth");
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync("AdminAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
        return RedirectToAction("Index", "Admin");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminAuth");
        return RedirectToAction("Index", "Home");
    }
}
