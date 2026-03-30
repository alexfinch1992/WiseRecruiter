using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using JobPortal.Data;
using JobPortal.Domain.Recommendations;
using JobPortal.Helpers;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=jobportal.db"));

// Add session and authentication
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/";
});

// Register simplified business logic services (CRUD handled directly by controllers with DbContext)
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IScorecardService, ScorecardService>();
builder.Services.AddScoped<IFacetService, FacetService>();
builder.Services.AddScoped<IScorecardTemplateService, ScorecardTemplateService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IFileUploadService, LocalFileUploadService>();
builder.Services.AddScoped<IScorecardAnalyticsService, ScorecardAnalyticsService>();
builder.Services.AddScoped<IInterviewService, InterviewService>();
builder.Services.AddScoped<IStageAuthorizationService, StageAuthorizationService>();
builder.Services.AddScoped<IStageStateMachine<Stage1TransitionContext>, Stage1StateMachine>();
builder.Services.AddScoped<IStageStateMachine<Stage2TransitionContext>, Stage2StateMachine>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IStageOrderService, StageOrderService>();
builder.Services.AddScoped<IApplicationStageService, ApplicationStageService>();
builder.Services.AddScoped<IHiringPipelineService, HiringPipelineService>();
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IJobAccessService, JobAccessService>();
    
var app = builder.Build();

// Initialize database with default admin user
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
    DbInitializer.Initialize(context);

    // Seed Identity roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = { "Admin", "Recruiter", "HiringManager" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed the master Admin user
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await DbInitializer.SeedAdminUserAsync(userManager);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // HSTS: 30 days for production; extend to 1 year (31536000s) once stable.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers — applied before routing so every response carries them.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]        = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    await next();
});

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
