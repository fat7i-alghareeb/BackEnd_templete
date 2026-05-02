using Microsoft.AspNetCore.Localization;

using Taxi.Application.Common.Interfaces;
using Taxi.Contracts.Common;

namespace Taxi.Api.Services;

/// <summary>
/// Read-only accessor for the request culture resolved by
/// <see cref="Microsoft.AspNetCore.Builder.RequestLocalizationMiddleware"/>.
/// Async-safe: never mutates <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>.
/// </summary>
public sealed class LanguageContext(IHttpContextAccessor accessor) : ILanguageContext
{
    public string Language =>
        accessor.HttpContext?.Features.Get<IRequestCultureFeature>()
            ?.RequestCulture.UICulture.TwoLetterISOLanguageName
        ?? Languages.Default;
}