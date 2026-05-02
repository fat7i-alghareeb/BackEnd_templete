using System.ComponentModel;
using System.Text.Json.Serialization;

using Taxi.Domain.Common.Results.Abstractions;

namespace Taxi.Domain.Common.Results;

public static class Result
{
    public static Success Success => default;

    public static Created Created => default;

    public static Deleted Deleted => default;

    public static Updated Updated => default;
}

public sealed class Result<TValue> : IResult<TValue>
{
    private readonly TValue? value = default;

    private readonly List<Error>? errors = null;

    [JsonConstructor]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("For serializer only.", true)]
    public Result(TValue? value, List<Error>? errors, bool isSuccess)
    {
        if (isSuccess)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
            this.errors = [];
            this.IsSuccess = true;
        }
        else
        {
            if (errors == null || errors.Count == 0)
            {
                throw new ArgumentException("Provide at least one error.", nameof(errors));
            }

            this.errors = errors;
            this.value = default!;
            this.IsSuccess = false;
        }
    }

    private Result(Error error)
    {
        this.errors = [error];
    }

    private Result(List<Error> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            throw new ArgumentException("Cannot create an ErrorOr<TValue> from an empty collection of errors. Provide at least one error.", nameof(errors));
        }

        this.errors = errors;

        this.IsSuccess = false;
    }

    private Result(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        this.value = value;

        this.IsSuccess = true;
    }

    public bool IsSuccess { get; }

    public bool IsError => !this.IsSuccess;

    public List<Error> Errors => this.IsError ? this.errors! : [];

    public TValue Value => this.IsSuccess ? this.value! : default!;

    public Error TopError => (this.errors?.Count > 0) ? this.errors[0] : default;

    public TNextValue Match<TNextValue>(Func<TValue, TNextValue> onValue, Func<List<Error>, TNextValue> onError)
        => this.IsSuccess ? onValue(this.Value!) : onError(this.Errors);

    public static implicit operator Result<TValue>(TValue value)
        => new(value);

    public static implicit operator Result<TValue>(Error error)
        => new(error);

    public static implicit operator Result<TValue>(List<Error> errors)
        => new(errors);
}

public readonly record struct Success;

public readonly record struct Created;

public readonly record struct Deleted;

public readonly record struct Updated;