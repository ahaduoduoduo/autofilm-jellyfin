# AutoFilm module map

Updated: 2026-07-30

The upstream Jellyfin structure remains intact. AutoFilm code is separated by
responsibility:

- `MediaBrowser.Controller/AutoFilm/`
  - Provider-neutral remote path, refresh, subtitle, and probe
    interfaces.
  - `AutoFilmRemotePath.cs` validates and converts `openlist:///` paths.
  - `IAutoFilmRemoteLibraryRoots.cs` defines configured remote-root lookup.
  - `AutoFilmSubtitleCompatibility.cs` normalizes external SUP responses.
- `MediaBrowser.Model/Configuration/MediaPathSourceType.cs`
  - Distinguishes normal host paths from OpenList media library sources.
- `Emby.Server.Implementations/AutoFilm/AutoFilmOptions.cs`
  - Environment configuration for OpenList access, refresh limits, and media
    probe intervals.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteLibraryRoots.cs`
  - Reads OpenList roots from Jellyfin's normal virtual-folder configuration.
- `Emby.Server.Implementations/AutoFilm/AutoFilmOpenListClient.cs`
  - Token-authenticated path lookup, listing, upload, delete, public download
    URI, and container-internal download URI.
  - For precise post-download refreshes, forwards the target-object refresh
    flag so OpenList reloads the exact parent directory before lookup.
- `Emby.Server.Implementations/AutoFilm/AutoFilmDirectorySnapshot.cs`
  - In-memory `IDirectoryService` snapshot used by normal Jellyfin resolvers.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteRefreshService.cs`
  - Creates or refreshes a bounded remote hierarchy through normal resolvers
    and metadata providers.
  - Recursively creates descendants missing from an existing Jellyfin folder
    without treating a partial or unavailable remote snapshot as deletion.
  - When `provider_target` is `movie`, applies provider IDs to the only direct
    video in a result directory; series refreshes retain folder-level behavior.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteProbeQueue.cs`
  - Single-concurrency, minimum-interval ffprobe queue for new videos.
- `Emby.Server.Implementations/AutoFilm/AutoFilmSubtitleService.cs`
  - OpenList resolution, numbered new remote uploads, immediate stream
    insertion, stale stream removal, remote deletion, and raw local SUP
    delivery.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteMediaSourceProvider.cs`
  - Dynamic HTTP direct-play source using existing Jellyfin media streams.
- `Jellyfin.Api/Controllers/AutoFilmController.cs`
  - OpenList directory browsing and path-only remote refresh.
- `Jellyfin.Api/Helpers/AutoFilmRedirectHelper.cs`
  - Produces ASCII-safe `Location` values for video and remote subtitle 302
    responses, including paths with non-ASCII names.
- `Jellyfin.Api/Controllers/VideosController.cs`
  - `openlist:///` video redirects.
- `Jellyfin.Api/Controllers/SubtitleController.cs`
  - Remote subtitle reads, local SUP raw-file responses with HTTP range support,
    upload, and delete integration.
- `Jellyfin.Api/Controllers/LibraryController.cs`
  - OpenList-first item deletion.
- `Emby.Server.Implementations/Library/MediaSourceManager.cs`
  - Prevents local filesystem probing and normalizes external SUP streams.
- `Emby.Server.Implementations/Library/LibraryManager.cs`
  - Accepts `Local` and `OpenList` media sources and persists OpenList roots as
    normal library locations.
- `Emby.Server.Implementations/IO/ManagedFileSystem.cs`
  - Provides synthetic directory metadata for configured OpenList roots.
- `Emby.Server.Implementations/IO/LibraryMonitor.cs`
  - Excludes OpenList roots from operating-system filesystem watchers.
- `Dockerfile.autofilm`
  - Builds the modified Jellyfin server and the separate AutoFilm jellyfin-web
    source context, then packages both with the base image's FFmpeg runtime.

Installation-specific database migration and legacy subtitle reverse lookup are
kept only in the `codex/personal-legacy-compat` branch. They are intentionally
absent from the default branch and its public module surface.

Detailed configuration and behavior are in
`docs/autofilm-remote-media.md`.
