using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.AutoFilm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Administrator endpoints for AutoFilm remote media.
/// </summary>
[Route("AutoFilm")]
[Authorize(Policy = Policies.RequiresElevation)]
[Tags("AutoFilm")]
public sealed class AutoFilmController : BaseJellyfinApiController
{
    private readonly IAutoFilmRemoteRefreshService _remoteRefreshService;
    private readonly IAutoFilmOpenListClient _openListClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmController"/> class.
    /// </summary>
    /// <param name="remoteRefreshService">Precise remote refresh service.</param>
    /// <param name="openListClient">Authenticated OpenList client.</param>
    public AutoFilmController(
        IAutoFilmRemoteRefreshService remoteRefreshService,
        IAutoFilmOpenListClient openListClient)
    {
        _remoteRefreshService = remoteRefreshService;
        _openListClient = openListClient;
    }

    /// <summary>
    /// Lists an OpenList directory for a Jellyfin library source picker.
    /// </summary>
    /// <param name="request">Directory and refresh behavior.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OpenList directory children.</returns>
    [HttpPost("OpenList/Browse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AutoFilmOpenListObject>>> BrowseOpenList(
        [FromBody, Required] AutoFilmOpenListBrowseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var openListPath = AutoFilmRemotePath.TryGetOpenListPath(
                request.Path,
                out var parsed)
                ? parsed
                : AutoFilmRemotePath.TryGetOpenListPath(
                    AutoFilmRemotePath.FromOpenListPath(request.Path),
                    out parsed)
                    ? parsed
                    : throw new ArgumentException(
                        "Path must be an OpenList absolute path or OpenList URI.",
                        nameof(request));
            var objects = await _openListClient.ListObjectsAsync(
                openListPath,
                request.Refresh,
                cancellationToken).ConfigureAwait(false);
            return Ok(objects);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Creates or refreshes one OpenList path with Jellyfin's normal resolvers.
    /// </summary>
    /// <param name="request">Path and optional metadata hints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or refreshed item.</returns>
    [HttpPost("RemoteRefresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutoFilmRemoteRefreshResult>> RemoteRefresh(
        [FromBody, Required] AutoFilmRemoteRefreshRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _remoteRefreshService.RefreshAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
