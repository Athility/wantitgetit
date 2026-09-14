using System;
using System.Reflection;

namespace StreamDesk.Core;

/// <summary>
/// Single source of truth for product identity and version at runtime. The
/// build pipeline keeps assembly/Git tag/Release versions aligned at 1.0.0.
/// </summary>
public static class AppVersion
{
    public const string ProductName = "StreamDesk";

    public const string RepositoryUrl = "https://github.com/streamdesk/streamdesk";

    /// <summary>Informational version from the assembly (e.g. "1.0.0").</summary>
    public static string Informational =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "1.0.0";

    /// <summary>Display version for the About page, e.g. "Version 1.0.0".</summary>
    public static string Display => $"Version {Informational}";
}
