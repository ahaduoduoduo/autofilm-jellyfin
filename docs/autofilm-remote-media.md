# AutoFilm remote media behavior

Updated: 2026-07-28

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
| `AUTOFILM_JELLYFIN_INBOUND_TOKEN` | Token accepted from OpenList events | empty |
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
instances do not enumerate remote roots. Remote discovery comes from active
OpenList events, `RemoteRefresh`, or an administrator selecting a remote path.

## New remote paths

```http
POST /AutoFilm/RemoteRefresh
Content-Type: application/json

{
  "path": "/tv/example/Season 01/example.S01E01.mkv",
  "recursive": false,
  "refresh": false,
  "force_probe": false,
  "provider_ids": {
    "Tmdb": "123"
  }
}
```

`provider_ids` is optional. Jellyfin builds an in-memory directory snapshot,
uses its normal resolvers, creates missing records, queues normal metadata
providers, and probes only new videos or explicit force requests.

The requested path must be inside a configured OpenList media library root.
Parent discovery uses only `openlist:///` Folder records and never accesses a
host filesystem path for an OpenList item.

## Playback

For a remote video, `/Videos/{id}/stream`:

1. Converts `item.Path` to an OpenList absolute path.
2. Calls `/api/autofilm/objects/get`.
3. Returns a signed 302 using `AUTOFILM_OPENLIST_PUBLIC_URL`.

The generated media source is HTTP, direct-play-only, and reports existing
Jellyfin `MediaStreams`. Transcoding is disabled.

## Subtitles

For an external `openlist:///` subtitle:

1. Return OpenList 302 when the remote path exists.
2. On explicit remote 404, remove the stale stream record.
3. Authentication, network, or provider failures never remove the record.

New Jellyfin subtitle uploads are written to OpenList first and then added to
`MediaStreamInfos`.

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

## Events

```http
POST /AutoFilm/Events
X-AutoFilm-Token: ...
```

Supported types:

- `object.upsert`: refresh or create the path.
- `object.move`: update the existing item and subtitle paths in place.
- `object.remove`: delete the Jellyfin record without deleting OpenList again.

Subtitle events are acknowledged without directory refresh because subtitle
reads and AutoFilm uploads manage their stream records directly.

A move between configured roots preserves the item and reparents it, creating
missing intermediate Folder records. A move out of all configured roots removes
the Jellyfin item; a move from outside into a configured root creates it.

Jellyfin does not poll OpenList.

## Personal compatibility branch

The default branch does not contain database path migration or legacy subtitle
reverse lookup. The installation-specific implementation remains in
`codex/personal-legacy-compat`; it keeps the batch path migration endpoints and
the read-only local subtitle fallback with serialized lazy upload.

## Deletion

Remote item and subtitle deletion calls the OpenList path delete endpoint
before changing Jellyfin. If OpenList fails, Jellyfin returns an error and
preserves the local record.

## Safety

- Real 115 delete, move, and bulk subtitle upload tests require a dedicated test
  directory.
- Existing rclone, symlink, and Nginx services remain available only as
  production rollback components until physical Infuse validation and rollback
  preparation are complete.
