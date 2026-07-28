using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Accesses the authenticated AutoFilm API exposed by OpenList.
/// </summary>
public interface IAutoFilmOpenListClient
{
    /// <summary>
    /// Gets one object by path.
    /// </summary>
    /// <param name="path">OpenList path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The object, or <see langword="null"/> when it does not exist.</returns>
    Task<AutoFilmOpenListObject?> GetObjectAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Lists one complete directory.
    /// </summary>
    /// <param name="path">OpenList directory path.</param>
    /// <param name="refresh">Whether provider data should be refreshed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The directory children.</returns>
    Task<IReadOnlyList<AutoFilmOpenListObject>> ListObjectsAsync(
        string path,
        bool refresh,
        CancellationToken cancellationToken);

    /// <summary>
    /// Converts a signed OpenList download path to an absolute URI.
    /// </summary>
    /// <param name="obj">OpenList object.</param>
    /// <returns>The signed absolute URI.</returns>
    Uri GetDownloadUri(AutoFilmOpenListObject obj);

    /// <summary>
    /// Converts a signed OpenList download path to the container-internal URI.
    /// </summary>
    /// <param name="obj">OpenList object.</param>
    /// <returns>The signed internal URI used by server-side probing.</returns>
    Uri GetInternalDownloadUri(AutoFilmOpenListObject obj);

    /// <summary>
    /// Uploads a subtitle without exposing storage credentials.
    /// </summary>
    /// <param name="remotePath">Destination OpenList path.</param>
    /// <param name="localPath">Source local path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the upload.</returns>
    Task UploadFileAsync(
        string remotePath,
        string localPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uploads in-memory content without a local media directory.
    /// </summary>
    /// <param name="remotePath">Destination OpenList path.</param>
    /// <param name="content">Content to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the upload.</returns>
    Task UploadContentAsync(
        string remotePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a remote file or directory by its OpenList path.
    /// </summary>
    /// <param name="remotePath">OpenList absolute path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the deletion.</returns>
    Task DeletePathAsync(
        string remotePath,
        CancellationToken cancellationToken);
}
