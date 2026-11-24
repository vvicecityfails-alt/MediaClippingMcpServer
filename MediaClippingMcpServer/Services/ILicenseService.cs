using MediaClippingMcpServer.Models;

namespace MediaClippingMcpServer.Services;

/// <summary>
/// Service for validating and managing licenses
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Validates a license key and returns license info
    /// </summary>
    Task<LicenseValidationResult> ValidateLicenseAsync(string licenseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current license info (from environment, config, or file)
    /// </summary>
    Task<LicenseInfo> GetCurrentLicenseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific feature is enabled for the current license
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(string featureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current license tier
    /// </summary>
    Task<LicenseTier> GetCurrentTierAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a license key (for CLI/configuration)
    /// </summary>
    Task SetLicenseKeyAsync(string licenseKey, CancellationToken cancellationToken = default);
}