# Architecture

StreamDesk follows a strict one-way dependency flow:

```
StreamDesk.App (Avalonia UI)
    ↓
StreamDesk.Application   (services, ProviderManager, DownloadManager)
    ↓                       ↓
StreamDesk.Providers     StreamDesk.Infrastructure (SQLite, HTTP, secrets, image cache)
    └───────────┬──────────┘
                ↓
          StreamDesk.Core (models, abstractions, exceptions)
```

## Layers

**StreamDesk.Core** — `MediaSummary`, `MediaDetails`, `EpisodeInfo`, `SeasonInfo`, `ChannelInfo`, `PlaybackSource`, `SubtitleTrack`, `DownloadTask`, `AppSettings`; the provider interfaces and `ProviderCapabilities`; `ProviderNotConfiguredException` / `ProviderException`. No dependencies.

**StreamDesk.Application** — app services (`SettingsService`, `FavoritesService`, `WatchHistoryService`, `PlaybackService`, `SubtitleService`, `LibraryService`), `ProviderManager` (registration, capability queries, merged search, playback/subtitle/download routing, failure isolation) and `DownloadManager` (persistent queue, HTTP range resume, restart recovery). Depends only on Core + Infrastructure.

**StreamDesk.Providers** — TMDB (metadata over the official API), Local (folder scan → movies/series/episodes), M3U (parser + playlist provider for Live TV), and the MovieBox / 4KHDHub authorized-API adapter slots on `AuthorizedApiProviderBase`.

**StreamDesk.Infrastructure** — SQLite `Database` with forward-only migrations, repositories (settings, favorites, history, playlists/channels, library folders, downloads, media library), `HttpClientFactory`, `ImageCache` (disk-backed poster/thumbnail cache) and `SecretProtector` (DPAPI).

**StreamDesk.App** — Avalonia 11 desktop UI: sidebar shell with responsive icon-collapse, theme system (dark/light/system via theme dictionaries; screens bind only to theme resources, never hard-coded colors), ViewLocator-based page navigation, debounced global search, details with season selector, Live TV, download queue, settings, help dialog, global keyboard shortcuts.

## Key flows

**Search** — `Ctrl+K` → `MainViewModel` debounces input → `ProviderManager.SearchAsync` fans out to enabled providers, merges and logs-without-failing on per-provider errors → grid renders `MediaCardViewModel`s.

**Playback** — user presses Play → VM resolves the item via `ProviderManager.ResolvePlaybackAsync(itemId, providerId)` → `PlaybackService` validates the source (exists / authorized URL) → launches VLC or mpv with subtitle args. Providers hand over legitimate local files or authorized URLs only.

**Downloads** — provider resolves `(PlaybackSource, suggestedFileName)` → `DownloadManager.Enqueue` persists the task → worker pool (configurable concurrency) streams to disk with 2-second progress persistence; pause/stop across restarts resumes via HTTP `Range` when the server supports it.

**Persistence** — single SQLite file in per-user app data with versioned migrations; settings are JSON in the `settings` table with DPAPI-protected secrets.

## Testing strategy

Each layer has its own test project mirroring the dependency rules:

- `Core.Tests` — model/contract behavior, no infrastructure.
- `Application.Tests` — services and managers against the real SQLite layer and a local in-process HTTP server (download lifecycle, range resume, payload integrity).
- `Providers.Tests` — M3U parsing/grouping, TMDB DTO mapping, and the **honesty contract** for the unconfigured MovieBox/4KHDHub stubs (must throw `ProviderNotConfiguredException`, never fake success).
