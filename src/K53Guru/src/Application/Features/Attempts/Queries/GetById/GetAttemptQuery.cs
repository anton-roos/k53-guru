using K53Guru.Application.Features.Attempts.DTOs;

namespace K53Guru.Application.Features.Attempts.Queries.GetById;

/// <summary>
/// Re-reads an already-composed Attempt's frozen snapshot for resume - see
/// spec-3-3-start-single-code-attempt.md. Never re-randomises; only re-reads the persisted
/// DisplayOrder, so a resume returns identical order/content by construction. Anonymous
/// learner-facing query - no [RequestAuthorize].
/// </summary>
public class GetAttemptQuery : IRequest<Result<AttemptDto>>
{
    public int AttemptId { get; set; }

    /// <summary>
    /// The requesting learner's UUID. Must match the Attempt's owning LearnerProfileId - a
    /// mismatch is rejected identically to a nonexistent AttemptId, never leaking another
    /// learner's attempt's existence.
    /// </summary>
    public Guid LearnerProfileId { get; set; }
}

public class GetAttemptQueryHandler : IRequestHandler<GetAttemptQuery, Result<AttemptDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public GetAttemptQueryHandler(IApplicationDbContextFactory dbContextFactory, IMapper mapper)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<AttemptDto>> Handle(GetAttemptQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);

        // Both the id AND the owning LearnerProfileId must match - a wrong learner gets the exact
        // same NotFoundException as a nonexistent id, never leaking another learner's attempt's
        // existence.
        var attempt = await db.Attempts
            .Include(a => a.AttemptQuestions.OrderBy(q => q.DisplayOrder))
            .ThenInclude(q => q.AttemptAnswerOptions.OrderBy(o => o.Order))
            .SingleOrDefaultAsync(
                a => a.Id == request.AttemptId && a.LearnerProfileId == request.LearnerProfileId,
                cancellationToken)
            ?? throw new NotFoundException($"Attempt with id: [{request.AttemptId}] not found.");

        return await Result<AttemptDto>.SuccessAsync(_mapper.Map<AttemptDto>(attempt));
    }
}
