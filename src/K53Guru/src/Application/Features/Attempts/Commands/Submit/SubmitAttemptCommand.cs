using K53Guru.Application.Features.Attempts.DTOs;
using K53Guru.Domain.Common;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Attempts.Commands.Submit;

/// <summary>
/// Records a learner's selected answers on an already-started Attempt, then grades it entirely
/// server-side (Story 3.5): for each constituent code (one for a single-code Attempt, one per
/// code for a combination - Story 3.4), computes a per-section correct count against the CURRENT
/// TestConfig/SectionRule.PassMark (read live at grading time, never snapshotted - Story 3.2's own
/// "scoring reads config live" AC) and derives that code's pass/fail (failing any one section
/// fails the code). Anonymous learner-facing command - no [RequestAuthorize], mirroring
/// StartAttemptCommand/GetAttemptQuery.
/// </summary>
public class SubmitAttemptCommand : IRequest<Result<GradedAttemptResultDto>>
{
    public int AttemptId { get; set; }

    /// <summary>
    /// The requesting learner's UUID. Must match the Attempt's owning LearnerProfileId - a
    /// mismatch is rejected identically to a nonexistent AttemptId (NotFoundException), exactly
    /// like GetAttemptQuery's resume ownership check.
    /// </summary>
    public Guid LearnerProfileId { get; set; }

    public List<SubmitAttemptAnswer> Answers { get; set; } = new();
}

/// <summary>
/// One submitted answer: which snapshotted AttemptQuestion it answers, and which of that
/// question's snapshotted AttemptAnswerOptions the learner selected.
/// </summary>
public class SubmitAttemptAnswer
{
    public int AttemptQuestionId { get; set; }

    public int SelectedAttemptAnswerOptionId { get; set; }
}

public class SubmitAttemptCommandHandler : IRequestHandler<SubmitAttemptCommand, Result<GradedAttemptResultDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public SubmitAttemptCommandHandler(IApplicationDbContextFactory dbContextFactory, IMapper mapper)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<GradedAttemptResultDto>> Handle(SubmitAttemptCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);

        // (1) Load the Attempt with the same ownership check as GetAttemptQuery - id AND owning
        // LearnerProfileId must both match, else NotFoundException, never leaking another
        // learner's attempt's existence.
        var attempt = await db.Attempts
            .Include(a => a.AttemptQuestions.OrderBy(q => q.DisplayOrder))
            .ThenInclude(q => q.AttemptAnswerOptions)
            .SingleOrDefaultAsync(
                a => a.Id == request.AttemptId && a.LearnerProfileId == request.LearnerProfileId,
                cancellationToken)
            ?? throw new NotFoundException($"Attempt with id: [{request.AttemptId}] not found.");

        // (2) Reject a second submission outright - no re-grading/re-submission path.
        if (attempt.SubmittedAt != null)
            return await Result<GradedAttemptResultDto>.FailureAsync("Attempt has already been submitted.");

        // (3) Reject outright if the submitted Answers contain more than one entry for the same
        // AttemptQuestionId - without this, a learner could submit both the correct AND an
        // incorrect option for the same question (both AttemptAnswerOptions end up
        // IsSelected = true), and grading's Any(o => o.IsSelected && o.IsCorrect) check would then
        // count the question as correct regardless of actual intent, guaranteeing every section
        // passes. Checked before touching IsSelected on anything or persisting anything.
        if (request.Answers.GroupBy(a => a.AttemptQuestionId).Any(g => g.Count() > 1))
            return await Result<GradedAttemptResultDto>.FailureAsync("Duplicate answer submitted for one or more questions.");

        // (4) Record the learner's selections. An AttemptQuestionId/SelectedAttemptAnswerOptionId
        // that doesn't belong to this attempt is silently ignored, not an error - and a question
        // with no matching answer in the submitted list is simply graded as incorrect below (its
        // AttemptAnswerOptions all remain IsSelected = false).
        var questionsById = attempt.AttemptQuestions.ToDictionary(q => q.Id);
        foreach (var answer in request.Answers)
        {
            if (!questionsById.TryGetValue(answer.AttemptQuestionId, out var question))
                continue;

            var option = question.AttemptAnswerOptions
                .SingleOrDefault(o => o.Id == answer.SelectedAttemptAnswerOptionId);
            if (option == null)
                continue;

            option.IsSelected = true;
        }

        // (5) Derive the attempt's constituent codes via the same shared helper StartAttemptCommand
        // uses - one code for a single-code Attempt, two for a combination (Story 3.4).
        var constituentCodes = attempt.Code.GetConstituentCodes();

        // (6) Grade each constituent code independently against its OWN current TestConfig -
        // a partial pass across codes is possible for a combination Attempt.
        var codeResults = new List<CodeResult>();
        foreach (var code in constituentCodes)
        {
            var config = await db.TestConfigs
                .Include(tc => tc.SectionRules)
                .SingleOrDefaultAsync(tc => tc.Code == code, cancellationToken);
            if (config == null)
                return await Result<GradedAttemptResultDto>.FailureAsync(
                    $"No test configuration found for code [{code}].");

            // AttemptQuestion.Code's dual meaning (Story 3.4): a shared Rules/Signs question
            // carries the FULL combination as its Code, a VehicleControls question carries only
            // its ONE constituent code - HasFlag correctly matches both cases against this code.
            var codeQuestions = attempt.AttemptQuestions.Where(q => q.Code.HasFlag(code)).ToList();

            var sectionResults = new List<SectionResult>();
            foreach (var sectionGroup in codeQuestions.GroupBy(q => q.Section))
            {
                var rule = config.SectionRules.SingleOrDefault(sr => sr.Section == sectionGroup.Key);
                if (rule == null)
                    return await Result<GradedAttemptResultDto>.FailureAsync(
                        $"Test configuration for code [{code}] is missing section rule(s) for: {sectionGroup.Key}.");

                var correctCount = sectionGroup.Count(
                    q => q.AttemptAnswerOptions.Any(o => o.IsSelected && o.IsCorrect));
                var sectionPassed = correctCount >= rule.PassMark;

                sectionResults.Add(new SectionResult
                {
                    Section = sectionGroup.Key,
                    CorrectCount = correctCount,
                    PassMark = rule.PassMark,
                    Passed = sectionPassed
                });
            }

            // Failing any one section fails the whole code; a code with no graded sections at all
            // (not reachable through a properly-composed Attempt) is conservatively not passed.
            var codePassed = sectionResults.Count > 0 && sectionResults.All(sr => sr.Passed);

            codeResults.Add(new CodeResult
            {
                AttemptId = attempt.Id,
                Code = code,
                Passed = codePassed,
                SectionResults = sectionResults
            });
        }

        // (7) Persist CodeResult/SectionResult, mark the Attempt submitted, save.
        db.CodeResults.AddRange(codeResults);
        attempt.SubmittedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two concurrent SubmitAttemptCommand calls for the same AttemptId can both observe
            // SubmittedAt == null at step (2) above and both grade + build a full CodeResult set;
            // the CodeResultConfiguration unique index on (AttemptId, Code) turns the loser's write
            // into a real, catchable unique-constraint violation here instead of silently
            // succeeding with duplicate grading results. Unlike StartAttemptCommand's
            // LearnerProfile race (which retries, since the loser's own work can still succeed once
            // the conflict is removed), by the time this conflict fires the OTHER concurrent
            // request has already fully graded and persisted the attempt - there is nothing left
            // for this call to do except report that it's already submitted, using the exact same
            // message as the existing "already submitted" rejection above.
            return await Result<GradedAttemptResultDto>.FailureAsync("Attempt has already been submitted.");
        }

        var dto = new GradedAttemptResultDto
        {
            AttemptId = attempt.Id,
            Passed = codeResults.All(cr => cr.Passed),
            CodeResults = _mapper.Map<List<CodeResultDto>>(codeResults)
        };

        return await Result<GradedAttemptResultDto>.SuccessAsync(dto);
    }
}
