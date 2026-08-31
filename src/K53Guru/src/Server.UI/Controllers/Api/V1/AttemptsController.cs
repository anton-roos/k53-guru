using K53Guru.Application.Features.Attempts.Commands.Start;
using K53Guru.Application.Features.Attempts.Commands.Submit;
using K53Guru.Application.Features.Attempts.DTOs;
using K53Guru.Application.Features.Attempts.Queries.GetById;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace K53Guru.Server.UI.Controllers.Api.V1;

/// <summary>
/// Learner-facing attempt composition/resume/submit (Epic 3, Stories 3.3-3.5). Anonymous,
/// rate-limited (reuses the "learner-api" IP-based policy from Story 3.1 unchanged - no per-UUID
/// partitioning yet, deferred per spec-3-3-start-single-code-attempt.md). Mirrors
/// SittingsController's shape: thin, no business logic, every action delegates entirely to an
/// Application-layer MediatR command/query.
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

    /// <summary>
    /// Records the learner's selected answers on an already-started attempt and grades it entirely
    /// server-side, returning the versioned per-code/per-section result. Rejects a second
    /// submission against an already-submitted attempt outright - no re-grading.
    /// </summary>
    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<GradedAttemptResultDto>> SubmitAttempt(int id, [FromBody] SubmitAttemptCommand command)
    {
        command.AttemptId = id;
        var result = await _mediator.Send(command);
        if (result.Succeeded)
            return Ok(result.Data);

        // The not-found/wrong-learner case (the same NotFoundException GetAttemptQuery's ownership
        // check throws, formatted identically: "Attempt with id: [{id}] not found.") must return
        // NotFound, exactly like GetAttempt's equivalent failure - "identical to Story 3.3's resume
        // behavior" per spec. Every other failure (already submitted, duplicate answer, the
        // concurrent-double-submit race) returns BadRequest.
        var isNotFound = result.Errors.Any(m => m.Contains("not found", StringComparison.OrdinalIgnoreCase));
        return isNotFound ? NotFound(result) : BadRequest(result);
    }
}
