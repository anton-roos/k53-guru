using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Application.Features.Tests.Queries.AvailableSittings;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace K53Guru.Server.UI.Controllers.Api.V1;

/// <summary>
/// The solution's first learner-facing API surface (Epic 3, Story 3.1). Anonymous, cacheable,
/// rate-limited discovery of published sittings. Contains no business logic - every action
/// delegates entirely to an Application-layer MediatR query.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/[controller]")]
[EnableRateLimiting("learner-api")]
public class SittingsController : ControllerBase
{
    private readonly ISender _mediator;

    public SittingsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the currently published, servable sittings (single-code or valid Code1+2/Code1+3
    /// combinations). Anonymous - no credentials required.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AvailableSittingDto>>> GetAvailableSittings()
    {
        return await _mediator.Send(new GetAvailableSittingsQuery());
    }
}
