# AutoFilm module map

Updated: 2026-07-28

The upstream Jellyfin structure remains intact. AutoFilm code is separated by
responsibility:

- `MediaBrowser.Controller/AutoFilm/`
  - Provider-neutral remote path, migration, refresh, event, subtitle, and
    probe interfaces.
  - `AutoFilmRemotePath.cs` validates and converts `openlist:///` paths.
  - `IAutoFilmRemoteLibraryRoots.cs` defines configured remote-root lookup.
  - `AutoFilmSubtitleCompatibility.cs` normalizes external SUP responses.
- `MediaBrowser.Model/Configuration/MediaPathSourceType.cs`
  - Distinguishes normal host paths from OpenList media library sources.
- `Emby.Server.Implementations/AutoFilm/AutoFilmOptions.cs`
  - Environment configuration, migration mapping, and legacy subtitle fallback.
- `Emby.Server.Implementations/AutoFilm/AutoFilmMigrationService.cs`
  - Bounded local migration for items, physical folders, base videos, external
    subtitle streams, media library `PathInfos`, and `.mblink` targets.
  - Does not call OpenList or execute media probes.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteLibraryRoots.cs`
  - Reads OpenList roots from Jellyfin's normal virtual-folder configuration.
- `Emby.Server.Implementations/AutoFilm/AutoFilmOpenListClient.cs`
  - Token-authenticated path lookup, listing, upload, delete, public download
    URI, and container-internal download URI.
- `Emby.Server.Implementations/AutoFilm/AutoFilmDirectorySnapshot.cs`
  - In-memory `IDirectoryService` snapshot used by normal Jellyfin resolvers.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteRefreshService.cs`
  - Creates or refreshes a bounded remote hierarchy through normal resolvers
    and metadata providers.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteProbeQueue.cs`
  - Single-concurrency, minimum-interval ffprobe queue for new videos.
- `Emby.Server.Implementations/AutoFilm/AutoFilmPathEventService.cs`
  - Applies active upsert, move, and remove events by path.
- `Emby.Server.Implementations/AutoFilm/AutoFilmSubtitleService.cs`
  - Remote-first resolution, local fallback, lazy migration, new remote upload,
    stale stream removal, and remote deletion.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteMediaSourceProvider.cs`
  - Dynamic HTTP direct-play source using existing Jellyfin media streams.
- `Jellyfin.Api/Controllers/AutoFilmController.cs`
  - Migration preview/apply, OpenList directory browsing, and path-only remote
    refresh.
- `Jellyfin.Api/Controllers/AutoFilmEventsController.cs`
  - Token-protected OpenList event receiver.
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

Detailed configuration and behavior are in
`docs/autofilm-remote-media.md`.
