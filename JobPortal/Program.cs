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

// Inject the "AdminId" claim from the legacy AdminUsers table at sign-in
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, JobPortal.Services.Auth.AdminClaimsPrincipalFactory>();

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
builder.Services.AddScoped<ICandidateCoreService, CandidateCoreService>();
builder.Services.AddScoped<IScorecardSummaryService, ScorecardSummaryService>();
builder.Services.AddScoped<IRelatedApplicationsService, RelatedApplicationsService>();
builder.Services.AddScoped<IRecommendationSummaryService, RecommendationSummaryService>();
builder.Services.AddScoped<IWriteRecommendationService, WriteRecommendationService>();
builder.Services.AddScoped<IMoveApplicationStageService, MoveApplicationStageService>();
builder.Services.AddScoped<IInterviewCommandService, InterviewCommandService>();
builder.Services.AddScoped<JobPortal.Services.Alerts.AlertRecipientResolver>();
builder.Services.AddScoped<JobPortal.Services.Alerts.AlertService>();
builder.Services.AddScoped<IJobCommandService, JobCommandService>();
builder.Services.AddScoped<IJobQueryService, JobQueryService>();
builder.Services.AddScoped<IJobStageCommandService, JobStageCommandService>();
builder.Services.AddScoped<ICandidateQueryService, CandidateQueryService>();
builder.Services.AddScoped<IResumeReviewService, ResumeReviewService>();
builder.Services.AddScoped<IScorecardCommandService, ScorecardCommandService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<RecommendationCommandService>();
builder.Services.AddScoped<IRecommendationActionService>(sp => sp.GetRequiredService<RecommendationCommandService>());
builder.Services.AddScoped<IRecommendationDraftService>(sp => sp.GetRequiredService<RecommendationCommandService>());
builder.Services.AddScoped<ICandidateDetailsService, CandidateDetailsService>();
    
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
// ViewResumeInline is excluded so controller-served PDFs can be embedded inline.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/Admin/ViewResumeInline"))
    {
        context.Response.Headers["X-Frame-Options"]        = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    }
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
