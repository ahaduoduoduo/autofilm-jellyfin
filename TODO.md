# AutoFilm development status

Updated: 2026-08-10

## Completed

- [x] 2026-08-06: Add explicit additive and full OpenList scan modes. Full
  rescans require a fresh bounded snapshot, remove database-only descendants,
  correct item types through normal Jellyfin resolvers, and never delete
  OpenList files.
- [x] 2026-08-06: Resolve newly selected OpenList video files and movie
  directories with their inherited Jellyfin collection type instead of
  defaulting to a generic Video or Folder.

- [x] 2026-08-05: Enforce direct-play-only playback for `autofilm:` OpenList
  media after device-profile calculation and omit generated transcoding URLs,
  without changing local-media transcoding behavior.

- [x] 2026-07-31: Make Jellyfin's standard administrator metadata refresh
  enqueue an OpenList Movie/Episode for remote probing when its embedded video
  stream record is missing; healthy remote items remain probe-free.
- [x] 2026-07-31: Retry an exact replacement ffprobe after temporary
  `FfmpegException` failures, with three total attempts and bounded delays;
  cancellation and other failures remain immediate.
- [x] 2026-07-31: Add bounded, read-only replacement discovery using
  Jellyfin's configured `VideoResolver` and `EpisodeResolver`, without creating
  library records.
- [x] 2026-07-31: Add preview/apply/rollback media replacement for an existing
  OpenList Movie or Episode. Preview uses the normal media encoder; apply keeps
  the same Item ID, metadata, images, provider IDs and user data while
  replacing internal media streams and retaining external subtitles.
- [x] 2026-07-31: Serialize apply per Item ID, revalidate path, size and
  modification time, require the replacement in the same media directory, and
  restore the previous database snapshot if persistence fails.
- [x] 2026-07-31: Expose Jellyfin Web deletion for physical OpenList season
  directories and complete OpenList series while keeping virtual seasons and
  media-library roots protected; AutoFilm Core remains limited to Movie and
  Episode deletion.
- [x] 2026-07-30: Expose Jellyfin Web media deletion for valid
  `openlist:///` movies and episodes while retaining normal user delete
  permissions and excluding arbitrary remote URLs and library folders.
- [x] 2026-07-30: Run the additive descendant importer for newly created remote
  folders so first-time episode, season, and multi-season downloads create
  their contained videos during the same explicit refresh.
- [x] 2026-07-30: Add an authenticated binary subtitle endpoint that streams
  every subtitle format through Jellyfin to OpenList without Base64 expansion
  or a fixed request-size limit, while retaining the upstream JSON API for
  client compatibility.
- [x] 2026-07-30: Recreate missing episode and video descendants during an
  explicit recursive remote refresh without deleting records absent from the
  current OpenList snapshot.
- [x] 2026-07-30: Parse season and episode numbers from `openlist:///` video
  paths and refresh every discovered remote video after recursive recovery.
- [x] 2026-07-30: Forward precise-refresh intent to OpenList object lookup so a
  newly completed offline-download result is visible without an administrator
  refreshing its parent directory manually.
- [x] 2026-07-30: Bind movie provider IDs to the single video inside an exact
  OpenList result directory instead of applying them to the wrapper folder.
- [x] 2026-07-28: Store item and external subtitle paths as `openlist:///`.
- [x] 2026-07-28: Return OpenList 302 responses for video and remote subtitles.
- [x] 2026-07-30: Preserve percent encoding in video and remote subtitle 302
  locations so paths containing non-ASCII names remain valid HTTP headers.
- [x] 2026-07-28: Expose direct-play-only HTTP media sources with transcoding
  disabled.
- [x] 2026-07-28: Use Jellyfin resolvers and metadata providers for new remote
  paths.
- [x] 2026-07-28: Add a serialized, rate-limited probe queue for new videos.
- [x] 2026-07-28: Add OpenList subtitle reads and stale stream removal.
- [x] 2026-07-28: Store new subtitle uploads directly in OpenList.
- [x] 2026-07-28: Delete OpenList paths before removing Jellyfin items.
- [x] 2026-07-29: Remove automatic OpenList path-event handling. Remote media
  enters Jellyfin only through an explicit `RemoteRefresh`.
- [x] 2026-07-28: Build and run the self-contained DSM test image.
- [x] 2026-07-28: Store remote media library roots as `openlist:///` in
  `PathInfos` and `.mblink` files while preserving local libraries.
- [x] 2026-07-28: Add Local/OpenList library source typing and an authenticated
  OpenList directory browser endpoint.
- [x] 2026-07-28: Restrict remote refresh to configured remote library roots.
- [x] 2026-07-28: Normalize external SUP streams in PlaybackInfo and serve
  them through Jellyfin's own subtitle endpoint.
- [x] 2026-07-28: Return real local-library external SUP files unchanged with
  HTTP range support instead of invoking Jellyfin's subtitle encoder.
- [x] 2026-07-28: Build the forked jellyfin-web source and package it with the
  modified server in one runtime image.
- [x] 2026-07-28: Remove installation-specific database migration and legacy
  subtitle reverse lookup from the maintained public branch.
- [x] 2026-07-28: Keep the default branch free of legacy path configuration,
  migration APIs, and local fallback upload behavior.
- [x] 2026-07-30: Use Jellyfin's standard subtitle upload and delete endpoints
  as the single interface for both local and OpenList media.
- [x] 2026-07-30: Give OpenList subtitle uploads the same numbered-name
  collision behavior as Jellyfin local subtitle uploads.
- [x] 2026-07-30: Add a focused remote subtitle upload test covering numbered
  names and immediate media-stream insertion.
- [x] 2026-08-10: Resolve nested OpenList television refreshes to their owning
  Series, move previously misplaced matching provider IDs off a Season, and
  persist parsed episode season numbers before the Series metadata refresh.
- [x] 2026-08-10: Force provider-backed remote refreshes to revisit existing
  Season and Episode metadata after the Series identity is saved.
- [x] 2026-08-10: Replace scanner-derived Episode filenames with provider
  titles after exact remote television imports.

## Planned

- [ ] Add focused unit tests for root path mapping, subtitle state transitions,
  and delete failure handling.
- [x] 2026-07-28: Add a dedicated Local/OpenList source picker and authenticated
  OpenList directory browser to `autofilm-jellyfin-web`.
- [ ] Verify Infuse seeking, playback reporting, deletion, and SUP/PGS behavior
  on a physical client; server-side PlaybackInfo and subtitle 302 are verified.
