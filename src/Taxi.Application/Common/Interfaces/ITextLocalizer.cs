namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// Resolves a <c>LocalizationKeys</c> constant against <c>SharedResource.{culture}.json</c> for the
/// current request's language. For text the Application layer itself must produce — email subjects
/// and bodies — rather than errors, which <c>ProblemExtensions</c> translates in the API.
/// Implemented in the API project (it owns the resource files), like <see cref="ILanguageContext"/>.
/// </summary>
public interface ITextLocalizer
{
    /// <summary>Gets the translated text, formatted with <paramref name="args"/>; the key itself if missing.</summary>
    string this[string key, params object[] args] { get; }
}
