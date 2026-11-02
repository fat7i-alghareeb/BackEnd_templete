using System.Reflection;

using NetArchTest.Rules;

using Xunit;

namespace Taxi.ArchitectureTests;

/// <summary>
/// The project-reference arrows in AGENTS.md §1, enforced at type level. A project reference can be
/// added in a minute; these tests make the build fail the moment a type actually uses it.
/// </summary>
public class LayerDependencyTests
{
    private const string Contracts = "Taxi.Contracts";
    private const string Domain = "Taxi.Domain";
    private const string Application = "Taxi.Application";
    private const string Infrastructure = "Taxi.Infrastructure";
    private const string Api = "Taxi.Api";

    [Fact]
    public void Contracts_DependsOnNoOtherLayer()
    {
        AssertNoDependency(Assemblies.Contracts, Domain, Application, Infrastructure, Api);
    }

    [Fact]
    public void Domain_DependsOnlyOnContracts()
    {
        AssertNoDependency(Assemblies.Domain, Application, Infrastructure, Api);
    }

    [Fact]
    public void Domain_DoesNotUseEfCoreOrAspNetCore()
    {
        AssertNoDependency(Assemblies.Domain, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        AssertNoDependency(Assemblies.Application, Infrastructure, Api);
    }

    [Fact]
    public void Application_DoesNotUseAspNetCore()
    {
        // No HttpContext, IActionResult or Identity types in handlers.
        AssertNoDependency(Assemblies.Application, "Microsoft.AspNetCore");
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        AssertNoDependency(Assemblies.Infrastructure, Api);
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        var offenders = string.Join(", ", result.FailingTypeNames ?? []);

        Assert.True(result.IsSuccessful, $"{assembly.GetName().Name} must not depend on {string.Join(", ", forbidden)}. Offenders: {offenders}");
    }
}
