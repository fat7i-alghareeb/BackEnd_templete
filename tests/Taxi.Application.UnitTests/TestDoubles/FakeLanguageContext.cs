using Taxi.Application.Common.Interfaces;
using Taxi.Contracts.Common;

namespace Taxi.Application.UnitTests.TestDoubles;

public sealed class FakeLanguageContext(string language = Languages.En) : ILanguageContext
{
    public string Language { get; set; } = language;
}
