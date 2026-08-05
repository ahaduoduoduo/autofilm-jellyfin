using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Result of one precise remote refresh.
/// </summary>
public sealed record AutoFilmRemoteRefreshResult(
    string Action,
    string ScanMode,
    Guid ItemId,
    string ItemName,
    string ItemType,
    string Path,
    int DirectoriesRead,
    int ObjectsRead,
    int RemovedItems,
    int ReclassifiedItems);
