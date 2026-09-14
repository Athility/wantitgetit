# Providers

How StreamDesk's provider system works, and how to plug in a legitimate implementation.

## The contract

The UI only knows `StreamDesk.Core` abstractions. A provider declares what it can do:

```csharp
Descriptor = new ProviderDescriptor(id, displayName, description)
{
    Capabilities = ProviderCapabilities.Movies | ProviderCapabilities.Search | ...,
    IsConfigured = ...,
    IsEnabled    = ...
};
```

Capabilities: `Movies, Shows, Anime, Episodes, Search, Metadata, Playback, Subtitles, Downloads, LiveTV, HomeSections`.

Interfaces: `IMediaProvider` (catalog/details/seasons), `ISearchProvider`, `IPlaybackProvider`, `ISubtitleProvider`, `IDownloadProvider`, `ISelfTestProvider`.

`ProviderManager` registers providers, initializes them, merges search across enabled+configured providers, routes playback/subtitle/download resolution to the owning provider by id, and isolates failures so one broken provider never breaks the app.

## Built-in providers

| Provider | Id | Capabilities | Configured when |
|---|---|---|---|
| TMDB | `tmdb` | metadata, search, home sections | API key set (Settings → Media Providers) |
| Local Library | `local` | movies/shows/anime, playback | at least one folder added |
| M3U | `m3u` | LiveTV, playback | at least one playlist imported |
| MovieBox | `moviebox` | movies, shows, episodes, playback, subtitles, downloads | **never by default** |
| 4KHDHub | `4khdhub` | same | **never by default** |

## Authorized-API adapter slots (MovieBox / 4KHDHub)

Both adapters share `AuthorizedApiProviderBase` (`src/StreamDesk.Providers/Shared/ConfiguredApiProviderBase.cs`):

- Configuration: base URL + API key, issued by the service operator. Stored protected at rest.
- **Unconfigured:** every catalog call throws `ProviderNotConfiguredException`; Test Connection reports "not configured". The UI shows the provider as *Not configured*.
- **Configured but not implemented:** the adapter is "configured" (valid URL + key), but since no authorized client exists, calls throw a `ProviderException` naming the exact extension point, and Test Connection honestly reports that an implementation is required. **No fake success, ever.**

### Where to implement an authorized API

1. **Client** — override `CreateApiClient(baseUrl, apiKey)` in `MovieBoxProvider` / `FourKHDHubProvider` and return a client that calls the operator's documented API.
2. **Mapping** — map the operator's DTOs to `MediaSummary` / `MediaDetails` / `SeasonInfo` / `PlaybackSource` / `SubtitleTrack` (mirror `TmdbMapper` in `src/StreamDesk.Providers/Tmdb/`).
3. **Threading the calls** — replace the `NotImplementedException` bodies in `AuthorizedApiProviderBase` (or override the methods per provider) with real calls via the client.
4. **Tests** — the stub contract stays: unconfigured ⇒ `ProviderNotConfiguredException`; configured+implemented ⇒ real results. Add mapping tests for the real DTOs.

No scraping, reverse-engineered endpoints, DRM circumvention, authentication bypass, hidden-API discovery, or extraction of unauthorized copyrighted streams is supported anywhere in StreamDesk. The adapter pattern exists precisely so an *authorized* integration can be added without UI changes.

## Configuration storage

- Settings, favorites, watch history, playlists, library folders and download state live in SQLite under the per-user app-data folder (`StreamDesk/database.db`).
- Provider API keys are additionally protected at rest with DPAPI (`SecretProtector`) and are never written to logs.
- Nothing secret is committed: `.gitignore` excludes local databases, storage folders, environment files and build output.
