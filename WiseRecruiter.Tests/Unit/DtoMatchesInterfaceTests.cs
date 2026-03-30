using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using JobPortal.Models.ViewModels;
using Xunit;

namespace WiseRecruiter.Tests.Unit
{
    /// <summary>
    /// Guards against "silent drift" between the C# DTO and the TypeScript interface.
    /// Any rename or addition on the C# side MUST be reflected in CandidateApplication.ts.
    /// </summary>
    public class DtoMatchesInterfaceTests
    {
        // ASP.NET Core's default System.Text.Json policy: PascalCase → camelCase
        private static string ToCamelCase(string name) =>
            char.ToLowerInvariant(name[0]) + name[1..];

        [Fact]
        public void CandidateApplicationDto_PropertiesCamelCased_MatchTypeScriptInterface()
        {
            // ── C# side ─────────────────────────────────────────────────────────────────
            var dtoProperties = typeof(CandidateApplicationDto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => ToCamelCase(p.Name))
                .OrderBy(n => n)
                .ToList();

            // ── TypeScript side: locate the interface file by walking up from the test binary ──
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !dir.GetDirectories("JobPortal").Any())
                dir = dir.Parent;

            dir.Should().NotBeNull(
                "the test binary must be nested inside the repository root that contains 'JobPortal'");

            var tsPath = Path.Combine(
                dir!.FullName,
                "JobPortal", "ClientApp", "src", "types", "CandidateApplication.ts");

            File.Exists(tsPath).Should().BeTrue(
                $"TypeScript interface file must exist at: {tsPath}");

            var tsContent = File.ReadAllText(tsPath);

            // Match any "  propertyName: type" or "  propertyName?: type" lines inside
            // the interface block (two leading spaces = interface member, not a comment).
            var tsProperties = Regex
                .Matches(tsContent, @"^\s{2}(\w+)\??\s*:", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value)
                .OrderBy(n => n)
                .ToList();

            tsProperties.Should().NotBeEmpty(
                "the TypeScript interface must define at least one property");

            // ── Assert they are identical ────────────────────────────────────────────────
            dtoProperties.Should().BeEquivalentTo(
                tsProperties,
                because:
                    "CandidateApplicationDto properties (camelCased) must exactly match the " +
                    "TypeScript CandidateApplication interface. " +
                    "If you add, remove, or rename a DTO property you MUST update " +
                    "JobPortal/ClientApp/src/types/CandidateApplication.ts and re-run `npm run build`.");
        }
    }
}
