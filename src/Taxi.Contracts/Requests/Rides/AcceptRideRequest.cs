using System.ComponentModel.DataAnnotations;

using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Rides;

public class AcceptRideRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.RequiredField)]
    public Guid DriverId { get; set; }
}