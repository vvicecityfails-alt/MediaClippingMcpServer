namespace MediaClippingMcpServer.Models;

/// <summary>
/// License tiers available for the MCP server
/// </summary>
public enum LicenseTier
{
    /// <summary>
    /// Free tier - local file operations only
    /// </summary>
    Free = 0,

    /// <summary>
    /// Pro tier - all features including YouTube download, batch processing
    /// </summary>
    Pro = 1,

    /// <summary>
    /// Enterprise tier - self-hosted, SLA, custom tools, SSO
    /// </summary>
    Enterprise = 2
}

/// <summary>
/// Features that can be gated by license tier
/// </summary>
public static class LicenseFeatures
{
    public const string LocalFileOperations = "local_file_operations";
    public const string YouTubeDownload = "youtube_download";
    public const string VimeoDownload = "vimeo_download";
    public const string BatchProcessing = "batch_processing";
    public const string PriorityQueue = "priority_queue";
    public const string CustomTools = "custom_tools";
    public const string SSO = "sso";
    public const string AuditLogs = "audit_logs";
    public const string SLA = "sla";
}

/// <summary>
/// License information for a user
/// </summary>
public record LicenseInfo(
    LicenseTier Tier,
    string? LicenseKey,
    string? CustomerEmail,
    DateTime? ExpiresAt,
    Dictionary<string, bool> Features
);

/// <summary>
/// Result of a license validation
/// </summary>
public record LicenseValidationResult(
    bool IsValid,
    LicenseInfo? LicenseInfo,
    string? ErrorMessage
);