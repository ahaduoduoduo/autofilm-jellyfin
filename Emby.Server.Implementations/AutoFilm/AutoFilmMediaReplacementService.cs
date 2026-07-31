using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using Emby.Naming.TV;
using Emby.Naming.Video;
using MediaBrowser.Common;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Changes the media backing an existing remote video while preserving the
/// Jellyfin item identity and all provider and user metadata.
/// </summary>
public sealed class AutoFilmMediaReplacementService
    : IAutoFilmMediaReplacementService, IDisposable
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RollbackLifetime = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions CloneOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, ReplacementPlan> _previews = new();
    private readonly ConcurrentDictionary<string, RollbackPlan> _rollbacks = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _itemLocks = new();
    private readonly SemaphoreSlim _probeSlots = new(2, 2);
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly IAutoFilmOpenListClient _openListClient;
    private readonly NamingOptions _namingOptions;
    private readonly ILogger<AutoFilmMediaReplacementService> _logger;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmMediaReplacementService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    /// <param name="mediaEncoder">Jellyfin media probing service.</param>
    /// <param name="openListClient">OpenList object and download API client.</param>
    /// <param name="namingOptions">Configured Jellyfin media naming options.</param>
    /// <param name="logger">Service logger.</param>
    public AutoFilmMediaReplacementService(
        ILibraryManager libraryManager,
        IMediaStreamRepository mediaStreamRepository,
        IMediaEncoder mediaEncoder,
        IAutoFilmOpenListClient openListClient,
        NamingOptions namingOptions,
        ILogger<AutoFilmMediaReplacementService> logger)
    {
        _libraryManager = libraryManager;
        _mediaStreamRepository = mediaStreamRepository;
        _mediaEncoder = mediaEncoder;
        _openListClient = openListClient;
        _namingOptions = namingOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AutoFilmMediaReplacementInspectResult> InspectAsync(
        AutoFilmMediaReplacementInspectRequest request,
        CancellationToken cancellationToken)
    {
        var requestedPath = NormalizePath(request.Path);
        var root = await _openListClient.GetObjectAsync(
            requestedPath,
            true,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("OpenList replacement path does not exist.");
        var candidates = new List<AutoFilmMediaReplacementCandidate>();
        var directoriesRead = 0;
        var objectsRead = 1;
        var queue = new Queue<AutoFilmOpenListObject>();
        if (root.IsDirectory)
        {
            queue.Enqueue(root);
        }
        else
        {
            AddCandidate(root, candidates);
        }

        while (queue.TryDequeue(out var directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++directoriesRead > 64)
            {
                throw new InvalidOperationException("Replacement inspection exceeded 64 directories.");
            }

            var children = await _openListClient.ListObjectsAsync(
                directory.Path,
                directoriesRead == 1,
                cancellationToken).ConfigureAwait(false);
            objectsRead += children.Count;
            if (objectsRead > 5000)
            {
                throw new InvalidOperationException("Replacement inspection exceeded 5000 objects.");
            }

            foreach (var child in children)
            {
                if (child.IsDirectory)
                {
                    if (request.Recursive)
                    {
                        queue.Enqueue(child);
                    }
                }
                else
                {
                    AddCandidate(child, candidates);
                }
            }
        }

        return new AutoFilmMediaReplacementInspectResult(
            requestedPath,
            directoriesRead,
            objectsRead,
            candidates
                .OrderByDescending(candidate => candidate.Size)
                .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
                .ToArray());
    }

    /// <inheritdoc />
    public async Task<AutoFilmMediaReplacementPreview> PreviewAsync(
        AutoFilmMediaReplacementPreviewRequest request,
        CancellationToken cancellationToken)
    {
        CleanupExpired();
        var video = RequireRemoteVideo(request.ItemId);
        var newPath = NormalizePath(request.NewPath);
        var replacementObject = await _openListClient.GetObjectAsync(
            newPath,
            true,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Replacement file does not exist.");
        if (replacementObject.IsDirectory
            || VideoResolver.ResolveFile(newPath, _namingOptions) is null)
        {
            throw new InvalidOperationException("Replacement path is not a Jellyfin-recognized video file.");
        }

        await _probeSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        MediaInfo mediaInfo;
        try
        {
            mediaInfo = await ProbeAsync(
                video,
                replacementObject,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _probeSlots.Release();
        }

        var oldStreams = CloneStreams(GetStreams(video.Id));
        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow + PreviewLifetime;
        var current = FactsFromVideo(video, oldStreams);
        var replacement = FactsFromProbe(replacementObject, mediaInfo);
        _previews[token] = new ReplacementPlan(
            expiresAt,
            video.Id,
            video.Path,
            replacementObject,
            mediaInfo);
        return new AutoFilmMediaReplacementPreview(
            token,
            expiresAt,
            video.Id,
            video.Name,
            video.GetBaseItemKind().ToString(),
            current,
            replacement);
    }

    /// <inheritdoc />
    public async Task<AutoFilmMediaReplacementResult> ApplyAsync(
        AutoFilmMediaReplacementApplyRequest request,
        CancellationToken cancellationToken)
    {
        CleanupExpired();
        if (!_previews.TryRemove(request.PreviewToken, out var plan)
            || plan.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Replacement preview is missing or expired.");
        }

        var itemLock = _itemLocks.GetOrAdd(plan.ItemId, _ => new SemaphoreSlim(1, 1));
        await itemLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var video = RequireRemoteVideo(plan.ItemId);
            if (!string.Equals(video.Path, plan.ExpectedOldPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Jellyfin item path changed after preview.");
            }

            var currentObject = await _openListClient.GetObjectAsync(
                plan.ReplacementObject.Path,
                true,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Replacement file disappeared after preview.");
            if (currentObject.Size != plan.ReplacementObject.Size
                || currentObject.Modified != plan.ReplacementObject.Modified)
            {
                throw new InvalidOperationException("Replacement file changed after preview.");
            }

            EnsureSameParent(plan.ExpectedOldPath, currentObject.Path);
            var previous = Snapshot(video, CloneStreams(GetStreams(video.Id)));
            var replacementStreams = MergeReplacementStreams(
                plan.MediaInfo.MediaStreams,
                previous.Streams);
            try
            {
                SaveStreams(video.Id, replacementStreams, cancellationToken);
                ApplyMedia(video, currentObject, plan.MediaInfo);
                video.HasSubtitles = replacementStreams.Any(
                    stream => stream.Type == MediaStreamType.Subtitle);
                await video.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                SaveStreams(video.Id, previous.Streams, cancellationToken);
                Restore(video, previous);
                await video.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
                throw;
            }

            var rollbackToken = Guid.NewGuid().ToString("N");
            _rollbacks[rollbackToken] = new RollbackPlan(
                DateTimeOffset.UtcNow + RollbackLifetime,
                video.Id,
                video.Path,
                previous);
            return new AutoFilmMediaReplacementResult(
                "applied",
                video.Id,
                previous.Path,
                video.Path,
                rollbackToken,
                FactsFromVideo(video, replacementStreams));
        }
        finally
        {
            itemLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AutoFilmMediaReplacementResult> RollbackAsync(
        AutoFilmMediaReplacementRollbackRequest request,
        CancellationToken cancellationToken)
    {
        CleanupExpired();
        if (!_rollbacks.TryRemove(request.RollbackToken, out var plan)
            || plan.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Replacement rollback is missing or expired.");
        }

        var itemLock = _itemLocks.GetOrAdd(plan.ItemId, _ => new SemaphoreSlim(1, 1));
        await itemLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var video = RequireRemoteVideo(plan.ItemId);
            if (!string.Equals(video.Path, plan.ExpectedCurrentPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Jellyfin item path changed after replacement.");
            }

            var oldOpenListPath = GetOpenListPath(plan.Previous.Path);
            var oldObject = await _openListClient.GetObjectAsync(
                oldOpenListPath,
                true,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The previous media file is not available.");
            var currentPath = video.Path;
            var current = Snapshot(video, CloneStreams(GetStreams(video.Id)));
            try
            {
                SaveStreams(video.Id, plan.Previous.Streams, cancellationToken);
                Restore(video, plan.Previous);
                video.Size = oldObject.Size;
                await video.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                SaveStreams(video.Id, current.Streams, cancellationToken);
                Restore(video, current);
                await video.UpdateToRepositoryAsync(
                    ItemUpdateType.MetadataEdit,
                    cancellationToken).ConfigureAwait(false);
                throw;
            }

            return new AutoFilmMediaReplacementResult(
                "rolled_back",
                video.Id,
                currentPath,
                video.Path,
                null,
                FactsFromVideo(video, plan.Previous.Streams));
        }
        finally
        {
            itemLock.Release();
        }
    }

    private async Task<MediaInfo> ProbeAsync(
        Video video,
        AutoFilmOpenListObject replacementObject,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _mediaEncoder.GetMediaInfo(
                    new MediaInfoRequest
                    {
                        ExtractChapters = false,
                        MediaType = DlnaProfileType.Video,
                        MediaSource = new MediaSourceInfo
                        {
                            Path = _openListClient.GetInternalDownloadUri(replacementObject).ToString(),
                            Protocol = MediaProtocol.Http,
                            IsRemote = true,
                            VideoType = video.VideoType
                        }
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (FfmpegException ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(attempt == 1 ? 3 : 8);
                _logger.LogWarning(
                    ex,
                    "AutoFilm replacement probe attempt {Attempt}/{MaxAttempts} failed for {Path}; retrying after {Delay}",
                    attempt,
                    maxAttempts,
                    replacementObject.Path,
                    delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void AddCandidate(
        AutoFilmOpenListObject obj,
        ICollection<AutoFilmMediaReplacementCandidate> candidates)
    {
        var video = VideoResolver.ResolveFile(obj.Path, _namingOptions);
        if (video is null)
        {
            return;
        }

        var episode = new EpisodeResolver(_namingOptions).Resolve(
            obj.Path,
            false,
            isOptimistic: false);
        candidates.Add(new AutoFilmMediaReplacementCandidate(
            obj.Path,
            video.Name,
            video.Container,
            obj.Size,
            obj.Modified,
            video.ExtraType?.ToString(),
            episode?.SeasonNumber,
            episode?.EpisodeNumber,
            episode?.EndingEpisodeNumber));
    }

    private Video RequireRemoteVideo(Guid itemId)
    {
        if (_libraryManager.GetItemById<BaseItem>(itemId) is not Video video)
        {
            throw new InvalidOperationException("Jellyfin replacement target is not a video.");
        }

        if (!AutoFilmRemotePath.IsRemote(video.Path))
        {
            throw new InvalidOperationException("Only OpenList-backed videos can be replaced.");
        }

        return video;
    }

    private IReadOnlyList<MediaStream> GetStreams(Guid itemId) =>
        _mediaStreamRepository.GetMediaStreams(new MediaStreamQuery { ItemId = itemId });

    private void SaveStreams(
        Guid itemId,
        IReadOnlyList<MediaStream> streams,
        CancellationToken cancellationToken) =>
        _mediaStreamRepository.SaveMediaStreams(itemId, streams, cancellationToken);

    private static IReadOnlyList<MediaStream> MergeReplacementStreams(
        IReadOnlyList<MediaStream> replacement,
        IReadOnlyList<MediaStream> previous)
    {
        var streams = CloneStreams(replacement.Where(stream => !stream.IsExternal))
            .Concat(CloneStreams(previous.Where(stream => stream.IsExternal)))
            .ToArray();
        for (var index = 0; index < streams.Length; index++)
        {
            streams[index].Index = index;
        }

        return streams;
    }

    private static IReadOnlyList<MediaStream> CloneStreams(IEnumerable<MediaStream> streams)
    {
        var json = JsonSerializer.Serialize(streams.ToArray(), CloneOptions);
        return JsonSerializer.Deserialize<MediaStream[]>(json, CloneOptions)
            ?? Array.Empty<MediaStream>();
    }

    private static VideoSnapshot Snapshot(
        Video video,
        IReadOnlyList<MediaStream> streams) =>
        new(
            video.Path,
            video.Size,
            video.RunTimeTicks,
            video.TotalBitrate,
            video.Container,
            video.Width,
            video.Height,
            video.DefaultVideoStreamIndex,
            video.HasSubtitles,
            CloneStreams(streams));

    private static void ApplyMedia(
        Video video,
        AutoFilmOpenListObject obj,
        MediaInfo mediaInfo)
    {
        video.Path = AutoFilmRemotePath.FromOpenListPath(obj.Path);
        video.Size = obj.Size;
        video.RunTimeTicks = mediaInfo.RunTimeTicks;
        video.TotalBitrate = mediaInfo.Bitrate;
        video.Container = mediaInfo.Container;
        var videoStream = mediaInfo.MediaStreams.FirstOrDefault(
            stream => stream.Type == MediaStreamType.Video);
        video.Width = videoStream?.Width ?? 0;
        video.Height = videoStream?.Height ?? 0;
        video.DefaultVideoStreamIndex = videoStream?.Index;
        video.HasSubtitles = mediaInfo.MediaStreams.Any(
            stream => stream.Type == MediaStreamType.Subtitle);
    }

    private static void Restore(Video video, VideoSnapshot snapshot)
    {
        video.Path = snapshot.Path;
        video.Size = snapshot.Size;
        video.RunTimeTicks = snapshot.RunTimeTicks;
        video.TotalBitrate = snapshot.Bitrate;
        video.Container = snapshot.Container;
        video.Width = snapshot.Width;
        video.Height = snapshot.Height;
        video.DefaultVideoStreamIndex = snapshot.DefaultVideoStreamIndex;
        video.HasSubtitles = snapshot.HasSubtitles;
    }

    private static AutoFilmMediaReplacementFacts FactsFromVideo(
        Video video,
        IReadOnlyList<MediaStream> streams) =>
        new(
            video.Path,
            video.Size,
            video.RunTimeTicks,
            video.TotalBitrate,
            video.Container,
            video.Width,
            video.Height,
            CloneStreams(streams));

    private static AutoFilmMediaReplacementFacts FactsFromProbe(
        AutoFilmOpenListObject obj,
        MediaInfo mediaInfo)
    {
        var video = mediaInfo.MediaStreams.FirstOrDefault(
            stream => stream.Type == MediaStreamType.Video);
        return new AutoFilmMediaReplacementFacts(
            AutoFilmRemotePath.FromOpenListPath(obj.Path),
            obj.Size,
            mediaInfo.RunTimeTicks,
            mediaInfo.Bitrate,
            mediaInfo.Container,
            video?.Width,
            video?.Height,
            CloneStreams(mediaInfo.MediaStreams));
    }

    private static string NormalizePath(string path)
    {
        if (AutoFilmRemotePath.TryGetOpenListPath(path, out var openListPath))
        {
            return openListPath;
        }

        return AutoFilmRemotePath.TryGetOpenListPath(
            AutoFilmRemotePath.FromOpenListPath(path),
            out openListPath)
            ? openListPath
            : throw new ArgumentException("Path must be an OpenList absolute path.");
    }

    private static string GetOpenListPath(string remotePath) =>
        AutoFilmRemotePath.TryGetOpenListPath(remotePath, out var result)
            ? result
            : throw new InvalidOperationException("Stored path is not an OpenList URI.");

    private static void EnsureSameParent(string oldRemotePath, string newOpenListPath)
    {
        var oldOpenListPath = GetOpenListPath(oldRemotePath);
        if (!string.Equals(
                Path.GetDirectoryName(oldOpenListPath)?.Replace('\\', '/'),
                Path.GetDirectoryName(newOpenListPath)?.Replace('\\', '/'),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Replacement file must be in the existing media directory.");
        }
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _previews.Where(entry => entry.Value.ExpiresAt <= now))
        {
            _previews.TryRemove(entry.Key, out _);
        }

        foreach (var entry in _rollbacks.Where(entry => entry.Value.ExpiresAt <= now))
        {
            _rollbacks.TryRemove(entry.Key, out _);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _probeSlots.Dispose();
        foreach (var itemLock in _itemLocks.Values)
        {
            itemLock.Dispose();
        }
    }

    private sealed record ReplacementPlan(
        DateTimeOffset ExpiresAt,
        Guid ItemId,
        string ExpectedOldPath,
        AutoFilmOpenListObject ReplacementObject,
        MediaInfo MediaInfo);

    private sealed record RollbackPlan(
        DateTimeOffset ExpiresAt,
        Guid ItemId,
        string ExpectedCurrentPath,
        VideoSnapshot Previous);

    private sealed record VideoSnapshot(
        string Path,
        long? Size,
        long? RunTimeTicks,
        int? Bitrate,
        string? Container,
        int Width,
        int Height,
        int? DefaultVideoStreamIndex,
        bool HasSubtitles,
        IReadOnlyList<MediaStream> Streams);
}
