using System;

namespace StreamDesk.Application.Providers;

/// <summary>Stable provider identifiers shared by UI, settings and tests.</summary>
public static class ProviderIds
{
    public const string Tmdb = "tmdb";

    public const string Local = "local";

    public const string M3U = "m3u";

    public const string MovieBox = "moviebox";

    public const string FourKHDHub = "4khdhub";
}

/// <summary>Lifecycle/status shown in Settings → Media Providers.</summary>
public enum ProviderStatus
{
    Enabled,
    Disabled,
    NotConfigured,
    Error
}
