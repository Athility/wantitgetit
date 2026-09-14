namespace StreamDesk.Core;

/// <summary>A Live TV channel imported from a user-provided M3U/M3U8 playlist.</summary>
public sealed class ChannelInfo
{
    public ChannelInfo()
    {
    }

    public ChannelInfo(string id, string name, string streamUrl)
    {
        Id = id;
        Name = name;
        StreamUrl = streamUrl;
    }

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The stream URL exactly as supplied by the user's playlist.</summary>
    public string StreamUrl { get; set; } = string.Empty;

    /// <summary>Group assigned via group-title in the playlist, or "Ungrouped".</summary>
    public string Group { get; set; } = "Ungrouped";

    public string? LogoUrl { get; set; }
}
