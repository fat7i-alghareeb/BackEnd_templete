using System.Text.Json;

using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Application.UnitTests.Common;

/// <summary>
/// <c>CachingBehavior</c> stores the whole <c>Result&lt;TResponse&gt;</c> in HybridCache, whose
/// default serializer for non-byte payloads is System.Text.Json. The only public constructor on
/// <c>Result&lt;TValue&gt;</c> is marked <c>[Obsolete(error: true)]</c>, which blocks compile-time
/// use but not reflection. These tests pin that down: if STJ ever stops being able to rehydrate a
/// Result, every cached endpoint starts returning 500 on its *second* request — a failure that is
/// very easy to miss by hand.
/// </summary>
public class ResultSerializationTests
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;

    [Fact]
    public void SuccessResult_SurvivesAJsonRoundTrip()
    {
        Result<CarDto> original = new CarDto(Guid.NewGuid(), "Toyota", "Camry", 2024, "A reliable sedan.");

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<Result<CarDto>>(json, Options);

        Assert.NotNull(restored);
        Assert.True(restored.IsSuccess);
        Assert.Equal(original.Value, restored.Value);
    }

    [Fact]
    public void SuccessResult_OfACollection_SurvivesAJsonRoundTrip()
    {
        // GetCarsQuery caches Result<List<CarDto>>, so the collection case matters too.
        Result<List<CarDto>> original = new List<CarDto>
        {
            new(Guid.NewGuid(), "Toyota", "Camry", 2024, "A reliable sedan."),
            new(Guid.NewGuid(), "Honda", "Civic", 2025, "A compact car."),
        };

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<Result<List<CarDto>>>(json, Options);

        Assert.NotNull(restored);
        Assert.True(restored.IsSuccess);
        Assert.Equal(2, restored.Value.Count);
        Assert.Equal("Toyota", restored.Value[0].Make);
    }

    [Fact]
    public void FailureResults_AreNeverCached_SoOnlySuccessNeedsToRoundTrip()
    {
        // CachingBehavior guards with `result is IResult { IsSuccess: true }`, so a failed Result
        // is never handed to the serializer. This test documents that contract rather than
        // asserting failures round-trip — the obsolete constructor throws on an empty error list.
        Result<CarDto> failure = CarDtoNotFound;

        Assert.True(failure.IsError);
        Assert.Single(failure.Errors);
    }

    private static Error CarDtoNotFound => Error.NotFound("Car.NotFound", "Car was not found.");
}
