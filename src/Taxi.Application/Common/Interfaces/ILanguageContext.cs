using Taxi.Contracts.Common;

namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// Provides the language resolved from the current HTTP request's
/// Accept-Language header (or the configured default).
/// Injected into query handlers to drive SQL-level bilingual projections.
/// </summary>
public interface ILanguageContext
{
    /// <summary>
    /// Gets the resolved language code for the current request. One of the
    /// constants on <see cref="Languages"/> (e.g. <see cref="Languages.En"/>,
    /// <see cref="Languages.Ar"/>). Falls back to <see cref="Languages.Default"/>
    /// if no valid Accept-Language header is present.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Gets a value indicating whether returns true when the resolved language is Arabic.
    /// Convenience property for use in EF Core .Select() expressions.
    /// </summary>
    bool IsArabic => this.Language == Languages.Ar;
}