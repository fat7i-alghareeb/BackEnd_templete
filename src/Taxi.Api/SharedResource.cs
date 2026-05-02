namespace Taxi.Api;

/// <summary>
/// Marker class used by IStringLocalizer to locate the JSON resource files at
/// <c>Resources/SharedResource.{culture}.json</c>.
/// Lives at the project's root namespace so that
/// <c>My.Extensions.Localization.Json</c> resolves the file path cleanly without
/// duplicating the "Resources" folder segment.
/// </summary>
public sealed class SharedResource
{
}