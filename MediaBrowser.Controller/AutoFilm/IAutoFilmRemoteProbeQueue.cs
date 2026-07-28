using System;

namespace MediaBrowser.Controller.AutoFilm;

/// <summary>
/// Serializes remote media probes to protect the storage account.
/// </summary>
public interface IAutoFilmRemoteProbeQueue
{
    /// <summary>
    /// Queues a remote video probe.
    /// </summary>
    /// <param name="itemId">Jellyfin video identifier.</param>
    /// <param name="force">Whether existing embedded stream data may be replaced.</param>
    void Enqueue(Guid itemId, bool force);
}
