using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using Emby.Naming.ExternalFiles;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Synchronizes OpenList sidecar subtitle records from a bounded directory snapshot.
/// </summary>
public sealed class AutoFilmRemoteSubtitleScanner
{
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly ExternalPathParser _pathParser;
    private readonly NamingOptions _namingOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmRemoteSubtitleScanner"/> class.
    /// </summary>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    /// <param name="localizationManager">Jellyfin language resolver.</param>
    /// <param name="namingOptions">Jellyfin media naming options.</param>
    public AutoFilmRemoteSubtitleScanner(
        IMediaStreamRepository mediaStreamRepository,
        ILocalizationManager localizationManager,
        NamingOptions namingOptions)
    {
        _mediaStreamRepository = mediaStreamRepository;
        _namingOptions = namingOptions;
        _pathParser = new ExternalPathParser(
            namingOptions,
            localizationManager,
            DlnaProfileType.Subtitle);
    }

    /// <summary>
    /// Synchronizes subtitles for videos whose containing directories were enumerated.
    /// </summary>
    /// <param name="videos">Resolved remote videos.</param>
    /// <param name="snapshot">Bounded OpenList snapshot.</param>
    /// <param name="removeMissing">Whether missing remote subtitle records are removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The asynchronous operation.</returns>
    internal async Task SynchronizeAsync(
        IReadOnlyList<Video> videos,
        AutoFilmDirectorySnapshot snapshot,
        bool removeMissing,
        CancellationToken cancellationToken)
    {
        foreach (var video in videos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentStreams = _mediaStreamRepository.GetMediaStreams(
                new MediaStreamQuery { ItemId = video.Id });
            var result = BuildResult(
                video,
                currentStreams,
                snapshot,
                _pathParser,
                _namingOptions,
                removeMissing);
            if (result is null || !result.Changed)
            {
                continue;
            }

            _mediaStreamRepository.SaveMediaStreams(
                video.Id,
                result.Streams,
                cancellationToken);
            video.SubtitleFiles = result.SubtitleFiles;
            video.HasSubtitles = result.Streams.Any(
                stream => stream.Type == MediaStreamType.Subtitle);
            await video.UpdateToRepositoryAsync(
                ItemUpdateType.MetadataImport,
                cancellationToken).ConfigureAwait(false);
        }
    }

    internal static AutoFilmRemoteSubtitleSyncResult? BuildResult(
        Video video,
        IReadOnlyList<MediaStream> currentStreams,
        AutoFilmDirectorySnapshot snapshot,
        ExternalPathParser pathParser,
        NamingOptions namingOptions,
        bool removeMissing)
    {
        if (!AutoFilmRemotePath.IsRemote(video.Path))
        {
            return null;
        }

        var directory = GetParentPath(video.Path);
        if (!snapshot.WasDirectoryEnumerated(directory))
        {
            return null;
        }

        var snapshotFiles = snapshot.GetFiles(directory);
        var snapshotPaths = snapshotFiles
            .Select(file => file.FullName)
            .ToHashSet(StringComparer.Ordinal);
        var streams = currentStreams
            .Where(stream => !ShouldRemoveMissing(
                stream,
                directory,
                snapshotPaths,
                removeMissing))
            .ToList();
        var changed = streams.Count != currentStreams.Count;
        var existingPaths = streams
            .Where(stream => stream.Type == MediaStreamType.Subtitle
                && stream.IsExternal
                && !string.IsNullOrWhiteSpace(stream.Path))
            .Select(stream => stream.Path)
            .ToHashSet(StringComparer.Ordinal);
        var nextIndex = streams.Count == 0
            ? 0
            : streams.Max(stream => stream.Index) + 1;
        var prefix = Path.GetFileNameWithoutExtension(
            video.Path[(video.Path.LastIndexOf('/') + 1)..]);
        foreach (var file in snapshotFiles)
        {
            if (!TryParseSidecar(
                    file.FullName,
                    prefix,
                    pathParser,
                    namingOptions,
                    out var pathInfo)
                || !existingPaths.Add(file.FullName))
            {
                continue;
            }

            streams.Add(AutoFilmExternalSubtitleStream.Create(
                file.FullName,
                nextIndex++,
                pathInfo!.Language,
                pathInfo.Title,
                pathInfo.IsDefault,
                pathInfo.IsForced,
                pathInfo.IsHearingImpaired));
            changed = true;
        }

        var subtitleFiles = streams
            .Where(stream => stream.Type == MediaStreamType.Subtitle
                && stream.IsExternal
                && !string.IsNullOrWhiteSpace(stream.Path))
            .Select(stream => stream.Path)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        changed |= !new HashSet<string>(
            video.SubtitleFiles ?? Array.Empty<string>(),
            StringComparer.Ordinal).SetEquals(subtitleFiles);
        return new AutoFilmRemoteSubtitleSyncResult(
            streams,
            subtitleFiles,
            changed);
    }

    private static bool TryParseSidecar(
        string path,
        string prefix,
        ExternalPathParser pathParser,
        NamingOptions namingOptions,
        out ExternalPathParserResult? pathInfo)
    {
        pathInfo = null;
        if (!AutoFilmExternalSubtitleStream.IsSupportedPath(path))
        {
            return false;
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        if (!fileNameWithoutExtension.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase)
            || (fileNameWithoutExtension.Length > prefix.Length
                && !namingOptions.MediaFlagDelimiters.Contains(
                    fileNameWithoutExtension[prefix.Length])))
        {
            return false;
        }

        pathInfo = pathParser.ParseFile(
            path,
            fileNameWithoutExtension[prefix.Length..]);
        return pathInfo is not null;
    }

    private static bool ShouldRemoveMissing(
        MediaStream stream,
        string directory,
        IReadOnlySet<string> snapshotPaths,
        bool removeMissing)
    {
        return removeMissing
            && stream.Type == MediaStreamType.Subtitle
            && stream.IsExternal
            && !string.IsNullOrWhiteSpace(stream.Path)
            && AutoFilmRemotePath.TryGetOpenListPath(stream.Path, out _)
            && string.Equals(
                GetParentPath(stream.Path),
                directory,
                StringComparison.Ordinal)
            && !snapshotPaths.Contains(stream.Path);
    }

    private static string GetParentPath(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator <= "openlist://".Length
            ? "openlist:///"
            : path[..separator];
    }
}
