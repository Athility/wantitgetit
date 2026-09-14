using System;

namespace StreamDesk.Core;

/// <summary>Base exception for provider failures with a user-facing message.</summary>
public class ProviderException : Exception
{
    public ProviderException(string message)
        : base(message)
    {
    }

    public ProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Friendly, actionable message safe to show in the UI.</summary>
    public virtual string UserMessage => Message;
}

/// <summary>
/// Thrown by provider adapters that require external authorized configuration
/// (API base URL, keys, credentials) which has not been supplied. The UI maps
/// this to an honest "Not configured" state.
/// </summary>
public sealed class ProviderNotConfiguredException : ProviderException
{
    public ProviderNotConfiguredException(string providerDisplayName)
        : base($"{providerDisplayName} is not configured.")
    {
        ProviderDisplayName = providerDisplayName;
    }

    public string ProviderDisplayName { get; }

    public override string UserMessage =>
        $"{ProviderDisplayName} is not configured. Add its authorized API settings in Settings \u2192 Media Providers.";
}
