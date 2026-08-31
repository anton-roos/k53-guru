using K53Guru.Application.Features.Attempts.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Attempts.Commands.Start;

/// <summary>
/// Composes and freezes a new single-code Attempt for a learner against a published, single-code
/// Test - see spec-3-3-start-single-code-attempt.md. Anonymous learner-facing command - no
/// [RequestAuthorize], mirroring GetAvailableSittingsQuery/Story 3.1.
/// </summary>
public class StartAttemptCommand : IRequest<Result<AttemptDto>>
{
    /// <summary>
    /// Client-supplied UUID identifying the learner. Not seen before -&gt; a new LearnerProfile row
    /// is created alongside the Attempt (this command IS the profile's first write).
    /// </summary>
    public Guid LearnerProfileId { get; set; }

    public int TestId { get; set; }
}

public class StartAttemptCommandHandler : IRequestHandler<StartAttemptCommand, Result<AttemptDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public StartAttemptCommandHandler(IApplicationDbContextFactory dbContextFactory, IMapper mapper)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<AttemptDto>> Handle(StartAttemptCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);

        // (1) Load the Test and reject if missing, not published, or a combination sitting - a
        // combination (Test.Codes with more than one bit) is Story 3.4's concern.
        var test = await db.Tests.SingleOrDefaultAsync(t => t.Id == request.TestId, cancellationToken);
        if (test == null)
            return await Result<AttemptDto>.FailureAsync($"Test with id: [{request.TestId}] not found.");

        if (test.Status != TestStatus.Published)
            return await Result<AttemptDto>.FailureAsync("Test is not published.");

        if (!IsSingleCode(test.Codes))
            return await Result<AttemptDto>.FailureAsync(
                "Test is a combination sitting; starting a single-code attempt requires a Test with exactly one Code.");

        // (2) Load the matching TestConfig + SectionRules for that Code.
        var testConfig = await db.TestConfigs
            .Include(tc => tc.SectionRules)
            .SingleOrDefaultAsync(tc => tc.Code == test.Codes, cancellationToken);
        if (testConfig == null)
            return await Result<AttemptDto>.FailureAsync($"No test configuration found for code [{test.Codes}].");

        // (3) Load Test.TestQuestions - the curated pool for this Test - grouped by section.
        var pool = await db.TestQuestions
            .Where(tq => tq.TestId == test.Id)
            .Include(tq => tq.Question).ThenInclude(q => q.AnswerOptions)
            .Select(tq => tq.Question)
            .ToListAsync(cancellationToken);
        var poolBySection = pool.GroupBy(q => q.Section).ToDictionary(g => g.Key, g => g.ToList());

        // (4) For each section in fixed order (SectionType's declared order is already
        // Rules -> Signs -> VehicleControls), reject the whole command if the pool is
        // under-provisioned; else randomly shuffle and take exactly QuestionCount.
        var orderedRules = testConfig.SectionRules.OrderBy(sr => sr.Section).ToList();

        // Guard against a TestConfig that is missing one of its three expected SectionRules (not
        // reachable today through any admin flow, but not guarded against either) - without this,
        // the handler would silently compose an attempt with fewer than 3 sections.
        var expectedSections = new[] { SectionType.Rules, SectionType.Signs, SectionType.VehicleControls };
        var missingSections = expectedSections.Except(orderedRules.Select(r => r.Section)).ToList();
        if (missingSections.Count > 0)
            return await Result<AttemptDto>.FailureAsync(
                $"Test configuration for code [{test.Codes}] is missing section rule(s) for: {string.Join(", ", missingSections)}.");

        var selections = new List<(SectionType Section, List<Question> Questions)>();
        foreach (var rule in orderedRules)
        {
            poolBySection.TryGetValue(rule.Section, out var sectionPool);
            sectionPool ??= new List<Question>();

            if (sectionPool.Count < rule.QuestionCount)
                return await Result<AttemptDto>.FailureAsync(
                    $"Section [{rule.Section}] has {sectionPool.Count} question(s) available, fewer than the {rule.QuestionCount} required.");

            var shuffled = sectionPool.OrderBy(_ => Random.Shared.Next()).Take(rule.QuestionCount).ToList();
            selections.Add((rule.Section, shuffled));
        }

        // (5) Upsert the LearnerProfile - this command IS the profile's first write, no separate
        // "create profile" endpoint exists.
        var learnerProfile = await db.LearnerProfiles
            .SingleOrDefaultAsync(lp => lp.Id == request.LearnerProfileId, cancellationToken);
        var learnerProfileWasCreated = learnerProfile == null;
        if (learnerProfile == null)
        {
            learnerProfile = new LearnerProfile { Id = request.LearnerProfileId };
            db.LearnerProfiles.Add(learnerProfile);
        }

        // (6) Build and save the Attempt + AttemptQuestion + AttemptAnswerOption graph with
        // sequential DisplayOrder - a single monotonic counter across all sections.
        var attempt = new Attempt
        {
            LearnerProfileId = request.LearnerProfileId,
            TestId = test.Id,
            Code = test.Codes,
            StartedAt = DateTime.UtcNow
        };

        var displayOrder = 1;
        foreach (var (section, questions) in selections)
        {
            foreach (var question in questions)
            {
                attempt.AttemptQuestions.Add(new AttemptQuestion
                {
                    QuestionId = question.Id,
                    Section = section,
                    DisplayOrder = displayOrder++,
                    Stem = question.Stem,
                    SignRef = question.SignRef,
                    AttemptAnswerOptions = question.AnswerOptions
                        .OrderBy(a => a.Order)
                        .Select(a => new AttemptAnswerOption
                        {
                            Text = a.Text,
                            IsCorrect = a.IsCorrect,
                            Order = a.Order
                        })
                        .ToList()
                });
            }
        }

        db.Attempts.Add(attempt);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (learnerProfileWasCreated)
        {
            // Two concurrent StartAttemptCommand calls for the same brand-new LearnerProfileId
            // (e.g. a client double-submit or a retried timed-out request) can both observe
            // "not found" above and both Add a LearnerProfile with the same PK; the loser hits a
            // unique-constraint violation here. By now the winner's LearnerProfile row already
            // exists, so detach the one we speculatively added and retry exactly once - the
            // Attempt insert then succeeds against the now-existing row (no navigation property
            // needs updating, since Attempt.LearnerProfileId is already a plain Guid value
            // pointing at the right id). If this invocation instead FOUND an existing
            // LearnerProfile, learnerProfileWasCreated is false and this catch is skipped
            // entirely, so any exception propagates normally. If the retry below also throws, it
            // is not caught again and propagates to the existing DbExceptionHandler pipeline
            // exactly as before.
            db.ChangeTracker.Entries<LearnerProfile>()
                .Single(e => ReferenceEquals(e.Entity, learnerProfile))
                .State = EntityState.Detached;

            await db.SaveChangesAsync(cancellationToken);
        }

        return await Result<AttemptDto>.SuccessAsync(_mapper.Map<AttemptDto>(attempt));
    }

    private static bool IsSingleCode(LicenceCode codes) =>
        codes == LicenceCode.Code1 || codes == LicenceCode.Code2 || codes == LicenceCode.Code3;
}
