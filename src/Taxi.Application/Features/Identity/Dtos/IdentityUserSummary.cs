namespace Taxi.Application.Features.Identity.Dtos;

/// <summary>The minimum about an account a handler needs to decide whether to send an email.</summary>
public sealed record IdentityUserSummary(string UserId, string Email, bool EmailConfirmed);
