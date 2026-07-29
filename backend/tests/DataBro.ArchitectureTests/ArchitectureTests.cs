using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace DataBro.ArchitectureTests;

/// <summary>
/// Enforces the module boundaries and layer dependency rules documented in docs/ARCHITECTURE.md.
/// These run in CI on every build so violations fail fast.
/// </summary>
public class ArchitectureTests
{
    private const string Prefix = "DataBro.Modules";

    private static readonly string[] Modules = ["Identity", "Content", "Media", "Search"];

    private static readonly Assembly[] DomainAssemblies =
    [
        typeof(Modules.Identity.Domain.IdentityDomainMarker).Assembly,
        typeof(Modules.Content.Domain.ContentDomainMarker).Assembly,
        typeof(Modules.Media.Domain.MediaDomainMarker).Assembly,
        typeof(Modules.Search.Domain.SearchDomainMarker).Assembly,
    ];

    private static readonly Assembly[] ApplicationAssemblies =
    [
        typeof(Modules.Identity.Application.IdentityApplicationMarker).Assembly,
        typeof(Modules.Content.Application.ContentApplicationMarker).Assembly,
        typeof(Modules.Media.Application.MediaApplicationMarker).Assembly,
        typeof(Modules.Search.Application.SearchApplicationMarker).Assembly,
    ];

    [Fact]
    public void Domain_should_not_depend_on_application_infrastructure_or_api()
    {
        foreach (var assembly in DomainAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(
                    ".Application", ".Infrastructure", ".Api")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(assembly, result));
        }
    }

    [Fact]
    public void Domain_should_not_depend_on_web_or_persistence_frameworks()
    {
        foreach (var assembly in DomainAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(
                    "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(assembly, result));
        }
    }

    [Fact]
    public void Domain_should_not_depend_on_other_modules()
    {
        AssertNoCrossModuleDependency(DomainAssemblies);
    }

    [Fact]
    public void Application_should_not_depend_on_other_modules()
    {
        AssertNoCrossModuleDependency(ApplicationAssemblies);
    }

    private static void AssertNoCrossModuleDependency(Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var owning = Modules.Single(m => assembly.GetName().Name!.Contains($"{Prefix}.{m}."));
            var foreignNamespaces = Modules
                .Where(m => m != owning)
                .Select(m => $"{Prefix}.{m}.")
                .ToArray();

            var result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(foreignNamespaces)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(assembly, result));
        }
    }

    private static string Describe(Assembly assembly, TestResult result)
    {
        var failing = result.FailingTypeNames ?? [];
        return $"{assembly.GetName().Name} violated an architecture rule. Offending types: "
               + string.Join(", ", failing);
    }
}
