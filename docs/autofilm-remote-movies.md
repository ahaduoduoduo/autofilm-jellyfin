# OpenList movie release directories

Updated: 2026-08-12

AutoFilm scans one bounded OpenList path after an offline download succeeds.
Movie releases commonly contain a wrapper directory with one primary video,
subtitle and NFO files, and sometimes a `Sample` directory or promotional
videos. Jellyfin's normal movie resolver deliberately declines some of these
directories because their contents can also represent several independent
videos.

The AutoFilm resolver is limited to explicit OpenList scans inside a Jellyfin
movie library. Local libraries, scheduled library scans, television libraries,
and other Jellyfin resolver calls retain upstream behavior.

For one release directory, primary-video selection follows these rules:

1. If exactly one direct child resolves as a Movie, use it even when other
   subdirectories such as `Sample` exist.
2. If several direct children resolve as Movies, use one only when its
   normalized filename is the unique match contained in the normalized release
   directory name.
3. If selection remains ambiguous, keep Jellyfin's original Folder and Video
   representation. File size is not used to guess between real cuts or
   editions.

Provider identifiers from the refresh request are applied to the selected
Movie. A full remote rescan uses the same resolver to replace a previous
`Folder + Video` representation while retaining provider identifiers, core
metadata, linked collection references, and user data. The replacement never
renames, moves, or deletes an OpenList object.

Full rescans also remove database-only descendants after a fresh non-empty
OpenList snapshot succeeds. Additive scans continue to preserve missing
records so a temporary remote outage cannot erase the library.

When the selected item itself no longer exists, a full scan refreshes its
parent first. Jellyfin removes the stale database item only when that parent
still returns other objects and the target remains absent. An empty parent is
treated as an unavailable or ambiguous remote result and removal is refused.
