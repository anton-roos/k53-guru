using K53Guru.Application.Features.Attempts.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Attempts.Commands.CheckAnswer;

/// <summary>
/// Practice-mode-only immediate per-question feedback (Story 3.6): records the learner's
/// selected option on a snapshotted AttemptQuestion - explicitly RE-settable on every call (this
/// IS what "permits retry" means, unlike SubmitAttemptCommand's one-shot answer recording) - and
/// returns whether it was correct, which option actually is correct, and that question's
/// snapshotted explanation. Never persists a CodeResult/SectionResult; that stays
/// SubmitAttemptCommand's job alone. Anonymous learner-facing command - no [RequestAuthorize],
/// mirroring StartAttemptCommand/GetAttemptQuery/SubmitAttemptCommand.
/// </summary>
public class CheckAnswerCommand : IRequest<Result<CheckAnswerResultDto>>
{
    public int AttemptId { get; set; }

    /// <summary>
    /// The requesting learner's UUID. Must match the Attempt's owning LearnerProfileId - a
    /// mismatch is rejected identically to a nonexistent AttemptId (NotFoundException), exactly
    /// like GetAttemptQuery's/SubmitAttemptCommand's own ownership check.
    /// </summary>
    public Guid LearnerProfileId { get; set; }

    public int AttemptQuestionId { get; set; }

    public int SelectedAttemptAnswerOptionId { get; set; }
}

public class CheckAnswerCommandHandler : IRequestHandler<CheckAnswerCommand, Result<CheckAnswerResultDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;

    public CheckAnswerCommandHandler(IApplicationDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<CheckAnswerResultDto>> Handle(CheckAnswerCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);

        // (1) Same ownership check as GetAttemptQuery/SubmitAttemptCommand - id AND owning
        // LearnerProfileId must both match, else NotFoundException, never leaking another
        // learner's attempt's existence.
        var attempt = await db.Attempts
            .Include(a => a.AttemptQuestions)
            .ThenInclude(q => q.AttemptAnswerOptions)
            .SingleOrDefaultAsync(
                a => a.Id == request.AttemptId && a.LearnerProfileId == request.LearnerProfileId,
                cancellationToken)
            ?? throw new NotFoundException($"Attempt with id: [{request.AttemptId}] not found.");

        // (2) Test mode's confidentiality must never be bypassable through this endpoint.
        if (attempt.Mode != AttemptMode.Practice)
            return await Result<CheckAnswerResultDto>.FailureAsync("check-answer is only available in Practice mode.");

        // (3) No answering after the attempt is done - matches SubmitAttemptCommand's own
        // "no re-submission" rule.
        if (attempt.SubmittedAt != null)
            return await Result<CheckAnswerResultDto>.FailureAsync("Attempt has already been submitted.");

        // (4) Resolve the target AttemptQuestion/AttemptAnswerOption - a 404-equivalent
        // Result.Failure if either doesn't belong to this attempt.
        var question = attempt.AttemptQuestions.SingleOrDefault(q => q.Id == request.AttemptQuestionId);
        if (question == null)
            return await Result<CheckAnswerResultDto>.FailureAsync(
                $"AttemptQuestion with id: [{request.AttemptQuestionId}] not found on this attempt.");

        var selectedOption = question.AttemptAnswerOptions
            .SingleOrDefault(o => o.Id == request.SelectedAttemptAnswerOptionId);
        if (selectedOption == null)
            return await Result<CheckAnswerResultDto>.FailureAsync(
                $"AttemptAnswerOption with id: [{request.SelectedAttemptAnswerOptionId}] not found on this question.");

        // (5) Clear IsSelected on every option for this question, then set the newly-selected
        // one - explicitly re-settable on every call, not a one-shot lock like
        // SubmitAttemptCommand's answers. This IS what "permits retry" means.
        foreach (var option in question.AttemptAnswerOptions)
        {
            option.IsSelected = false;
        }
        selectedOption.IsSelected = true;

        // (5b) Force EF to emit an UPDATE for every one of this question's options regardless of
        // whether the tracked value appears unchanged from what THIS DbContext instance originally
        // loaded. Without this, two near-simultaneous CheckAnswerCommand calls for the same
        // AttemptQuestionId can each load the question while every sibling is still
        // IsSelected = false, so each "clear" step is a no-op from that context's perspective and
        // SaveChangesAsync only issues an UPDATE for the option being set to true - never touching
        // the sibling's row. If both commit, the database ends up with two options marked
        // IsSelected = true for the same question. Marking every option's IsSelected property as
        // modified makes each call unconditionally rewrite all sibling rows, so under a race the
        // last write to commit fully wins.
        foreach (var option in question.AttemptAnswerOptions)
        {
            db.ChangeTracker.Entries<AttemptAnswerOption>()
                .Single(e => e.Entity.Id == option.Id)
                .Property(o => o.IsSelected).IsModified = true;
        }

        await db.SaveChangesAsync(cancellationToken);

        // (6) Reveal the correct option and explanation immediately, by design (Practice mode's
        // whole point). Never persists a CodeResult/SectionResult - that's still only
        // SubmitAttemptCommand's job, callable separately if/when a practice session wants an
        // overall score.
        var correctOption = question.AttemptAnswerOptions.Single(o => o.IsCorrect);
        var dto = new CheckAnswerResultDto
        {
            IsCorrect = selectedOption.IsCorrect,
            CorrectAttemptAnswerOptionId = correctOption.Id,
            Explanation = question.Explanation
        };

        return await Result<CheckAnswerResultDto>.SuccessAsync(dto);
    }
}
