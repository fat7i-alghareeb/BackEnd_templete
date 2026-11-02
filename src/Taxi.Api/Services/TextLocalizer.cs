using Microsoft.Extensions.Localization;

using Taxi.Application.Common.Interfaces;

namespace Taxi.Api.Services;

/// <summary>
/// <see cref="ITextLocalizer"/> over <c>SharedResource.{culture}.json</c>. The culture is the one
/// <c>UseRequestLocalization</c> resolved from Accept-Language, so emails follow the caller's language.
/// </summary>
public sealed class TextLocalizer(IStringLocalizer<SharedResource> localizer) : ITextLocalizer
{
    private readonly IStringLocalizer<SharedResource> localizer = localizer;

    public string this[string key, params object[] args] =>
        args.Length > 0 ? this.localizer[key, args].Value : this.localizer[key].Value;
}
