# AutoFilm development status

Updated: 2026-07-28

## Completed

- [x] 2026-07-28: Store item and external subtitle paths as `openlist:///`.
- [x] 2026-07-28: Return OpenList 302 responses for video and remote subtitles.
- [x] 2026-07-28: Expose direct-play-only HTTP media sources with transcoding
  disabled.
- [x] 2026-07-28: Use Jellyfin resolvers and metadata providers for new remote
  paths.
- [x] 2026-07-28: Add a serialized, rate-limited probe queue for new videos.
- [x] 2026-07-28: Add OpenList subtitle reads and stale stream removal.
- [x] 2026-07-28: Store new subtitle uploads directly in OpenList.
- [x] 2026-07-28: Delete OpenList paths before removing Jellyfin items.
- [x] 2026-07-28: Receive token-protected active OpenList path events.
- [x] 2026-07-28: Update paths in place for move events and preserve item IDs.
- [x] 2026-07-28: Build and run the self-contained DSM test image.
- [x] 2026-07-28: Store remote media library roots as `openlist:///` in
  `PathInfos` and `.mblink` files while preserving local libraries.
- [x] 2026-07-28: Add Local/OpenList library source typing and an authenticated
  OpenList directory browser endpoint.
- [x] 2026-07-28: Restrict remote refresh and OpenList events to configured
  remote library roots.
- [x] 2026-07-28: Normalize external SUP streams in PlaybackInfo and serve
  them through Jellyfin's own subtitle endpoint.
- [x] 2026-07-28: Return real local-library external SUP files unchanged with
  HTTP range support instead of invoking Jellyfin's subtitle encoder.
- [x] 2026-07-28: Build the forked jellyfin-web source and package it with the
  modified server in one runtime image.
- [x] 2026-07-28: Isolate installation-specific database migration and legacy
  subtitle reverse lookup in `codex/personal-legacy-compat`.
- [x] 2026-07-28: Keep the default branch free of legacy path configuration,
  migration APIs, and local fallback upload behavior.

## Planned

- [ ] Add focused unit tests for root path mapping, subtitle state transitions,
  event idempotency, and delete failure handling.
- [x] 2026-07-28: Add a dedicated Local/OpenList source picker and authenticated
  OpenList directory browser to `autofilm-jellyfin-web`.
- [ ] Verify Infuse seeking, playback reporting, deletion, and SUP/PGS behavior
  on a physical client; server-side PlaybackInfo and subtitle 302 are verified.
