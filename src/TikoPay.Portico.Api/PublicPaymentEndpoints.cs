using Microsoft.EntityFrameworkCore;
using TikoPay.Portico.Contracts;
using TikoPay.Portico.Persistence;

namespace TikoPay.Portico.Api;

internal static class PublicPaymentEndpoints
{
    public static IEndpointRouteBuilder MapPublicPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/public/payment-intents/resolve/{intentToken}", async (
            string intentToken,
            PorticoDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(intentToken))
            {
                return Results.BadRequest(new { message = "intentToken is required." });
            }

            var intent = await (
                from paymentIntent in dbContext.PaymentIntents.AsNoTracking()
                join merchant in dbContext.Merchants.AsNoTracking() on paymentIntent.MerchantId equals merchant.Id
                where paymentIntent.IntentReference == intentToken || paymentIntent.Id.ToString() == intentToken
                select new ResolveCustomerPaymentIntentResponse(
                    paymentIntent.Id,
                    paymentIntent.IntentReference,
                    merchant.DisplayName,
                    paymentIntent.AmountMinor,
                    paymentIntent.Currency,
                    paymentIntent.Status.ToString(),
                    paymentIntent.ExpiresAt,
                    paymentIntent.Description))
                .FirstOrDefaultAsync(cancellationToken);

            return intent is null ? Results.NotFound() : Results.Ok(intent);
        }).WithName("ResolveCustomerPaymentIntent");

        return endpoints;
    }
}
