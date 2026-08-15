using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace Emby.Server.Implementations.AutoFilm;

internal sealed record AutoFilmRemoteSubtitleSyncResult(
    IReadOnlyList<MediaStream> Streams,
    string[] SubtitleFiles,
    bool Changed);
