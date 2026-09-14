# StreamDesk

StreamDesk is a lightweight, keyboard-friendly **Windows desktop media client** with a modern Netflix/Jellyfin-style browsing experience. It plays **only media you are authorized to access**: your local files, metadata from services you configure (TMDB), channels from **your own M3U/M3U8 playlists**, and — where an operator grants you API access — authorized provider services.

> StreamDesk ships with **no bundled streams and no scraping**. Unconfigured providers (MovieBox, 4KHDHub) are honest adapter slots: they show **Not configured** in the UI and refuse to fake success until a legitimate, authorized API implementation is supplied.

## Features

- **Home** — Continue Watching, Trending, Popular Movies/Shows/Anime, Recently Added, Favorites
- **Browse** — Movies, TV Shows, Anime grids with poster cards, lazy image loading and caching
- **Global search** — `Ctrl+K`, debounced, across movies/shows/anime/episodes, keyboard navigable
- **Details** — Backdrop, poster, synopsis, genres, ratings, runtime, cast; season selector and episode list for shows
- **Playback** — External players: **VLC** and **mpv**, auto-detected or manually configured
- **Subtitles** — External SRT/VTT/ASS sidecar files, preferred language, auto-select
- **Live TV** — Import your own `.m3u`/`.m3u8` files or URLs; groups, search, favorites, refresh
- **Downloads** — Queue with pause/resume/cancel/retry, HTTP range resume, restart recovery (only where the provider permits)
- **Favorites & history** — Movies, shows, episodes and channels persist locally; continue-watching with resume/restart
- **Themes** — Dark, Light and System, persisted
- **Keyboard-first** — Full shortcut set (see below), no mouse required

## Requirements

- Windows 10 1809+ / Windows 11 (x64)
- One external player for playback: [VLC](https://www.videolan.org/vlc/) or [mpv](https://mpv.io/) (auto-detected from default install locations)
- Optional: a free [TMDB API key](https://www.themoviedb.org/settings/api) for rich metadata and posters

## Installation

1. Download `StreamDesk-Windows-x64-vX.Y.Z.zip` from the [Releases](../../releases) page
2. Extract anywhere
3. Run `StreamDesk.exe` — no installer, no runtime download (self-contained)

## Running from source

```bash
git clone https://github.com/streamdesk/streamdesk.git
cd streamdesk
dotnet run --project src/StreamDesk.App
```

Requires the .NET 9 SDK.

## Building

```bash
dotnet build StreamDesk.sln -c Release
dotnet test  StreamDesk.sln -c Release

# Self-contained Windows x64 executable
dotnet publish src/StreamDesk.App -c Release -r win-x64 --self-contained true -o publish
```

Output: `publish/StreamDesk.exe`.

## Providers

StreamDesk has a strict provider architecture — the UI never talks to a specific service:

| Provider | Purpose | Status |
|---|---|---|
| **TMDB** | Metadata, posters, backdrops, seasons/episodes | Enabled once an API key is configured |
| **Local Library** | Your own folders (movies/shows/anime) | Enabled; add folders in Settings |
| **M3U Playlists** | Live TV from playlists **you** provide | Enabled; import in Live TV |
| **MovieBox** | Streaming slot (adapter) | **Not configured** — needs authorized API |
| **4KHDHub** | Streaming slot (adapter) | **Not configured** — needs authorized API |

### Configuring providers

`Settings → Media Providers` shows each provider's status and capabilities, with Configure / Enable / Disable / Test Connection actions.

**TMDB:** paste your API key in Settings → Media Providers → TMDB → Configure. The key is stored protected at rest (DPAPI) and never committed or logged.

**Local Library:** Settings → Local Library → add folder. Scanned formats: `mkv mp4 avi mov wmv m4v ts webm flv mpg mpeg`.

**M3U:** Live TV → Add playlist → choose a `.m3u`/`.m3u8` file or paste a URL. Refresh any time.

**MovieBox / 4KHDHub:** these adapters are integration points, not scrapers. An authorized implementation plugs in via `CreateApiClient` in `src/StreamDesk.Providers/MovieBox/` or `/FourKHDHub/` — see [docs/providers.md](docs/providers.md). Until then they appear as *Not configured* and every catalog call throws `ProviderNotConfiguredException` rather than pretending to work. StreamDesk does not implement scraping, reverse-engineered endpoints, DRM circumvention, authentication bypass, or extraction of unauthorized copyrighted streams.

## VLC / mpv setup

Settings → Playback:

- **Preferred player** — Auto detect, VLC, or mpv
- **Executable paths** — e.g. `C:\Program Files\VideoLAN\VLC\vlc.exe` or `C:\Program Files\mpv\mpv.exe`

Auto-detect checks the standard install locations and `PATH`. Subtitles open in the player: mpv receives `--sub-file`, VLC receives `:sub-file`. The app is given a legitimate local file or authorized URL by the provider — no DRM bypass of any kind.

## Subtitle support

Place sidecar files next to your media: `Movie.en.srt`, `Show S01E02.hi.vtt`, `Episode.ass`. Settings → Subtitles sets the preferred language (English, Hindi, Auto) and auto-selection. Provider subtitles are used where an authorized provider legally supplies them.

## Downloads

Settings → Downloads chooses the directory and concurrent-download count. The queue supports pause, resume (HTTP range where the source permits), cancel, retry, delete, and automatic recovery after restart (interrupted downloads become paused). Progress shows speed, size, percentage and ETA. Downloads only occur for sources an enabled, configured provider resolves.

## Keyboard shortcuts

| Keys | Action |
|---|---|
| `Ctrl+K` / `Ctrl+F` | Global search |
| `Ctrl+L` | Live TV |
| `Ctrl+D` | Downloads |
| `Ctrl+,` | Settings |
| `F1` | Help |
| `Esc` | Back / close dialog |
| `Enter` | Open selected item |
| `Space` | Play/Pause where applicable |

## Development

```
StreamDesk/
├── src/
│   ├── StreamDesk.App/            Avalonia UI: views, view models, themes
│   ├── StreamDesk.Core/           Models, provider abstractions, exceptions
│   ├── StreamDesk.Application/    Services, ProviderManager, DownloadManager
│   ├── StreamDesk.Infrastructure/ SQLite storage, HTTP, image cache, secrets
│   └── StreamDesk.Providers/      TMDB, Local, M3U, MovieBox, FourKHDHub
├── tests/
│   ├── Core.Tests/                Core model contracts
│   ├── Application.Tests/         Services, managers, download pipeline
│   └── Providers.Tests/           Parsing, mapping, unconfigured-provider honesty
├── docs/                          Architecture and provider guides
└── .github/workflows/             build.yml, release.yml
```

Stack: **C# / .NET 9 / Avalonia 11**, SQLite (Microsoft.Data.Sqlite) for storage, xUnit for tests. The authoritative version lives in `Directory.Build.props` and `src/StreamDesk.Core/AppVersion.cs`.

## GitHub Actions

- **build.yml** — on every push/PR: restore → build → test → publish self-contained win-x64 → upload artifact
- **release.yml** — on `v*` tags: version-stamped publish → zip → GitHub Release with `StreamDesk-Windows-x64-<tag>.zip`

## Release process

```bash
git add .
git commit -m "release: prepare v1.0.0"
git tag v1.0.0
git push origin main
git push origin v1.0.0     # triggers the release workflow
```

## Known limitations

- MovieBox and 4KHDHub are **intentionally non-functional** until an authorized API implementation is supplied (by design, not a bug)
- TMDB supplies metadata only — it never provides video streams
- Downloads work only where the resolved source is a direct HTTP(S) file
- Playback relies on VLC/mpv being installed; StreamDesk embeds no player
- Windows-only packaging today (the Avalonia UI itself is cross-platform)
