namespace StreamDesk.Core;

/// <summary>A persisted download task.</summary>
public sealed class DownloadTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ItemId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Episode-style sub-label, e.g. "S1 E4 - Title".</summary>
    public string? SubLabel { get; set; }

    /// <summary>HTTP(S) source URL the provider resolved.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Absolute destination file path.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    public long BytesDownloaded { get; set; }

    public long? TotalBytes { get; set; }

    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;

    /// <summary>Last observed speed in bytes/second (runtime only).</summary>
    public double SpeedBytesPerSecond { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
