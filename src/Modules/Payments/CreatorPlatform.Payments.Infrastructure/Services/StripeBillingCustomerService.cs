using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure.Services;

public sealed class StripeBillingCustomerService : IBillingCustomerService
{
    private readonly StripeOptions _options;

    public StripeBillingCustomerService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> CreateAsync(
        string email, string name, IReadOnlyDictionary<string, string> metadata, string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var customer = await new CustomerService().CreateAsync(
            new CustomerCreateOptions
            {
                Email = email,
                Name = name,
                Metadata = new Dictionary<string, string>(metadata),
            },
            new RequestOptions { ApiKey = _options.SecretKey, IdempotencyKey = idempotencyKey },
            ct);

        return customer.Id;
    }
}
