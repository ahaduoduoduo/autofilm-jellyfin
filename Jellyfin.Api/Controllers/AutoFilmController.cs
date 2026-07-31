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
    private readonly IAutoFilmMediaReplacementService _mediaReplacementService;
    private readonly IAutoFilmOpenListClient _openListClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFilmController"/> class.
    /// </summary>
    /// <param name="migrationService">AutoFilm migration service.</param>
    /// <param name="remoteRefreshService">Precise remote refresh service.</param>
    /// <param name="mediaReplacementService">In-place remote media replacement service.</param>
    /// <param name="openListClient">Authenticated OpenList client.</param>
    public AutoFilmController(
        IAutoFilmMigrationService migrationService,
        IAutoFilmRemoteRefreshService remoteRefreshService,
        IAutoFilmMediaReplacementService mediaReplacementService,
        IAutoFilmOpenListClient openListClient)
    {
        _migrationService = migrationService;
        _remoteRefreshService = remoteRefreshService;
        _mediaReplacementService = mediaReplacementService;
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

    /// <summary>Finds remote videos without creating Jellyfin items.</summary>
    [HttpPost("MediaReplacement/Inspect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutoFilmMediaReplacementInspectResult>> InspectReplacement(
        [FromBody, Required] AutoFilmMediaReplacementInspectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaReplacementService.InspectAsync(
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

    /// <summary>Probes an exact remote replacement file.</summary>
    [HttpPost("MediaReplacement/Preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutoFilmMediaReplacementPreview>> PreviewReplacement(
        [FromBody, Required] AutoFilmMediaReplacementPreviewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaReplacementService.PreviewAsync(
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

    /// <summary>Changes the media path and streams of an existing item.</summary>
    [HttpPost("MediaReplacement/Apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutoFilmMediaReplacementResult>> ApplyReplacement(
        [FromBody, Required] AutoFilmMediaReplacementApplyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaReplacementService.ApplyAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Restores the previous media snapshot for an existing item.</summary>
    [HttpPost("MediaReplacement/Rollback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutoFilmMediaReplacementResult>> RollbackReplacement(
        [FromBody, Required] AutoFilmMediaReplacementRollbackRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _mediaReplacementService.RollbackAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
