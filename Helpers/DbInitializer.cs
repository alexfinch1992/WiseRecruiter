using JobPortal.Data;
using JobPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Helpers
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Seed admin user
            if (!context.AdminUsers.Any())
            {
                var adminUser = new AdminUser
                {
                    Username = "admin",
                    PasswordHash = PasswordHasher.Hash("admin123")
                };
                context.AdminUsers.Add(adminUser);
                context.SaveChanges();
            }

            // Seed sample jobs
            if (!context.Jobs.Any())
            {
                var jobs = new[]
                {
                    new Job
                    {
                        Title = "Senior Software Engineer",
                        Description = "We are looking for an experienced Senior Software Engineer to join our growing team. You will work on challenging projects using modern technologies including .NET, React, and cloud services. Ideal candidates will have 5+ years of experience and strong problem-solving skills."
                    },
                    new Job
                    {
                        Title = "Full Stack Developer",
                        Description = "Join us as a Full Stack Developer and help build amazing web applications. We're looking for someone with expertise in both frontend (React/Vue) and backend (Node.js/Python) technologies. You'll work in an agile environment with a talented team of developers."
                    }
                };
                context.Jobs.AddRange(jobs);
                context.SaveChanges();
                
                // Seed default stages for each job
                var allJobs = context.Jobs.ToList();
                var defaultStages = new[] { "Application", "Screen", "Interview", "Offer" };
                
                foreach (var job in allJobs)
                {
                    for (int i = 0; i < defaultStages.Length; i++)
                    {
                        context.JobStages.Add(new JobStage
                        {
                            JobId = job.Id,
                            Name = defaultStages[i],
                            Order = i
                        });
                    }
                }
                context.SaveChanges();
            }

            // Seed dummy candidates
            if (!context.Applications.Any())
            {
                var jobs = context.Jobs.Include(j => j.Stages).ToList();
                if (jobs.Any())
                {
                    var candidates = GenerateDummyCandidates(jobs);
                    context.Applications.AddRange(candidates);
                    context.SaveChanges();
                }
            }
        }

        private static List<Application> GenerateDummyCandidates(List<Job> jobs)
        {
            var applications = new List<Application>();
            var cities = new[] { "New York", "San Francisco", "Los Angeles", "Chicago", "Boston", "Seattle", "Austin", "Denver", "Portland", "Miami" };

            var candidateNames = new[]
            {
                ("Alice Johnson", "alice.johnson@email.com"),
                ("Bob Smith", "bob.smith@email.com"),
                ("Carol Williams", "carol.williams@email.com"),
                ("David Brown", "david.brown@email.com"),
                ("Emma Davis", "emma.davis@email.com"),
                ("Frank Wilson", "frank.wilson@email.com"),
                ("Grace Lee", "grace.lee@email.com"),
                ("Henry Martinez", "henry.martinez@email.com"),
                ("Iris Chen", "iris.chen@email.com"),
                ("Jack Anderson", "jack.anderson@email.com"),
                ("Karen Taylor", "karen.taylor@email.com"),
                ("Leo Thomas", "leo.thomas@email.com"),
                ("Monica Garcia", "monica.garcia@email.com"),
                ("Nathan Robinson", "nathan.robinson@email.com"),
                ("Olivia Clark", "olivia.clark@email.com")
            };

            var random = new Random(42); // Fixed seed for consistency

            foreach (var job in jobs)
            {
                var jobStages = job.Stages?.OrderBy(s => s.Order).ToList() ?? new List<JobStage>();
                
                if (!jobStages.Any())
                    continue; // Skip if no stages for this job

                // Add 7-8 candidates per job
                int candidatesPerJob = random.Next(7, 9);
                
                for (int i = 0; i < candidatesPerJob; i++)
                {
                    var (name, email) = candidateNames[applications.Count % candidateNames.Length];
                    var cityIndex = (applications.Count + i) % cities.Length;
                    var stageIndex = (applications.Count + i) % jobStages.Count;
                    var daysAgo = random.Next(1, 30);

                    applications.Add(new Application
                    {
                        Name = name,
                        Email = $"{name.ToLower().Replace(" ", ".")}.{applications.Count}@email.com",
                        City = cities[cityIndex],
                        JobId = job.Id,
                        CurrentJobStageId = jobStages[stageIndex].Id,
                        AppliedDate = DateTime.UtcNow.AddDays(-daysAgo),
                        ResumePath = null // In real scenario, would be actual resume files
                    });
                }
            }

            return applications;
        }
    }
}
