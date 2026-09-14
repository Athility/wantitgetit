using System;
using System.IO;

namespace StreamDesk.Infrastructure;

/// <summary>Per-user writable locations for database, caches and downloads.</summary>
public static class AppPaths
{
    public static string Root { get; } = ResolveRoot();

    public static string DatabasePath => Path.Combine(Root, "streamdesk.db");

    public static string ImageCacheDirectory => Path.Combine(Root, "ImageCache");

    public static string DownloadDirectory => Path.Combine(Root, "Downloads");

    private static string ResolveRoot()
    {
        // StreamDeskData override helps tests and portable setups.
        var overridePath = Environment.GetEnvironmentVariable("STREAMDESK_DATA");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "StreamDesk");
        }

        // Portable fallback for non-Windows dev machines/tests.
        return Path.Combine(AppContext.BaseDirectory, "StreamDeskData");
    }
}
