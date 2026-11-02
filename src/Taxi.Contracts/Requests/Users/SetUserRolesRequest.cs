using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Users;

/// <summary>
/// Body of <c>PUT /api/v1/users/{userId}/roles</c>. Replaces the user's whole role set.
/// </summary>
public class SetUserRolesRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.RolesRequired)]
    [MinLength(1, ErrorMessage = LocalizationKeys.Validation.RolesRequired)]
    public List<string> Roles { get; set; } = [];
}
