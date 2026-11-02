using MediatR;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Taxi.Application.Common.Behaviours;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Commands.GenerateToken;

using Xunit;

namespace Taxi.Application.UnitTests.Common;

/// <summary>
/// The request behaviours log every payload with <c>{@Request}</c>. Passwords and tokens once went
/// to the logs in clear text that way; these tests keep it from happening again.
/// </summary>
public class SensitiveRequestTests
{
    private static readonly string[] SensitiveNames = ["Password", "Token", "Secret"];

    [Fact]
    public void EveryRequestCarryingASecretOrAStream_IsMarkedSensitive()
    {
        var offenders = typeof(ISensitiveRequest).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(IsRequest))
            .Where(t => !typeof(ISensitiveRequest).IsAssignableFrom(t))
            .Where(t => t.GetProperties().Any(p =>
                typeof(Stream).IsAssignableFrom(p.PropertyType)
                || SensitiveNames.Any(n => p.Name.Contains(n, StringComparison.OrdinalIgnoreCase))))
            .Select(t => t.Name)
            .ToList();

        Assert.True(offenders.Count == 0, $"Mark these requests ISensitiveRequest: {string.Join(", ", offenders)}");
    }

    [Fact]
    public async Task LoggingBehaviour_NeverWritesTheSensitivePayload()
    {
        var logger = new CapturingLogger<GenerateTokenCommand>();
        var behaviour = new LoggingBehaviour<GenerateTokenCommand>(logger, Substitute.For<IUser>());

        await behaviour.Process(new GenerateTokenCommand("a@b.c", "hunter2"), default);

        var arguments = Assert.Single(logger.States);
        Assert.DoesNotContain(arguments, a => a is GenerateTokenCommand);
        Assert.Contains(RequestLogPayload.Redacted, arguments);
    }

    private static bool IsRequest(Type i) =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<object?[]> States { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IReadOnlyList<KeyValuePair<string, object?>> pairs
                ? pairs.Select(p => p.Value).ToArray()
                : [state];
            this.States.Add(values);
        }
    }
}
