using K53Guru.Application.Features.Attempts.Commands.Start;
using K53Guru.Application.Features.Attempts.DTOs;
using K53Guru.Application.Features.Attempts.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace K53Guru.Server.UI.Controllers.Api.V1;

/// <summary>
/// Learner-facing attempt composition/resume (Epic 3, Story 3.3). Anonymous, rate-limited
/// (reuses the "learner-api" IP-based policy from Story 3.1 unchanged - no per-UUID partitioning
/// yet, deferred per spec-3-3-start-single-code-attempt.md). Mirrors SittingsController's shape:
/// thin, no business logic, every action delegates entirely to an Application-layer MediatR
/// command/query.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/v1/[controller]")]
[EnableRateLimiting("learner-api")]
public class AttemptsController : ControllerBase
{
    private readonly ISender _mediator;

    public AttemptsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Composes and freezes a new single-code attempt from a published, single-code Test's
    /// curated question pool.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AttemptDto>> StartAttempt([FromBody] StartAttemptCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Re-reads an already-composed attempt's frozen snapshot for resume. Requires the requesting
    /// learner's UUID to match the attempt's owner - a mismatch is indistinguishable from a
    /// nonexistent id.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AttemptDto>> GetAttempt(int id, [FromQuery] Guid learnerProfileId)
    {
        var result = await _mediator.Send(new GetAttemptQuery { AttemptId = id, LearnerProfileId = learnerProfileId });
        return result.Succeeded ? Ok(result.Data) : NotFound(result);
    }
}
