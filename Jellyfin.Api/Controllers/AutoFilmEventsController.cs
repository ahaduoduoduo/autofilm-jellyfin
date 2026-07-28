using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.AutoFilm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Receives reliable path-only notifications from OpenList.
/// </summary>
[Route("AutoFilm/Events")]
[AllowAnonymous]
[Tags("AutoFilm")]
public sealed class AutoFilmEventsController : BaseJellyfinApiController
{
    private readonly IAutoFilmPathEventService _eventService;
    private readonly IAutoFilmInboundEventAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AutoFilmEventsController"/> class.
    /// </summary>
    /// <param name="eventService">Path event service.</param>
    /// <param name="authorizer">Inbound event authorizer.</param>
    public AutoFilmEventsController(
        IAutoFilmPathEventService eventService,
        IAutoFilmInboundEventAuthorizer authorizer)
    {
        _eventService = eventService;
        _authorizer = authorizer;
    }

    /// <summary>
    /// Applies one ordered OpenList event.
    /// </summary>
    /// <param name="eventItem">Path-only event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The local action.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AutoFilmPathEventResult>> Receive(
        [FromBody] AutoFilmPathEvent eventItem,
        CancellationToken cancellationToken)
    {
        if (!_authorizer.IsConfigured)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "AutoFilm inbound event token is not configured." });
        }

        if (!Request.Headers.TryGetValue(
                "X-AutoFilm-Token",
                out var suppliedToken)
            || !_authorizer.Validate(suppliedToken.ToString()))
        {
            return Unauthorized();
        }

        try
        {
            return await _eventService.ApplyAsync(
                eventItem,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
    }
}
