# OpenList television package handling

AutoFilm downloads can contain a release-name directory between the Jellyfin
Series directory and the physical season directories:

```text
Barry/
└── Barry 2018 S01-S04 Complete 1080p .../
    ├── S01/
    ├── S02/
    ├── S03/
    └── S04/
```

The physical release directory is not a reliable metadata target. Jellyfin's
normal resolver can initially classify a name containing `S01-S04` as a
Season. A RemoteRefresh request therefore resolves the nearest owning Series
through the persisted parent hierarchy before applying TMDB, TVDB, IMDb, or
other provider identifiers. AutoFilm Core declares `provider_target=series`;
Jellyfin verifies both the television virtual folder and the resolved Series
before saving the supplied TMDB ID. Manual scans omit the declaration and use
the normal library type.

The request path remains the bounded OpenList enumeration target and keeps its
existing recursive behavior. AutoFilm Core does not interpret the release
layout and does not need a separate multi-season request format.

Before the Series metadata refresh is queued, every discovered Episode is
passed through Jellyfin's standard episode-path parser. The resulting
`ParentIndexNumber` values let `SeriesMetadataService` create the logical
seasons and assign each Episode to the correct season even when physical
directories contain an additional release-name layer.

Episode metadata refreshes prefer the logical season matching the parsed
season number when the physical parent was classified as a different Season.
This prevents a multi-season wrapper directory from reclaiming every Episode
after provider metadata has been refreshed.

Season child, recursive, played, and unplayed counts use each Episode's
logical `SeasonId`. A physical multi-season wrapper can therefore remain in
the path hierarchy without inflating the first season's card statistics.

Requests that include provider identifiers use a full provider refresh. The
Series is queued at high priority; existing Seasons and discovered Episodes
are queued at normal priority so previously empty metadata is fetched after
the Series identity has been persisted. Requests without provider identifiers
keep Jellyfin's default refresh mode.

For discovered videos, an exact provider-backed refresh replaces the
scanner-derived display title with provider metadata. It does not force image
replacement or overwrite Series-level manual metadata.

When upgrading an item created by the previous implementation, matching
provider identifiers are removed from the incorrectly targeted physical
Season after they are saved on the Series. Episode paths and IDs are reused;
OpenList files are not moved, renamed, or deleted.

For an existing malformed Series, deploy the corrected server and issue one
bounded full RemoteRefresh with the Series provider identifiers. The scan must
finish reading the OpenList snapshot before any stale database-only item can be
removed. An empty or incomplete remote snapshot is rejected by the existing
full-scan safety checks.
