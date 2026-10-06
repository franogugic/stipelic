using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Orders.Application.Dtos;

public sealed class CreateCheckoutRequestDto
{
    /// <summary>Optional: when given it is prefilled (and locked) in Stripe Checkout; otherwise Stripe asks for it
    /// and the order gets it from checkout.session.completed.</summary>
    [EmailAddress]
    [MaxLength(254)]
    public string? Email { get; init; }
}
