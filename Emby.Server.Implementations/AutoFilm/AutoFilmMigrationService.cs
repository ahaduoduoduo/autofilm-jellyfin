using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.AutoFilm;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.AutoFilm;

/// <summary>
/// Rewrites legacy paths in Jellyfin's existing library configuration,
/// items, and stream records without reading OpenList.
/// </summary>
public sealed class AutoFilmMigrationService : IAutoFilmMigrationService
{
    private static readonly BaseItemKind[] MigratedItemTypes =
    [
        BaseItemKind.Folder,
        BaseItemKind.Series,
        BaseItemKind.Season,
        BaseItemKind.Movie,
        BaseItemKind.Episode,
        BaseItemKind.Video
    ];

    private readonly ILibraryManager _libraryManager;
    private readonly IMediaStreamRepository _mediaStreamRepository;
    private readonly AutoFilmOptions _options;
    private readonly ILogger<AutoFilmMigrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmMigrationService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="mediaStreamRepository">Jellyfin media stream repository.</param>
    /// <param name="options">AutoFilm configuration.</param>
    /// <param name="logger">Logger.</param>
    public AutoFilmMigrationService(
        ILibraryManager libraryManager,
        IMediaStreamRepository mediaStreamRepository,
        AutoFilmOptions options,
        ILogger<AutoFilmMigrationService> logger)
    {
        _libraryManager = libraryManager;
        _mediaStreamRepository = mediaStreamRepository;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AutoFilmMigrationResult> RunAsync(
        bool apply,
        int limit,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, 10000);
        var entries = new List<AutoFilmMigrationEntry>();
        var itemsMigrated = 0;
        var subtitlePathsMigrated = 0;
        var subtitleCodecsNormalized = 0;
        var failed = 0;

        // All item and stream work below is local database work. In particular,
        // migration does not verify objects or probe media through OpenList.
        var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = MigratedItemTypes,
                Recursive = true
            })
            .Where(item => _options.MapLegacyToRemoteUri(item.Path) is not null
                || AutoFilmRemotePath.IsRemote(item.Path))
            .OrderBy(GetMigrationOrder)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.Count >= boundedLimit)
            {
                break;
            }

            var previousPath = item.Path;
            var migratedPath = _options.MapLegacyToRemoteUri(previousPath)
                ?? previousPath;
            var pathChanged = !string.Equals(
                previousPath,
                migratedPath,
                StringComparison.Ordinal);
            var streams = _mediaStreamRepository.GetMediaStreams(
                new MediaStreamQuery { ItemId = item.Id });
            var changedSubtitlePaths = 0;
            var changedSubtitleCodecs = 0;

            foreach (var stream in streams)
            {
                if (stream.Type != MediaStreamType.Subtitle
                    || !stream.IsExternal
                    || string.IsNullOrWhiteSpace(stream.Path))
                {
                    continue;
                }

                var remoteSubtitlePath = _options.MapLegacyToRemoteUri(stream.Path);
                if (remoteSubtitlePath is not null)
                {
                    changedSubtitlePaths++;
                    if (apply)
                    {
                        stream.Path = remoteSubtitlePath;
                        stream.IsExternalUrl = true;
                        stream.SupportsExternalStream = true;
                    }
                }

                if (AutoFilmSubtitleCompatibility.RequiresExternalSupCodecNormalization(
                        stream))
                {
                    changedSubtitleCodecs++;
                    if (apply)
                    {
                        AutoFilmSubtitleCompatibility.NormalizeExternalSup(stream);
                    }
                }
            }

            if (!pathChanged
                && changedSubtitlePaths == 0
                && changedSubtitleCodecs == 0)
            {
                continue;
            }

            try
            {
                if (apply)
                {
                    if (changedSubtitlePaths > 0 || changedSubtitleCodecs > 0)
                    {
                        _mediaStreamRepository.SaveMediaStreams(
                            item.Id,
                            streams,
                            cancellationToken);
                    }

                    if (pathChanged)
                    {
                        item.Path = migratedPath;
                        await item.UpdateToRepositoryAsync(
                            ItemUpdateType.MetadataEdit,
                            cancellationToken).ConfigureAwait(false);
                    }
                }

                if (pathChanged)
                {
                    itemsMigrated++;
                }

                subtitlePathsMigrated += changedSubtitlePaths;
                subtitleCodecsNormalized += changedSubtitleCodecs;
                entries.Add(new AutoFilmMigrationEntry(
                    item.Id,
                    item.Name,
                    previousPath,
                    migratedPath,
                    apply ? "migrated" : "would_migrate",
                    changedSubtitlePaths,
                    changedSubtitleCodecs,
                    null));
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(
                    ex,
                    "AutoFilm local migration failed for item {ItemId}",
                    item.Id);
                entries.Add(new AutoFilmMigrationEntry(
                    item.Id,
                    item.Name,
                    previousPath,
                    migratedPath,
                    "failed",
                    0,
                    0,
                    ex.Message));
            }
        }

        var hasRemainingLegacyItemPaths = apply
            && _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = MigratedItemTypes,
                    Recursive = true
                })
                .Any(item => _options.MapLegacyToRemoteUri(item.Path) is not null);
        var libraryPaths = MigrateLibraryPaths(
            apply,
            hasRemainingLegacyItemPaths,
            cancellationToken);
        failed += libraryPaths.Count(entry => entry.State == "failed");
        var libraryPathsMigrated = libraryPaths.Count(
            entry => entry.State is "migrated" or "would_migrate");

        return new AutoFilmMigrationResult(
            apply,
            entries.Count,
            itemsMigrated,
            subtitlePathsMigrated,
            subtitleCodecsNormalized,
            libraryPathsMigrated,
            failed,
            entries,
            libraryPaths);
    }

    private IReadOnlyList<AutoFilmLibraryPathMigrationEntry> MigrateLibraryPaths(
        bool apply,
        bool deferred,
        CancellationToken cancellationToken)
    {
        var candidates = _libraryManager.GetVirtualFolders(true)
            .SelectMany(folder => folder.Locations
                .Select(path => new
                {
                    folder.Name,
                    PreviousPath = path,
                    MigratedPath = _options.MapLegacyToRemoteUri(path),
                    ExistingLocations = folder.Locations
                }))
            .Where(candidate => candidate.MigratedPath is not null)
            .OrderBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.PreviousPath, StringComparer.Ordinal)
            .ToArray();
        var results = new List<AutoFilmLibraryPathMigrationEntry>(
            candidates.Length);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (apply && !deferred)
                {
                    if (!candidate.ExistingLocations.Contains(
                            candidate.MigratedPath!,
                            StringComparer.Ordinal))
                    {
                        // Add the new root before removing the legacy root.
                        // A failure therefore leaves at least one usable source.
                        _libraryManager.AddMediaPath(
                            candidate.Name,
                            new MediaPathInfo(candidate.MigratedPath!)
                            {
                                SourceType = MediaPathSourceType.OpenList
                            });
                    }

                    _libraryManager.RemoveMediaPath(
                        candidate.Name,
                        candidate.PreviousPath);
                }

                results.Add(new AutoFilmLibraryPathMigrationEntry(
                    candidate.Name,
                    candidate.PreviousPath,
                    candidate.MigratedPath!,
                    deferred
                        ? "deferred"
                        : apply
                            ? "migrated"
                            : "would_migrate",
                    null));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AutoFilm library path migration failed for {LibraryName}: {Path}",
                    candidate.Name,
                    candidate.PreviousPath);
                results.Add(new AutoFilmLibraryPathMigrationEntry(
                    candidate.Name,
                    candidate.PreviousPath,
                    candidate.MigratedPath!,
                    "failed",
                    ex.Message));
            }
        }

        return results;
    }

    private static int GetMigrationOrder(BaseItem item)
    {
        return item switch
        {
            Folder => 0,
            MediaBrowser.Controller.Entities.Movies.Movie => 1,
            MediaBrowser.Controller.Entities.TV.Episode => 2,
            _ => 3
        };
    }
}
