using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Resolves external subtitles from OpenList and lazily migrates legacy files.
/// </summary>
public sealed class AutoFilmSubtitleService : IAutoFilmSubtitleService, IDisposable
{
    private static readonly string[] SupportedExtensions =
        [".ass", ".idx", ".srt", ".ssa", ".sub", ".sup", ".vtt"];

    private readonly IAutoFilmOpenListClient _openListClient;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly AutoFilmOptions _options;
    private readonly ILogger<AutoFilmSubtitleService> _logger;
    private readonly ConcurrentDictionary<string, byte> _pendingUploads =
        new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _uploadGate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmSubtitleService"/> class.
    /// </summary>
    /// <param name="openListClient">AutoFilm OpenList client.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    /// <param name="options">AutoFilm configuration.</param>
    /// <param name="logger">Logger.</param>
    public AutoFilmSubtitleService(
        IAutoFilmOpenListClient openListClient,
        ILibraryManager libraryManager,
        IMediaStreamRepository mediaStreamRepository,
        AutoFilmOptions options,
        ILogger<AutoFilmSubtitleService> logger)
    {
        _openListClient = openListClient;
        _libraryManager = libraryManager;
        _mediaStreamRepository = mediaStreamRepository;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _uploadGate.Dispose();
    }

    /// <inheritdoc />
    public async Task<AutoFilmSubtitleResolution?> ResolveAsync(
        Guid itemId,
        int streamIndex,
        string requestedFormat,
        CancellationToken cancellationToken)
    {
        var streams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = itemId });
        var stream = streams.FirstOrDefault(
            candidate => candidate.Type == MediaStreamType.Subtitle
                && candidate.IsExternal
                && candidate.Index == streamIndex);
        if (stream is null || string.IsNullOrWhiteSpace(stream.Path))
        {
            return null;
        }

        if (!AutoFilmRemotePath.TryGetOpenListPath(
                stream.Path,
                out var remotePath)
            || !IsSupportedSubtitle(remotePath))
        {
            if (IsRawSupRequest(stream, requestedFormat)
                && File.Exists(stream.Path))
            {
                return new AutoFilmSubtitleResolution(
                    "local",
                    null,
                    stream.Path,
                    false);
            }

            return null;
        }

        var localPath = _options.MapRemoteToLocal(stream.Path);
        try
        {
            var remoteObject = await _openListClient.GetObjectAsync(
                remotePath,
                cancellationToken).ConfigureAwait(false);
            if (remoteObject is not null && !remoteObject.IsDirectory)
            {
                return new AutoFilmSubtitleResolution(
                    "openlist",
                    _openListClient.GetDownloadUri(remoteObject),
                    null,
                    false);
            }
        }
        catch (Exception ex)
        {
            // Authentication and provider failures are not proof that the
            // subtitle disappeared. Preserve the record and use the local
            // fallback when it is available.
            _logger.LogWarning(
                ex,
                "AutoFilm could not verify subtitle {ItemId}/{StreamIndex}",
                itemId,
                streamIndex);
            return localPath is not null && File.Exists(localPath)
                ? new AutoFilmSubtitleResolution("legacy", null, localPath, false)
                : null;
        }

        if (localPath is not null && File.Exists(localPath))
        {
            QueueUpload(itemId, streamIndex, localPath, remotePath);
            return new AutoFilmSubtitleResolution("legacy", null, localPath, false);
        }

        var remainingStreams = streams
            .Where(candidate => candidate.Type != MediaStreamType.Subtitle
                || candidate.Index != streamIndex)
            .ToArray();
        _mediaStreamRepository.SaveMediaStreams(
            itemId,
            remainingStreams,
            cancellationToken);
        _logger.LogInformation(
            "AutoFilm removed missing subtitle record {ItemId}/{StreamIndex}",
            itemId,
            streamIndex);
        return new AutoFilmSubtitleResolution("missing", null, null, true);
    }

    private static bool IsRawSupRequest(
        MediaStream stream,
        string requestedFormat)
    {
        var normalizedFormat = requestedFormat.Trim().TrimStart('.');
        return stream.Path.EndsWith(".sup", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(
                    normalizedFormat,
                    "sup",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    normalizedFormat,
                    "pgssub",
                    StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<AutoFilmSubtitleResolution?> UploadAsync(
        Guid itemId,
        string format,
        string language,
        bool isForced,
        bool isHearingImpaired,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById<BaseItem>(itemId);
        if (item is null
            || !AutoFilmRemotePath.TryGetOpenListPath(
                item.Path,
                out var mediaPath))
        {
            return null;
        }

        var normalizedFormat = format.Trim().TrimStart('.').ToLowerInvariant();
        var extension = "." + normalizedFormat;
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unsupported subtitle format: '{format}'.", nameof(format));
        }

        var normalizedLanguage = language.Trim().ToLowerInvariant();
        if (normalizedLanguage.Length == 0
            || normalizedLanguage is "." or ".."
            || normalizedLanguage.AsSpan().IndexOfAny('/', '\\') >= 0)
        {
            throw new ArgumentException("Subtitle language contains invalid characters.", nameof(language));
        }

        var slash = mediaPath.LastIndexOf('/');
        var directory = slash <= 0
            ? "/"
            : mediaPath[..slash];
        var name = Path.GetFileNameWithoutExtension(mediaPath)
            + "."
            + normalizedLanguage
            + (isForced ? ".forced" : string.Empty)
            + (isHearingImpaired ? ".sdh" : string.Empty)
            + extension;
        var remotePath = directory == "/"
            ? "/" + name
            : directory + "/" + name;

        await _uploadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _openListClient.UploadContentAsync(
                remotePath,
                content,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _uploadGate.Release();
        }

        var remoteObject = await _openListClient.GetObjectAsync(
            remotePath,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Uploaded subtitle was not returned by OpenList.");
        var streams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = itemId });
        var streamIndex = streams.Count == 0
            ? 0
            : streams.Max(stream => stream.Index) + 1;
        var subtitleStream = new MediaStream
        {
            Type = MediaStreamType.Subtitle,
            Index = streamIndex,
            Codec = normalizedFormat,
            Language = normalizedLanguage,
            IsForced = isForced,
            IsHearingImpaired = isHearingImpaired,
            IsExternal = true,
            IsExternalUrl = true,
            SupportsExternalStream = true,
            DeliveryMethod = SubtitleDeliveryMethod.External,
            Path = AutoFilmRemotePath.FromOpenListPath(remotePath)
        };
        _mediaStreamRepository.SaveMediaStreams(
            itemId,
            [.. streams, subtitleStream],
            cancellationToken);
        return new AutoFilmSubtitleResolution(
            "openlist",
            _openListClient.GetDownloadUri(remoteObject),
            null,
            false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        Guid itemId,
        int streamIndex,
        CancellationToken cancellationToken)
    {
        var streams = _mediaStreamRepository.GetMediaStreams(
            new MediaStreamQuery { ItemId = itemId });
        var stream = streams.FirstOrDefault(
            candidate => candidate.Type == MediaStreamType.Subtitle
                && candidate.IsExternal
                && candidate.Index == streamIndex);
        if (stream is null
            || !AutoFilmRemotePath.TryGetOpenListPath(
                stream.Path,
                out var remotePath))
        {
            return false;
        }

        await _openListClient.DeletePathAsync(
            remotePath,
            cancellationToken).ConfigureAwait(false);
        _mediaStreamRepository.SaveMediaStreams(
            itemId,
            streams.Where(candidate => candidate != stream).ToArray(),
            cancellationToken);
        return true;
    }

    private void QueueUpload(
        Guid itemId,
        int streamIndex,
        string localPath,
        string remotePath)
    {
        var key = itemId.ToString("N") + ":" + streamIndex;
        if (!_pendingUploads.TryAdd(key, 0))
        {
            return;
        }

        _ = UploadLegacySubtitleAsync(
            key,
            itemId,
            streamIndex,
            localPath,
            remotePath);
    }

    private async Task UploadLegacySubtitleAsync(
        string key,
        Guid itemId,
        int streamIndex,
        string localPath,
        string remotePath)
    {
        await _uploadGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (!File.Exists(localPath))
            {
                return;
            }

            await _openListClient.UploadFileAsync(
                remotePath,
                localPath,
                CancellationToken.None).ConfigureAwait(false);
            var remoteObject = await _openListClient.GetObjectAsync(
                remotePath,
                CancellationToken.None).ConfigureAwait(false);
            if (remoteObject is not null)
            {
                _logger.LogInformation(
                    "AutoFilm migrated subtitle {ItemId}/{StreamIndex} to OpenList",
                    itemId,
                    streamIndex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "AutoFilm subtitle migration failed for {ItemId}/{StreamIndex}",
                itemId,
                streamIndex);
        }
        finally
        {
            _uploadGate.Release();
            _pendingUploads.TryRemove(key, out _);
        }
    }

    private static bool IsSupportedSubtitle(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
