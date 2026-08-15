# AutoFilm module map

Updated: 2026-08-16

The upstream Jellyfin structure remains intact. AutoFilm code is separated by
responsibility:

- `MediaBrowser.Controller/AutoFilm/`
  - Provider-neutral remote path, refresh, subtitle, and probe
    interfaces.
  - `AutoFilmRemotePath.cs` validates and converts `openlist:///` paths.
  - `AutoFilmRemoteScanMode.cs` validates the additive `new` and database
    reconciliation `full` modes.
  - `AutoFilmRemoteProviderTarget.cs` validates optional `movie` and `series`
    targets supplied by trusted importers.
  - `IAutoFilmRemoteLibraryRoots.cs` defines configured remote-root lookup.
  - `AutoFilmSubtitleCompatibility.cs` normalizes external SUP responses.
  - `IAutoFilmMediaReplacementService.cs` and
    `AutoFilmMediaReplacementModels.cs` define bounded discovery, immutable
    preview, apply and rollback contracts for an existing Video Item ID.
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
  - Streams subtitle request bodies to OpenList and preserves a known content
    length without buffering the complete file in Jellyfin.
  - For precise post-download refreshes, forwards the target-object refresh
    flag so OpenList reloads the exact parent directory before lookup.
- `Emby.Server.Implementations/AutoFilm/AutoFilmDirectorySnapshot.cs`
  - In-memory `IDirectoryService` snapshot used by normal Jellyfin resolvers.
  - Records which directories returned a complete listing so destructive
    database reconciliation never relies on a partial object set.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteRefreshService.cs`
  - Creates or refreshes a bounded remote hierarchy through normal resolvers
    and metadata providers.
  - Validates an explicit importer media type against the virtual folder and
    resolved Movie/Series before saving provider IDs or requesting metadata;
    requests without a type retain normal library inference.
  - Uses the same additive descendant importer for new and existing Jellyfin
    folders, including episode, season, and multi-season result directories,
    without treating a partial or unavailable remote snapshot as deletion.
  - Removes an exact missing target during a full scan only after its refreshed
    OpenList parent returns a non-empty result.
  - Lists an exact video's containing directory once so sidecar discovery uses
    the same bounded OpenList response as item resolution.
  - Refreshes metadata for every discovered video and probes newly created
    videos through the serialized remote probe queue.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteMovieResolver.cs`
  - Selects one unambiguous primary Movie from an explicitly scanned OpenList
    release directory without changing Jellyfin's normal local resolver.
  - Accepts a single direct video despite a `Sample` subdirectory, and uses a
    unique release-name match when promotional videos are present.
  - Reads the media type from Jellyfin's virtual-folder configuration when a
    persisted OpenList directory does not inherit a collection type.
  - Ignores a provisional Series parent when an explicit movie scan receives a
    wrapper directory whose name ends in a video extension.
  - Preserves an existing identified Movie when a full scan resolves its
    direct video under a persisted generic wrapper.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteReconciler.cs`
  - Runs only for an explicit full rescan after a fresh bounded snapshot was
    loaded successfully.
  - Removes stale Jellyfin database descendants with `DeleteFileLocation=false`
    and recreates incorrectly typed folders or videos through Jellyfin's normal
    resolvers.
  - Reuses provider IDs and core metadata, reroutes collection references, and
    reattaches user data after an item type change.
  - Uses the remote movie resolver during full scans so a previous
    `Folder + Video` result can become a Movie without changing OpenList files.
  - Reparents replacements to the persisted containing folder before insertion
    so a removed wrapper cannot remain as a database foreign-key target.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteProbeQueue.cs`
  - Single-concurrency, minimum-interval ffprobe queue for new videos and
    historical remote videos whose runtime or embedded video stream is absent.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteMediaHealthMonitor.cs`
  - Checks completed OpenList metadata refreshes using Jellyfin's local stream
    database and queues an incomplete video for the existing rate-limited
    probe service without polling OpenList or scanning the full library.
- `Jellyfin.Api/Controllers/ItemRefreshController.cs`
  - Keeps the upstream metadata refresh behavior and additionally queues an
    OpenList Movie or Episode for a non-forced probe. The queue skips items
    that already have an embedded video stream; historical streamless items
    receive width, height, runtime, bitrate, size, container and track data.
- `Emby.Server.Implementations/AutoFilm/AutoFilmMediaReplacementService.cs`
  - Uses the configured Jellyfin naming rules to inspect completed OpenList
    results and the normal media encoder to probe an exact replacement.
  - Limits replacement probes to two concurrent operations and retries only
    `FfmpegException` failures, for at most three total attempts with bounded
    delays, to tolerate a temporary remote read reset without unbounded 115
    requests.
  - Applies under a per-item lock after revalidating both paths and file facts;
    updates the existing Video and internal streams, keeps current external
    subtitle streams, and restores the previous snapshot on write failure.
  - Keeps preview and rollback tokens only in process memory; Core can perform
    a new reverse preview after restoring a backup when a server restart has
    invalidated a token.
- `Emby.Server.Implementations/AutoFilm/AutoFilmSubtitleService.cs`
  - OpenList resolution, numbered new remote uploads, immediate stream
    insertion, stale stream removal, remote deletion, and raw local SUP
    delivery.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteSubtitleScanner.cs`
  - Discovers matching sidecar subtitles from successfully enumerated OpenList
    directories without downloading subtitle contents.
  - Additive scans insert new records only; full scans can also remove stale
    remote records from the same enumerated directory.
- `Emby.Server.Implementations/AutoFilm/AutoFilmExternalSubtitleStream.cs`
  - Creates one common external-stream representation for uploaded and scanned
    OpenList subtitles.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteMediaSourceProvider.cs`
  - Dynamic HTTP direct-play source using existing Jellyfin media streams; a
    PlaybackInfo request also schedules repair when tracks or runtime are
    absent.
- `Emby.Server.Implementations/Library/LibraryManager.cs`
  - Lets Jellyfin's standard episode-name parser consume the underlying
    OpenList path when filling missing season and episode numbers.
- `Jellyfin.Api/Controllers/AutoFilmController.cs`
  - OpenList directory browsing and path-only remote refresh, plus media
    replacement inspect/preview/apply/rollback endpoints.
- `Jellyfin.Api/Helpers/AutoFilmRedirectHelper.cs`
  - Produces ASCII-safe `Location` values for video and remote subtitle 302
    responses, including paths with non-ASCII names.
- `Jellyfin.Api/Helpers/MediaInfoHelper.cs`
  - Applies the normal Jellyfin device-profile calculation, then enforces the
    final direct-play-only invariant for `openlist:///` media sources and
    removes any generated transcoding URL. AutoFilm playback sources retain the
    stable Jellyfin item media-source ID required by third-party clients. Local
    media keeps upstream playback capability behavior.
  - Publishes a Jellyfin delivery URL for every external `openlist:///`
    subtitle after device-profile handling, so ASS, SRT, SUP and other original
    formats remain available to third-party clients without enabling remote
    transcoding.
- `Jellyfin.Api/Controllers/VideosController.cs`
  - `openlist:///` video redirects.
- `Jellyfin.Api/Controllers/SubtitleController.cs`
  - Remote subtitle reads, local SUP raw-file responses with HTTP range support,
    upload, and delete integration.
  - Keeps the upstream Base64 JSON upload route for client compatibility and
    adds an authenticated unbuffered binary route for all AutoFilm subtitle
    formats; both use the same save operation.
- `Jellyfin.Api/Controllers/LibraryController.cs`
  - OpenList-first item deletion.
- `MediaBrowser.Controller/Entities/Video.cs`
  - Reports valid `openlist:///` movies and episodes as deletable so Jellyfin
    Web can expose its standard delete action while preserving user policy
    checks.
- `MediaBrowser.Controller/Entities/TV/Series.cs` and `Season.cs`
  - Report physical OpenList series and season directories as deletable.
  - Virtual seasons remain protected because they do not have their own path;
    media-library roots retain their upstream non-deletable behavior.
- `Emby.Server.Implementations/Library/MediaSourceManager.cs`
  - Prevents local filesystem probing and normalizes external SUP streams.
- `Emby.Server.Implementations/Library/UserDataManager.cs`
  - Preserves a client-reported resume position for an OpenList video when a
    temporary remote probe failure has left its runtime unknown; local media
    retains the upstream unknown-runtime behavior.
- `Emby.Server.Implementations/Library/LibraryManager.cs`
  - Accepts `Local` and `OpenList` media sources and persists OpenList roots as
    normal library locations.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteRefreshService.cs`
  - Loads a bounded OpenList snapshot, imports missing remote items, prepares
    episode numbers, and sends metadata/probe work to Jellyfin.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteProviderTargetResolver.cs`
  - Finds the Series that owns a nested release, prepares episode numbering,
    and prevents series provider identifiers from remaining on an intermediate
    Season.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteMovieResolver.cs`
  - Resolves an unambiguous primary Movie only during bounded OpenList scans;
    normal Jellyfin local and scheduled scanning remain unchanged.
- `Emby.Server.Implementations/AutoFilm/AutoFilmRemoteReconciler.cs`
  - Removes stale database-only remote items and replaces incorrectly typed
    items during an explicit full scan without deleting OpenList files.
- `Emby.Server.Implementations/IO/ManagedFileSystem.cs`
  - Provides synthetic directory metadata for configured OpenList roots.
- `Emby.Server.Implementations/IO/LibraryMonitor.cs`
  - Excludes OpenList roots from operating-system filesystem watchers.
- `Dockerfile.autofilm`
  - Builds the modified Jellyfin server and the separate AutoFilm jellyfin-web
    source context, then packages both with the base image's FFmpeg runtime.

Installation-specific database migration and legacy subtitle reverse lookup are
absent from the maintained public branch and its module surface.

Detailed configuration and behavior are in
`docs/autofilm-remote-media.md`. Sidecar synchronization behavior is in
`docs/autofilm-remote-subtitles.md`. Nested multi-season package behavior is in
`docs/autofilm-remote-series.md`.
