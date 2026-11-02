using System.Reflection;

using FluentValidation;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using NetArchTest.Rules;

using Taxi.Api.Controllers;
using Taxi.Domain.Common;

using Xunit;

namespace Taxi.ArchitectureTests;

/// <summary>The "MUST" rules from AGENTS.md §2 and §3 that can be checked mechanically.</summary>
public class ConventionTests
{
    [Fact]
    public void Entities_HaveNoPublicConstructors()
    {
        // Creation goes through a static Create(...) returning Result<T>.
        var offenders = Assemblies.Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Entity)))
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(t => t.Name)
            .ToList();

        Assert.True(offenders.Count == 0, $"Entities with public constructors: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Entities_HaveNoPublicSetters_ExceptInterceptorStampedFields()
    {
        // Audit and soft-delete fields are stamped by Infrastructure interceptors (documented exception).
        var allowed = typeof(AuditableEntity).GetProperties().Select(p => p.Name)
            .Concat(typeof(ISoftDeletable).GetProperties().Select(p => p.Name))
            .ToHashSet();

        var offenders = Assemblies.Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Entity)))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } && !allowed.Contains(p.Name))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Entity properties with public setters: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Handlers_AreNamedHandler_AndLiveUnderFeatures()
    {
        var handlers = Types.InAssembly(Assemblies.Application)
            .That().ImplementInterface(typeof(IRequestHandler<,>))
            .Or().ImplementInterface(typeof(INotificationHandler<>));

        AssertSuccess(handlers.Should().HaveNameEndingWith("Handler").GetResult(), "Handler names must end with 'Handler'");
        AssertSuccess(
            Types.InAssembly(Assemblies.Application).That().ImplementInterface(typeof(IRequestHandler<,>))
                .Should().ResideInNamespaceStartingWith("Taxi.Application.Features").GetResult(),
            "Request handlers must live under Taxi.Application.Features");
    }

    [Fact]
    public void Validators_AreNamedValidator()
    {
        AssertSuccess(
            Types.InAssembly(Assemblies.Application).That().Inherit(typeof(AbstractValidator<>))
                .Should().HaveNameEndingWith("Validator").And().BeSealed().GetResult(),
            "Validators must be sealed and end with 'Validator'");
    }

    [Fact]
    public void Controllers_InheritApiController_AndAreSealed()
    {
        AssertSuccess(
            Types.InAssembly(Assemblies.Api).That().Inherit(typeof(ControllerBase)).And().DoNotHaveName(nameof(ApiController))
                .Should().Inherit(typeof(ApiController)).And().BeSealed().GetResult(),
            "Controllers must be sealed and inherit ApiController");
    }

    [Fact]
    public void Controllers_DoNotTouchInfrastructureOrEfCore()
    {
        // No AppDbContext, UserManager<AppUser> or other Infrastructure type in a controller.
        AssertSuccess(
            Types.InAssembly(Assemblies.Api).That().ResideInNamespace("Taxi.Api.Controllers")
                .ShouldNot().HaveDependencyOnAny("Taxi.Infrastructure", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Identity")
                .GetResult(),
            "Controllers must not depend on Infrastructure, EF Core or Identity");
    }

    [Fact]
    public void ContractRequests_AreDataOnly()
    {
        // Plain classes with properties: no methods beyond accessors and the object overrides.
        var offenders = Assemblies.Contracts.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Taxi.Contracts.Requests", StringComparison.Ordinal) == true && t.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => $"{t.Name}.{m.Name}"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Request contracts with methods: {string.Join(", ", offenders)}");
    }

    private static void AssertSuccess(TestResult result, string rule) =>
        Assert.True(result.IsSuccessful, $"{rule}. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
