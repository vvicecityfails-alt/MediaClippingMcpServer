using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaClippingMcpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaClippingMcpServer.Services;

/// <summary>
/// Configuration for license service
/// </summary>
public class LicenseServiceOptions
{
    public const string SectionName = "License";

    /// <summary>
    /// Stripe secret key for verifying subscriptions
    /// </summary>
    public string? StripeSecretKey { get; set; }

    /// <summary>
    /// Stripe webhook secret for validating webhooks
    /// </summary>
    public string? StripeWebhookSecret { get; set; }

    /// <summary>
    /// Public key for verifying signed license keys (RSA)
    /// </summary>
    public string? LicensePublicKey { get; set; }

    /// <summary>
    /// Path to license file (for offline validation)
    /// </summary>
    public string? LicenseFilePath { get; set; } = "license.json";

    /// <summary>
    /// Cache duration for license validation
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// License service implementation with Stripe integration and offline key validation
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly ILogger<LicenseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly LicenseServiceOptions _options;
    private readonly HttpClient _httpClient;
    private LicenseInfo? _cachedLicense;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    // Feature matrix per tier
    private static readonly Dictionary<LicenseTier, Dictionary<string, bool>> TierFeatures = new()
    {
        [LicenseTier.Free] = new()
        {
            [LicenseFeatures.LocalFileOperations] = true,
            [LicenseFeatures.YouTubeDownload] = false,
            [LicenseFeatures.VimeoDownload] = false,
            [LicenseFeatures.BatchProcessing] = false,
            [LicenseFeatures.PriorityQueue] = false,
            [LicenseFeatures.CustomTools] = false,
            [LicenseFeatures.SSO] = false,
            [LicenseFeatures.AuditLogs] = false,
            [LicenseFeatures.SLA] = false,
        },
        [LicenseTier.Pro] = new()
        {
            [LicenseFeatures.LocalFileOperations] = true,
            [LicenseFeatures.YouTubeDownload] = true,
            [LicenseFeatures.VimeoDownload] = true,
            [LicenseFeatures.BatchProcessing] = true,
            [LicenseFeatures.PriorityQueue] = true,
            [LicenseFeatures.CustomTools] = false,
            [LicenseFeatures.SSO] = false,
            [LicenseFeatures.AuditLogs] = false,
            [LicenseFeatures.SLA] = false,
        },
        [LicenseTier.Enterprise] = new()
        {
            [LicenseFeatures.LocalFileOperations] = true,
            [LicenseFeatures.YouTubeDownload] = true,
            [LicenseFeatures.VimeoDownload] = true,
            [LicenseFeatures.BatchProcessing] = true,
            [LicenseFeatures.PriorityQueue] = true,
            [LicenseFeatures.CustomTools] = true,
            [LicenseFeatures.SSO] = true,
            [LicenseFeatures.AuditLogs] = true,
            [LicenseFeatures.SLA] = true,
        }
    };

    public LicenseService(
        ILogger<LicenseService> logger,
        IConfiguration configuration,
        IOptions<LicenseServiceOptions> options,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _options = options.Value;
        _httpClient = httpClient;
    }

    public async Task<LicenseValidationResult> ValidateLicenseAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try Stripe validation first if configured
            if (!string.IsNullOrEmpty(_options.StripeSecretKey))
            {
                var stripeResult = await ValidateWithStripeAsync(licenseKey, cancellationToken);
                if (stripeResult.IsValid)
                    return stripeResult;
            }

            // Fall back to signed license key validation
            var signedResult = ValidateSignedLicenseKey(licenseKey);
            if (signedResult.IsValid)
                return signedResult;

            return new LicenseValidationResult(false, null, "Invalid license key");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License validation failed");
            return new LicenseValidationResult(false, null, $"Validation error: {ex.Message}");
        }
    }

    public async Task<LicenseInfo> GetCurrentLicenseAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedLicense != null && DateTime.UtcNow < _cacheExpiry)
                return _cachedLicense;

            _cachedLicense = await LoadLicenseAsync(cancellationToken);
            _cacheExpiry = DateTime.UtcNow.Add(_options.CacheDuration);
            return _cachedLicense;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);
        return license.Features.TryGetValue(featureName, out var enabled) && enabled;
    }

    public async Task<LicenseTier> GetCurrentTierAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetCurrentLicenseAsync(cancellationToken);
        return license.Tier;
    }

    public async Task SetLicenseKeyAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        var result = await ValidateLicenseAsync(licenseKey, cancellationToken);
        if (!result.IsValid || result.LicenseInfo == null)
            throw new ArgumentException(result.ErrorMessage ?? "Invalid license key");

        // Save to file
        var licenseFile = _options.LicenseFilePath ?? "license.json";
        var json = JsonSerializer.Serialize(result.LicenseInfo, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(licenseFile, json, cancellationToken);

        // Invalidate cache
        _cachedLicense = result.LicenseInfo;
        _cacheExpiry = DateTime.UtcNow.Add(_options.CacheDuration);

        _logger.LogInformation("License key saved successfully. Tier: {Tier}", result.LicenseInfo.Tier);
    }

    private async Task<LicenseInfo> LoadLicenseAsync(CancellationToken cancellationToken)
    {
        // 1. Check environment variable (highest priority for containers)
        var envLicenseKey = Environment.GetEnvironmentVariable("MEDIA_CLIP_LICENSE_KEY");
        if (!string.IsNullOrEmpty(envLicenseKey))
        {
            var result = await ValidateLicenseAsync(envLicenseKey, cancellationToken);
            if (result.IsValid && result.LicenseInfo != null)
                return result.LicenseInfo;
        }

        // 2. Check license file
        var licenseFile = _options.LicenseFilePath ?? "license.json";
        if (File.Exists(licenseFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(licenseFile, cancellationToken);
                var licenseInfo = JsonSerializer.Deserialize<LicenseInfo>(json);
                if (licenseInfo != null)
                {
                    // Validate it's still valid
                    if (licenseInfo.ExpiresAt == null || licenseInfo.ExpiresAt > DateTime.UtcNow)
                        return licenseInfo;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load license file");
            }
        }

        // 3. Return free tier as default
        return GetFreeTierLicense();
    }

    private async Task<LicenseValidationResult> ValidateWithStripeAsync(string licenseKey, CancellationToken cancellationToken)
    {
        try
        {
            // licenseKey should be a Stripe customer ID or subscription ID
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.stripe.com/v1/customers/{licenseKey}/subscriptions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.StripeSecretKey);
            request.Headers.Add("Stripe-Version", "2024-04-10");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new LicenseValidationResult(false, null, "Stripe validation failed");

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var stripeData = JsonSerializer.Deserialize<JsonElement>(content);

            // Check for active subscription
            if (stripeData.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
            {
                foreach (var sub in data.EnumerateArray())
                {
                    if (sub.TryGetProperty("status", out var status) &&
                        status.GetString() == "active" &&
                        sub.TryGetProperty("items", out var items) &&
                        items.TryGetProperty("data", out var itemsData) &&
                        itemsData.GetArrayLength() > 0)
                    {
                        // Determine tier from price ID or metadata
                        var tier = DetermineTierFromSubscription(sub);
                        var licenseInfo = CreateLicenseInfo(tier, licenseKey, "stripe_customer");
                        return new LicenseValidationResult(true, licenseInfo, null);
                    }
                }
            }

            return new LicenseValidationResult(false, null, "No active subscription found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe validation error");
            return new LicenseValidationResult(false, null, $"Stripe error: {ex.Message}");
        }
    }

    private LicenseValidationResult ValidateSignedLicenseKey(string licenseKey)
    {
        try
        {
            if (string.IsNullOrEmpty(_options.LicensePublicKey))
                return new LicenseValidationResult(false, null, "License public key not configured");

            // Parse license key format: base64(payload).base64(signature)
            var parts = licenseKey.Split('.');
            if (parts.Length != 2)
                return new LicenseValidationResult(false, null, "Invalid license key format");

            var payloadBase64 = parts[0];
            var signatureBase64 = parts[1];

            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var signatureBytes = Convert.FromBase64String(signatureBase64);

            // Verify signature
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_options.LicensePublicKey);

            var payloadString = Encoding.UTF8.GetString(payloadBytes);
            if (!rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return new LicenseValidationResult(false, null, "Invalid license signature");

            // Parse payload
            var payload = JsonSerializer.Deserialize<LicensePayload>(payloadString);
            if (payload == null)
                return new LicenseValidationResult(false, null, "Invalid license payload");

            // Check expiration
            if (payload.ExpiresAt.HasValue && payload.ExpiresAt.Value < DateTime.UtcNow)
                return new LicenseValidationResult(false, null, "License expired");

            var licenseInfo = CreateLicenseInfo(payload.Tier, licenseKey, payload.CustomerEmail);
            return new LicenseValidationResult(true, licenseInfo, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signed license validation error");
            return new LicenseValidationResult(false, null, $"Validation error: {ex.Message}");
        }
    }

    private LicenseInfo GetFreeTierLicense()
    {
        return new LicenseInfo(
            Tier: LicenseTier.Free,
            LicenseKey: null,
            CustomerEmail: null,
            ExpiresAt: null,
            Features: TierFeatures[LicenseTier.Free]
        );
    }

    private LicenseInfo CreateLicenseInfo(LicenseTier tier, string? licenseKey, string? customerEmail)
    {
        return new LicenseInfo(
            Tier: tier,
            LicenseKey: licenseKey,
            CustomerEmail: customerEmail,
            ExpiresAt: tier == LicenseTier.Free ? null : DateTime.UtcNow.AddYears(1),
            Features: TierFeatures[tier]
        );
    }

    private LicenseTier DetermineTierFromSubscription(JsonElement subscription)
    {
        // Check metadata for tier
        if (subscription.TryGetProperty("metadata", out var metadata))
        {
            if (metadata.TryGetProperty("tier", out var tierProp))
            {
                return Enum.TryParse<LicenseTier>(tierProp.GetString(), true, out var tier) ? tier : LicenseTier.Pro;
            }
        }

        // Default to Pro for any active subscription
        return LicenseTier.Pro;
    }

    private record LicensePayload(
        LicenseTier Tier,
        string CustomerEmail,
        DateTime? ExpiresAt,
        string IssuedAt
    );
}