using System.ComponentModel.DataAnnotations;

using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Rides;

public class RequestRideRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.CustomerIdRequired)]
    public Guid CustomerId { get; set; }

    [Required(ErrorMessage = LocalizationKeys.Validation.RequiredField)]
    public string PickupEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.RequiredField)]
    public string PickupAr { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.RequiredField)]
    public string DestinationEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.RequiredField)]
    public string DestinationAr { get; set; } = string.Empty;
}