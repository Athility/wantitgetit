namespace StreamDesk.Core;

/// <summary>A cast/crew member of a movie or show.</summary>
public sealed class CastMember
{
    public CastMember()
    {
    }

    public CastMember(string name, string? role = null, string? profileUrl = null)
    {
        Name = name;
        Role = role;
        ProfileUrl = profileUrl;
    }

    public string Name { get; set; } = string.Empty;

    public string? Role { get; set; }

    public string? ProfileUrl { get; set; }
}
