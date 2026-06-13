using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Orders.Application.Dtos;

public sealed class CreateCheckoutRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;
}
