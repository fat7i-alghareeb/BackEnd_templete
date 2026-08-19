using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Domain.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void ImplicitConversion_FromValue_ProducesSuccess()
    {
        Result<string> result = "ok";

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
        Assert.Equal("ok", result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ImplicitConversion_FromError_ProducesFailure()
    {
        Result<string> result = Error.NotFound("Test.NotFound", "Nothing here.");

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Equal("Test.NotFound", result.TopError.Code);
        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
    }

    [Fact]
    public void ImplicitConversion_FromErrorList_KeepsEveryError()
    {
        // This is the path ValidationBehavior relies on when it returns (dynamic)errors.
        var errors = new List<Error>
        {
            Error.Validation("A", "first"),
            Error.Validation("B", "second"),
        };

        Result<string> result = errors;

        Assert.True(result.IsError);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("A", result.TopError.Code);
    }

    [Fact]
    public void Match_InvokesTheValueBranch_OnSuccess()
    {
        Result<int> result = 42;

        var matched = result.Match(value => $"value:{value}", errors => $"errors:{errors.Count}");

        Assert.Equal("value:42", matched);
    }

    [Fact]
    public void Match_InvokesTheErrorBranch_OnFailure()
    {
        Result<int> result = Error.Conflict("Test.Conflict", "Clash.");

        var matched = result.Match(value => $"value:{value}", errors => $"errors:{errors.Count}");

        Assert.Equal("errors:1", matched);
    }

    [Fact]
    public void ValidationForProperty_CarriesThePropertyName()
    {
        var error = Error.ValidationForProperty("Year", "Validation.Year.Invalid");

        Assert.Equal("Year", error.PropertyName);
        Assert.Equal("Validation.Year.Invalid", error.Code);
        Assert.Equal(ErrorKind.Validation, error.Type);
    }

    [Fact]
    public void Args_AreNull_WhenNoneAreSupplied()
    {
        var withoutArgs = Error.Validation("Code", "Description");
        var withArgs = Error.Validation("Code", "Description", 1886, 2026);

        Assert.Null(withoutArgs.Args);
        Assert.Equal([1886, 2026], withArgs.Args);
    }

    [Fact]
    public void TopError_IsDefault_OnSuccess()
    {
        Result<string> result = "ok";

        Assert.Equal(default, result.TopError);
    }
}
