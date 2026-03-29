using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Domain.Recommendations;
using JobPortal.Helpers;
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

builder.Services.AddAuthentication("AdminAuth")
    .AddCookie("AdminAuth", options =>
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
    
var app = builder.Build();

// Initialize database with default admin user
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
    DbInitializer.Initialize(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
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
