using Taxi.Application.Common.Interfaces;

namespace Taxi.Application.UnitTests.TestDoubles;

/// <summary>Returns the key and its arguments, so tests can assert which text was asked for.</summary>
public sealed class FakeTextLocalizer : ITextLocalizer
{
    public string this[string key, params object[] args] =>
        args.Length == 0 ? key : $"{key}|{string.Join('|', args)}";
}
