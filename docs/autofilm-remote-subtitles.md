# OpenList sidecar subtitle synchronization

Updated: 2026-08-16

External OpenList subtitles are stored as normal Jellyfin media-stream records
whose paths use `openlist:///`. Their contents remain in OpenList and are
served through Jellyfin's subtitle endpoint with an OpenList redirect.

An explicit OpenList scan can discover a sidecar only from a directory that
was completely enumerated during that request. An exact video scan therefore
lists its containing directory once in addition to resolving the selected
object. The directory response supplies both the video and its sibling
subtitle names; no subtitle content is downloaded and no second subtitle
lookup is made.

A sibling is associated with a video when:

1. its extension is supported by Jellyfin's subtitle naming parser;
2. its basename starts with the complete video basename; and
3. any remaining suffix begins with a configured media-flag delimiter.

For example, `Movie.2015.zh.srt` belongs to `Movie.2015.mkv`. Jellyfin's normal
external-path parser derives language, title, default, forced and
hearing-impaired flags. The resulting stream uses the same representation as a
subtitle uploaded through Jellyfin's API.

The two scan modes have different database behavior:

- `new` adds newly discovered subtitle records and preserves existing records
  that are absent from the current response.
- `full` adds newly discovered records and removes a remote external-subtitle
  record when its exact containing directory was enumerated successfully and
  that path is absent.

Local subtitle records are never removed by this synchronization. A failed or
unlisted directory cannot remove remote records either. These limits keep a
temporary OpenList or storage outage from being treated as a confirmed
subtitle deletion.
