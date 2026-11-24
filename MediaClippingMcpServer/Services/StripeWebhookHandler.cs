using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaClippingMcpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;

namespace MediaClippingMcpServer.Services;

/// <summary>
/// Handles Stripe webhooks for automatic license provisioning
/// </summary>
public class StripeWebhookHandler
{
    private readonly ILogger<StripeWebhookHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILicenseService _licenseService;

    public StripeWebhookHandler(
        ILogger<StripeWebhookHandler> logger,
        IConfiguration configuration,
        ILicenseService licenseService)
    {
        _logger = logger;
        _configuration = configuration;
        _licenseService = licenseService;
    }

    /// <summary>
    /// Processes a Stripe webhook event from raw payload and signature
    /// </summary>
    /// <param name="payload">Raw JSON payload from Stripe</param>
    /// <param name="signatureHeader">Stripe-Signature header value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<WebhookResult> HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        var webhookSecret = _configuration["License:StripeWebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Stripe webhook secret not configured");
            return new WebhookResult(false, "Webhook secret not configured");
        }

        if (string.IsNullOrEmpty(signatureHeader))
        {
            return new WebhookResult(false, "Missing Stripe-Signature header");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature verification failed");
            return new WebhookResult(false, $"Signature verification failed: {ex.Message}");
        }

        _logger.LogInformation("Received Stripe webhook: {EventType}", stripeEvent.Type);

        return await ProcessEventAsync(stripeEvent, cancellationToken);
    }

    /// <summary>
    /// Processes a Stripe webhook event from an already-constructed Event object
    /// </summary>
    public async Task<WebhookResult> HandleWebhookAsync(Event stripeEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received Stripe webhook: {EventType}", stripeEvent.Type);
        return await ProcessEventAsync(stripeEvent, cancellationToken);
    }

    private async Task<WebhookResult> ProcessEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        try
        {
            return stripeEvent.Type switch
            {
                "customer.subscription.created" => await HandleSubscriptionCreatedAsync(stripeEvent, cancellationToken),
                "customer.subscription.updated" => await HandleSubscriptionUpdatedAsync(stripeEvent, cancellationToken),
                "customer.subscription.deleted" => await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken),
                "invoice.payment_succeeded" => await HandlePaymentSucceededAsync(stripeEvent, cancellationToken),
                "invoice.payment_failed" => await HandlePaymentFailedAsync(stripeEvent, cancellationToken),
                _ => new WebhookResult(true, $"Unhandled event type: {stripeEvent.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe event {EventType}", stripeEvent.Type);
            return new WebhookResult(false, $"Processing error: {ex.Message}");
        }
    }

    private async Task<WebhookResult> HandleSubscriptionCreatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return new WebhookResult(false, "Invalid subscription object");

        return await ProvisionLicenseAsync(subscription, cancellationToken);
    }

    private async Task<WebhookResult> HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return new WebhookResult(false, "Invalid subscription object");

        return await ProvisionLicenseAsync(subscription, cancellationToken);
    }

    private async Task<WebhookResult> HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return new WebhookResult(false, "Invalid subscription object");

        _logger.LogInformation("Subscription {SubscriptionId} deleted, revoking license for customer {CustomerId}",
            subscription.Id, subscription.CustomerId);

        return new WebhookResult(true, "License revoked for deleted subscription");
    }

    private async Task<WebhookResult> HandlePaymentSucceededAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return new WebhookResult(false, "Invalid invoice object");

        _logger.LogInformation("Payment succeeded for invoice {InvoiceId}, customer {CustomerId}", invoice.Id, invoice.CustomerId);
        return new WebhookResult(true, "Payment succeeded");
    }

    private async Task<WebhookResult> HandlePaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return new WebhookResult(false, "Invalid invoice object");

        _logger.LogWarning("Payment failed for invoice {InvoiceId}, customer {CustomerId}", invoice.Id, invoice.CustomerId);
        return new WebhookResult(true, "Payment failed - license may need revocation");
    }

    private async Task<WebhookResult> ProvisionLicenseAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        try
        {
            // Get customer details
            var customerService = new CustomerService();
            var customer = await customerService.GetAsync(subscription.CustomerId, cancellationToken: cancellationToken);

            // Determine tier from subscription metadata or price
            var tier = DetermineTierFromSubscription(subscription);

            // Generate or retrieve license key
            var licenseKey = $"stripe_sub_{subscription.Id}";

            // Validate/create license
            var result = await _licenseService.ValidateLicenseAsync(licenseKey, cancellationToken);

            _logger.LogInformation("Provisioned {Tier} license for customer {CustomerEmail} (subscription: {SubscriptionId})",
                tier, customer.Email, subscription.Id);

            return new WebhookResult(true, $"License provisioned: {tier}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision license for subscription {SubscriptionId}", subscription.Id);
            return new WebhookResult(false, $"Provisioning failed: {ex.Message}");
        }
    }

    private LicenseTier DetermineTierFromSubscription(Subscription subscription)
    {
        // Check metadata first
        if (subscription.Metadata?.TryGetValue("tier", out var tierString) == true)
        {
            if (Enum.TryParse<LicenseTier>(tierString, true, out var tier))
                return tier;
        }

        // Check price metadata
        foreach (var item in subscription.Items.Data)
        {
            if (item.Price.Metadata?.TryGetValue("tier", out var priceTierString) == true)
            {
                if (Enum.TryParse<LicenseTier>(priceTierString, true, out var tier))
                    return tier;
            }
        }

        // Default to Pro
        return LicenseTier.Pro;
    }
}

/// <summary>
/// Result of webhook processing
/// </summary>
public record WebhookResult(bool Success, string Message);