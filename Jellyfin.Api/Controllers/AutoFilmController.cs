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
/// Admin endpoints for AutoFilm path migration.
/// </summary>
[Route("AutoFilm")]
[Authorize(Policy = Policies.RequiresElevation)]
[Tags("AutoFilm")]
public sealed class AutoFilmController : BaseJellyfinApiController
{
    private readonly IAutoFilmMigrationService _migrationService;
    private readonly IAutoFilmRemoteRefreshService _remoteRefreshService;
    private readonly IAutoFilmOpenListClient _openListClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmController"/> class.
    /// </summary>
    /// <param name="migrationService">AutoFilm migration service.</param>
    /// <param name="remoteRefreshService">Precise remote refresh service.</param>
    /// <param name="openListClient">Authenticated OpenList client.</param>
    public AutoFilmController(
        IAutoFilmMigrationService migrationService,
        IAutoFilmRemoteRefreshService remoteRefreshService,
        IAutoFilmOpenListClient openListClient)
    {
        _migrationService = migrationService;
        _remoteRefreshService = remoteRefreshService;
        _openListClient = openListClient;
    }

    /// <summary>
    /// Previews local Jellyfin path rewrites without OpenList requests.
    /// </summary>
    /// <param name="limit">Maximum number of candidate items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration preview.</returns>
    [HttpPost("Migration/Preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<AutoFilmMigrationResult> PreviewMigration(
        [FromQuery, Range(1, 10000)] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _migrationService.RunAsync(false, limit, cancellationToken);
    }

    /// <summary>
    /// Rewrites existing item and subtitle paths without OpenList requests.
    /// </summary>
    /// <param name="limit">Maximum number of candidate items.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The applied migration result.</returns>
    [HttpPost("Migration/Apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<AutoFilmMigrationResult> ApplyMigration(
        [FromQuery, Range(1, 10000)] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return _migrationService.RunAsync(true, limit, cancellationToken);
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
