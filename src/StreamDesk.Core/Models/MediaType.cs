namespace StreamDesk.Core;

/// <summary>Kind of media represented by a catalog item.</summary>
public enum MediaType
{
    /// <summary>A standalone movie.</summary>
    Movie,

    /// <summary>A TV show or anime series.</summary>
    Show,

    /// <summary>A single episode of a show.</summary>
    Episode,

    /// <summary>A live television channel.</summary>
    Channel
}
