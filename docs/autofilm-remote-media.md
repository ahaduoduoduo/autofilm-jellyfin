# AutoFilm remote media behavior

Updated: 2026-08-05

## Persistent model

Remote items and external subtitles use one reversible URI:

```text
openlist:///movie/example/example.mkv
openlist:///movie/example/example.zh-CN.ass
```

Jellyfin does not persist OpenList storage IDs, object IDs, pick codes,
LegacyPath, or a separate AutoFilm catalog.

Remote media library roots use the same URI in Jellyfin's normal
`LibraryOptions.PathInfos` and `.mblink` files. `MediaPathInfo.SourceType`
distinguishes `Local` and `OpenList`; a user-selected OpenList absolute path is
normalized to `openlist:///` before it is saved. Local and OpenList sources may
coexist in the same server.

## Configuration

| Variable | Purpose | Default |
|---|---|---|
| `AUTOFILM_OPENLIST_URL` | Container-internal OpenList URL | empty |
| `AUTOFILM_OPENLIST_PUBLIC_URL` | URL returned to playback clients | internal URL |
| `AUTOFILM_OPENLIST_TOKEN` | OpenList AutoFilm service token | empty |
| `AUTOFILM_REMOTE_REFRESH_MAX_DIRECTORIES` | Directory limit per refresh | `64` |
| `AUTOFILM_REMOTE_REFRESH_MAX_OBJECTS` | Object limit per refresh | `5000` |
| `AUTOFILM_REMOTE_PROBE_INTERVAL_SECONDS` | Minimum ffprobe interval | `30` |

The public and internal URLs are intentionally separate. Client redirects use
the public URL; server-side ffprobe uses the internal URL.

## Media library sources

The normal Jellyfin virtual-folder API accepts:

```json
{
  "LibraryOptions": {
    "PathInfos": [
      {
        "Path": "/115/movie",
        "SourceType": "OpenList"
      }
    ]
  }
}
```

The saved path becomes `openlist:///115/movie`. The existing Local source
behavior is unchanged. An administrator interface can browse OpenList without
storage-specific knowledge:

```text
POST /AutoFilm/OpenList/Browse
```

OpenList roots receive synthetic directory metadata only for Jellyfin library
registration. Standard filesystem scans and realtime `FileSystemWatcher`
instances do not enumerate remote roots. Remote discovery comes from
`RemoteRefresh` or an administrator selecting a remote path.

## New remote paths

```http
POST /AutoFilm/RemoteRefresh
Content-Type: application/json

{
  "path": "/tv/example/Season 01/example.S01E01.mkv",
  "recursive": false,
  "refresh": false,
  "force_probe": false,
  "provider_target": "movie",
  "provider_ids": {
    "Tmdb": "123"
  }
}
```

`provider_ids` is optional. For a single-file movie result directory,
`provider_target: "movie"` binds those IDs to the direct video rather than the
wrapper directory. Jellyfin builds an in-memory directory snapshot,
uses its normal resolvers, creates missing records, queues normal metadata
providers, and probes only new videos or explicit force requests.

An administrator's standard Jellyfin metadata refresh also enqueues the exact
OpenList Movie or Episode with `force=false`. This covers historical records
whose path and metadata exist but whose ffprobe never completed. The queue
checks the stored streams before accessing OpenList and returns immediately
when an embedded video stream already exists, so refreshing a healthy remote
item does not read the provider or replace valid tracks. A streamless item is
probed through the authenticated internal OpenList download URL; the result
updates tracks, width, height, runtime, bitrate, container and remote object
size without changing Item ID, provider IDs, images or user data.

When `refresh:true`, the exact target object lookup refreshes its OpenList
parent directory before resolving the target. This is required after a remote
offline task succeeds because the provider result can exist while OpenList's
cached parent listing still reflects the previous state. The refresh remains
bounded to that parent and the requested result hierarchy.

When `recursive:true` targets a Jellyfin folder, Jellyfin resolves every
missing descendant from the bounded snapshot and creates the missing records
through its normal library resolvers. The same importer runs whether the target
folder already exists or is being created by this request. A newly downloaded
episode, season directory, or multi-season directory therefore receives all
of its episode records during its first explicit refresh. The same operation
also restores episodes lost during an earlier provider outage.

The operation is additive: records absent from the current OpenList snapshot
are preserved rather than deleted, because a remote storage outage must not be
interpreted as a confirmed media deletion. Every discovered video also
receives its normal metadata refresh. Episode number parsing accepts the
underlying OpenList path, so names such as `The.Capture.S03E01.mkv` populate
season and episode indexes in the same way as local files.

The requested path must be inside a configured OpenList media library root.
Parent discovery uses only `openlist:///` Folder records and never accesses a
host filesystem path for an OpenList item.

## Existing media replacement

Resource upgrades do not use `RemoteRefresh`, because importing the replacement
as new media would create another item and separate user data. The replacement
API modifies only the media backing an existing remote Movie or Episode.

All endpoints require administrator permission:

```text
POST /AutoFilm/MediaReplacement/Inspect
POST /AutoFilm/MediaReplacement/Preview
POST /AutoFilm/MediaReplacement/Apply
POST /AutoFilm/MediaReplacement/Rollback
```

`Inspect` accepts an OpenList path and a recursive flag. It lists at most 64
directories and 5000 objects, runs the configured Jellyfin `VideoResolver` and
`EpisodeResolver`, and returns recognized video paths, sizes, extra types and
season/episode hints. It is read-only and does not create library records.

`Preview` accepts an existing Item ID and one exact OpenList file path. The
target must be an OpenList-backed Video. Jellyfin:

1. refreshes and resolves the exact OpenList object;
2. verifies that its extension is recognized by current naming options;
3. probes the internal signed download URI through `IMediaEncoder`;
4. returns current and replacement size, duration, bitrate, container,
   resolution and streams;
5. stores an immutable preview token for 30 minutes.

At most two replacement probes run concurrently. This limit is separate from
the serialized new-media probe queue. A probe that throws `FfmpegException`
is retried after 3 and 8 seconds, for at most three total attempts. This is a
finite replacement-only retry; cancellation and other exception types are not
retried, and the global new-media probe policy is unchanged.

`Apply` consumes the preview once and acquires a lock for that Item ID. It
requires the Jellyfin path to remain unchanged, the replacement size and
modification time to match the preview, and the replacement to be in the same
OpenList directory as the current video. It then:

- replaces the path, size, duration, bitrate, container, width, height and
  default video stream on the existing Video record;
- stores the replacement's internal streams;
- retains the current external subtitle streams and assigns a stable combined
  stream order;
- updates the repository as a metadata edit without running metadata providers,
  changing provider IDs, changing images or creating another item.

If either stream persistence or the Video update fails, Apply restores the
previous fields and streams before returning the error. A successful result
contains a rollback token and the same Item ID. The token is held in memory for
seven days; a Jellyfin restart invalidates it.

`Rollback` requires the old file to be present at its original path and the
current item still to reference the applied replacement. AutoFilm Core moves
its saved old file back before calling this endpoint. If the token was lost
after a Jellyfin restart, Core probes the restored old file and applies it as a
new reverse replacement.

Core moves the replacement into the existing media directory before Apply.
After Apply returns and the same Item ID reports the new path and a video
stream, Core marks the item successful and moves the old file to its isolated
backup directory. No user playback confirmation is required.

## Playback

For a remote video, `/Videos/{id}/stream`:

1. Converts `item.Path` to an OpenList absolute path.
2. Calls `/api/autofilm/objects/get`.
3. Returns a signed 302 using `AUTOFILM_OPENLIST_PUBLIC_URL`.

The `Location` value uses the URI's escaped absolute representation. OpenList
paths containing Chinese or other non-ASCII names therefore remain
percent-encoded and are valid HTTP response headers.

The generated media source is HTTP, direct-play-only, and reports existing
Jellyfin `MediaStreams`. After the standard device-profile calculation,
`MediaInfoHelper` restores the direct-play-only flags for media source IDs with
the `autofilm:` prefix and clears any generated `TranscodingUrl` and
transcoding container. Third-party clients therefore receive no usable
transcoding variant for OpenList media. Local media continues through
Jellyfin's standard playback capability calculation and may be transcoded.

## Subtitles

For an external `openlist:///` subtitle:

1. Return OpenList 302 when the remote path exists.
2. On explicit remote 404, remove the stale stream record.
3. Authentication, network, or provider failures never remove the record.

The resolver accepts `.ass`, `.ssa`, `.srt`, `.vtt`, `.sub`, `.idx`, and
`.sup`. `openlist:///` is an AutoFilm database URI rather than a path understood
by upstream Jellyfin; `AutoFilmSubtitleService` resolves it before a client
receives the subtitle response. The remote response serves the original
subtitle format. A request that asks Jellyfin to convert ASS/SRT to another
format does not pass the remote source through Jellyfin's subtitle encoder.

Jellyfin's standard `/Videos/{id}/Subtitles` JSON endpoint remains compatible
with Jellyfin Web, third-party clients, and plugins. AutoFilm Core sends every
subtitle format to the authenticated
`/AutoFilm/Videos/{id}/Subtitles` binary endpoint instead. The request body is
streamed through Jellyfin to OpenList without Base64 expansion or a fixed
request-size limit; the original content length is preserved when known.

Both endpoints use the same internal subtitle save operation. Local items
continue through Jellyfin's normal media-folder or metadata-folder save path.
Remote items are written to OpenList first and then added to
`MediaStreamInfos`. If the language filename already exists, remote uploads use
Jellyfin-compatible numbered names such as `.zh.0.ass`.

The standard subtitle delete endpoint removes local subtitles through
Jellyfin's subtitle manager and removes remote subtitles through OpenList.
AutoFilm Core therefore does not write, rename, or delete subtitle paths
directly and does not request a directory refresh after subtitle operations.

External `.sup` streams are returned in item and PlaybackInfo media streams as:

```text
Codec=sup
IsExternal=true
IsExternalUrl=true
SupportsExternalStream=true
DeliveryMethod=External
```

Jellyfin's subtitle route returns the OpenList 302 itself. For a real local
media library, an existing external `.sup` requested as `sup` or `pgssub` is
returned as the original physical file with HTTP range support. Both paths
bypass the text subtitle encoder and replace the old Nginx PGS/SUP response
rewriting.

## Explicit refresh

OpenList uploads, moves, renames, and deletions do not automatically change
Jellyfin. AutoFilm Core calls `RemoteRefresh` after a completed new-media
download. Existing-media upgrades use the explicit replacement API above.
An OpenList administrator may explicitly request the same operation for a
selected path. Administrators can also select **Scan OpenList content** from
the Jellyfin item menu for an existing OpenList folder. The explicit action
requests a refreshed recursive snapshot and adds missing descendants without
deleting existing records. Jellyfin does not poll OpenList.

## Personal compatibility branch

The default branch does not contain database path migration or legacy subtitle
reverse lookup. The installation-specific implementation remains in
`codex/personal-legacy-compat`; it keeps the batch path migration endpoints and
the read-only local subtitle fallback with serialized lazy upload.

## Deletion

Remote item and subtitle deletion calls the OpenList path delete endpoint
before changing Jellyfin. If OpenList fails, Jellyfin returns an error and
preserves the local record.

Movie, episode, series, and physical season DTOs backed by a valid
`openlist:///` path report `CanDelete=true` when the current user has Jellyfin
media-deletion permission. Jellyfin Web therefore exposes its normal delete
action for those remote items. Deleting a season or series deletes its OpenList
directory once, then removes the aggregate item and descendants from Jellyfin.
The standard confirmation dialog and user library restrictions remain in
effect.

Virtual seasons do not have their own path and remain non-deletable, preventing
a season action from deleting the containing series directory. Arbitrary HTTP
media and remote library roots also remain non-deletable. AutoFilm Core's Agent
tool continues to accept only exact Movie and Episode IDs; directory-level
Series and Season deletion is reserved for Jellyfin's user interface.

## Safety

- Real 115 delete, move, and bulk subtitle upload tests require a dedicated test
  directory.
- The default branch does not read legacy media or subtitle mounts; rclone,
  symlink and Nginx services are not part of the remote-media design.
