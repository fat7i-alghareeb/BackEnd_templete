using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Drivers;

/// <summary>Body of <c>PUT /api/v1/drivers/{id}/car</c>.</summary>
public class AssignDriverCarRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.CarIdRequired)]
    public Guid CarId { get; set; }
}
