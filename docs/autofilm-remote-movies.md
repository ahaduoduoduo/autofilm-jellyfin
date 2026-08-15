# OpenList movie release directories

Updated: 2026-08-16

AutoFilm scans one bounded OpenList path after an offline download succeeds.
Movie releases commonly contain a wrapper directory with one primary video,
subtitle and NFO files, and sometimes a `Sample` directory or promotional
videos. Jellyfin's normal movie resolver deliberately declines some of these
directories because their contents can also represent several independent
videos.

The AutoFilm resolver is limited to explicit OpenList scans inside a Jellyfin
movie library. Local libraries, scheduled library scans, television libraries,
and other Jellyfin resolver calls retain upstream behavior.

The library type comes from Jellyfin's virtual-folder configuration. This is
also used when the persisted OpenList root and its descendants are ordinary
`Folder` rows and therefore do not carry an inherited content type themselves.

Some offline providers create a wrapper directory whose name itself ends in a
video extension, for example `Movie.2015.2160p.mkv/Movie.2015.2160p.mkv`.
Jellyfin's mixed-content television resolver can provisionally classify that
wrapper as a Series. During an explicit movie-library scan, AutoFilm resolves
the direct video against the real movie-library parent instead of the
provisional Series, so the stored item remains a Movie. Television libraries
and normal Jellyfin scans retain upstream behavior.

After a Movie has provider identifiers, an exact full scan preserves that
Movie identity even when its persisted wrapper is an ordinary Folder without
an inherited collection type. A generic Video without provider identifiers is
not promoted by this rule, so samples, extras and ambiguous standalone files
continue to use Jellyfin's normal resolver result.

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
Movie. When a trusted importer declares `provider_target=movie`, Jellyfin first
requires a movie virtual folder and a resolved Movie. A conflict is rejected
before the TMDB ID is saved or provider metadata is requested. Manual scans do
not send this field and continue to infer the type from the configured library.
Provider-backed imports replace the scanner-derived release name with
provider metadata while retaining existing images. A full remote rescan uses
the same resolver to replace a previous
`Folder + Video` representation while retaining provider identifiers, core
metadata, linked collection references, and user data. The replacement never
renames, moves, or deletes an OpenList object.

A Movie selected from inside a wrapper directory is persisted directly below
the wrapper's stored parent. The temporary wrapper resolver object is never
used as a database parent, including both new imports and type replacements.

Full rescans also remove database-only descendants after a fresh non-empty
OpenList snapshot succeeds. Additive scans continue to preserve missing
records so a temporary remote outage cannot erase the library.

When the selected item itself no longer exists, a full scan refreshes its
parent first. Jellyfin removes the stale database item only when that parent
still returns other objects and the target remains absent. An empty parent is
treated as an unavailable or ambiguous remote result and removal is refused.
