# AutoFilm development status

Updated: 2026-07-29

## Completed

- [x] 2026-07-28: Store item and external subtitle paths as `openlist:///`.
- [x] 2026-07-28: Migrate the existing Jellyfin database without OpenList
  queries, media probes, or a shadow catalog.
- [x] 2026-07-28: Preserve item IDs, provider IDs, metadata, user data, and
  existing media streams.
- [x] 2026-07-28: Return OpenList 302 responses for video and remote subtitles.
- [x] 2026-07-28: Expose direct-play-only HTTP media sources with transcoding
  disabled.
- [x] 2026-07-28: Use Jellyfin resolvers and metadata providers for new remote
  paths.
- [x] 2026-07-28: Add a serialized, rate-limited probe queue for new videos.
- [x] 2026-07-28: Add remote-first subtitle reads, read-only local fallback,
  serialized lazy upload, and stale stream removal.
- [x] 2026-07-28: Store new subtitle uploads directly in OpenList.
- [x] 2026-07-28: Delete OpenList paths before removing Jellyfin items.
- [x] 2026-07-29: Remove automatic OpenList path-event handling. Remote media
  enters Jellyfin only through an explicit `RemoteRefresh`.
- [x] 2026-07-28: Build and run the self-contained DSM test image.
- [x] 2026-07-28: Validate a full copied database migration, playback,
  subtitles, move, and delete behavior.
- [x] 2026-07-28: Store remote media library roots as `openlist:///` in
  `PathInfos` and `.mblink` files while preserving local libraries.
- [x] 2026-07-28: Migrate physical Folder and base Video paths without
  OpenList requests; verify no legacy media paths remain in the test database.
- [x] 2026-07-28: Add Local/OpenList library source typing and an authenticated
  OpenList directory browser endpoint.
- [x] 2026-07-28: Restrict remote refresh to configured remote library roots.
- [x] 2026-07-28: Normalize external SUP streams in PlaybackInfo and serve
  them through Jellyfin's own subtitle endpoint.
- [x] 2026-07-28: Return real local-library external SUP files unchanged with
  HTTP range support instead of invoking Jellyfin's subtitle encoder.
- [x] 2026-07-28: Build the forked jellyfin-web source and package it with the
  modified server in one runtime image.

## Planned

- [ ] Add focused unit tests for root path mapping, subtitle state transitions,
  and delete failure handling.
- [x] 2026-07-29: Add an administrator page for migration preview, execution,
  status, and failed entries on the personal jellyfin-web branch.
- [x] 2026-07-28: Add a dedicated Local/OpenList source picker and authenticated
  OpenList directory browser to `autofilm-jellyfin-web`.
- [ ] Verify Infuse seeking, playback reporting, deletion, and SUP/PGS behavior
  on a physical client; server-side PlaybackInfo and subtitle 302 are verified.
- [ ] Perform a stopped-database backup and rollback exercise before production
  migration.
