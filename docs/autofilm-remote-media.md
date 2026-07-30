# AutoFilm remote media behavior

Updated: 2026-07-30

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
| `AUTOFILM_LEGACY_MEDIA_PREFIX` | Existing Jellyfin path prefix | `/movie/drimnt` |
| `AUTOFILM_OPENLIST_MEDIA_PREFIX` | Corresponding OpenList prefix | `/` |
| `AUTOFILM_LEGACY_SUBTITLE_ROOT` | Read-only legacy subtitle mount | `/legacy-subtitles` |
| `AUTOFILM_REMOTE_REFRESH_MAX_DIRECTORIES` | Directory limit per refresh | `64` |
| `AUTOFILM_REMOTE_REFRESH_MAX_OBJECTS` | Object limit per refresh | `5000` |
| `AUTOFILM_REMOTE_PROBE_INTERVAL_SECONDS` | Minimum ffprobe interval | `30` |

The public and internal URLs are intentionally separate. Client redirects use
the public URL; server-side ffprobe uses the internal URL.

## Migration

```text
POST /AutoFilm/Migration/Preview?limit=100
POST /AutoFilm/Migration/Apply?limit=100
```

Both endpoints require administrator permission. Preview does not write.
Apply changes existing item paths, physical Folder and base Video paths,
external subtitle paths, external SUP codecs, media library `PathInfos`, and
`.mblink` targets.

Migration guarantees:

- No OpenList request.
- No 115 directory request.
- No ffprobe.
- No metadata refresh.
- Existing item IDs, provider IDs, user data, and media streams remain.
- Repeated runs skip paths that are already `openlist:///`.
- Existing local libraries are not changed.
- A successful repeated preview returns zero candidates.

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
Parent discovery uses only `openlist:///` Folder records; it never falls back to
the legacy local path.

## Playback

For a remote video, `/Videos/{id}/stream`:

1. Converts `item.Path` to an OpenList absolute path.
2. Calls `/api/autofilm/objects/get`.
3. Returns a signed 302 using `AUTOFILM_OPENLIST_PUBLIC_URL`.

The `Location` value uses the URI's escaped absolute representation. OpenList
paths containing Chinese or other non-ASCII names therefore remain
percent-encoded and are valid HTTP response headers.

The generated media source is HTTP, direct-play-only, and reports existing
Jellyfin `MediaStreams`. Transcoding is disabled.

## Subtitles

For an external `openlist:///` subtitle:

1. Return OpenList 302 when the remote path exists.
2. On explicit remote 404, derive the read-only legacy path.
3. If local exists, return it immediately and queue a serialized upload.
4. If both sources are absent, remove the stale stream record.
5. Authentication, network, or provider failures never remove the record.

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
Jellyfin. AutoFilm Core calls `RemoteRefresh` after a completed media download.
An OpenList administrator may explicitly request the same operation for a
selected path. Administrators can also select **Scan OpenList content** from
the Jellyfin item menu for an existing OpenList folder. The explicit action
requests a refreshed recursive snapshot and adds missing descendants without
deleting existing records. Jellyfin does not poll OpenList.

## Deletion

Remote item and subtitle deletion calls the OpenList path delete endpoint
before changing Jellyfin. If OpenList fails, Jellyfin returns an error and
preserves the local record.

## Safety

- The legacy subtitle mount is read-only.
- Batch migration must run on a copied database first.
- Real 115 delete, move, and bulk subtitle upload tests require a dedicated test
  directory.
- Existing rclone, symlink, and Nginx services remain available only as
  production rollback components until physical Infuse validation and rollback
  preparation are complete.
