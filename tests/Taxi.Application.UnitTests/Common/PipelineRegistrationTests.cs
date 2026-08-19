using MediatR;
using MediatR.Pipeline;

using Microsoft.Extensions.DependencyInjection;

using Taxi.Application.Common.Behaviours;

using Xunit;

namespace Taxi.Application.UnitTests.Common;

/// <summary>
/// The MediatR pipeline is configured by registration order in <c>AddApplication()</c>, and a
/// reorder silently changes behaviour for every request in the system. These tests pin that order
/// and confirm the request pre-processor is actually wired.
/// </summary>
public class PipelineRegistrationTests
{
    [Fact]
    public void Behaviours_AreRegisteredOutermostFirst()
    {
        // Registration order IS execution order: first registered wraps everything after it.
        Assert.Equal(
            [
                typeof(UnhandledExceptionBehaviour<,>),
                typeof(ValidationBehavior<,>),
                typeof(PerformanceBehaviour<,>),
                typeof(CachingBehavior<,>),
            ],
            OurBehaviours());
    }

    [Fact]
    public void UnhandledExceptionBehaviour_IsOutermost()
    {
        // Anything registered before it escapes its try/catch and is never logged with the payload.
        Assert.Equal(typeof(UnhandledExceptionBehaviour<,>), OurBehaviours()[0]);
    }

    [Fact]
    public void ValidationBehaviour_RunsBeforeCaching()
    {
        var behaviours = OurBehaviours();

        // Invalid input must never reach the cache.
        Assert.True(
            behaviours.IndexOf(typeof(ValidationBehavior<,>)) < behaviours.IndexOf(typeof(CachingBehavior<,>)),
            "ValidationBehavior must be registered before CachingBehavior.");
    }

    [Fact]
    public void CachingBehaviour_IsInnermost()
    {
        // Closest to the handler, so a cache hit skips as little work as possible.
        Assert.Equal(typeof(CachingBehavior<,>), OurBehaviours()[^1]);
    }

    [Fact]
    public void LoggingBehaviour_IsRegisteredAsARequestPreProcessor()
    {
        var services = new ServiceCollection().AddApplication();

        var preProcessors = services
            .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(IRequestPreProcessor<>))
            .Select(d => d.ImplementationType)
            .ToList();

        // LoggingBehaviour is an open-generic IRequestPreProcessor, and MediatR's assembly scan
        // does NOT discover those — it only works because AddApplication() calls
        // AddOpenRequestPreProcessor explicitly. Drop that line and every request stops being logged.
        Assert.Contains(typeof(LoggingBehaviour<>), preProcessors);
    }

    [Fact]
    public void Validators_AreRegisteredFromTheAssembly()
    {
        var services = new ServiceCollection().AddApplication();

        var validators = services
            .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(FluentValidation.IValidator<>))
            .ToList();

        Assert.NotEmpty(validators);
    }

    /// <summary>
    /// The behaviours this project owns, in registration order. MediatR prepends its own
    /// <c>RequestPreProcessorBehavior</c> at index 0 to host the pre-processors; that is expected
    /// and is filtered out here so the assertions describe our configuration only.
    /// </summary>
    /// <returns>Our pipeline behaviour implementation types, outermost first.</returns>
    private static List<Type> OurBehaviours()
    {
        var services = new ServiceCollection().AddApplication();

        return services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType!)
            .Where(t => t.Assembly == typeof(ValidationBehavior<,>).Assembly)
            .ToList();
    }
}
