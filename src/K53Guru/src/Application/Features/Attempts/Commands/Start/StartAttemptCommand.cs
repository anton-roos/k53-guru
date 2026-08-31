using K53Guru.Application.Features.Attempts.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Attempts.Commands.Start;

/// <summary>
/// Composes and freezes a new Attempt for a learner against a published Test - either a
/// single-code Test (Story 3.3) or a valid combination Test (Code1+2/Code1+3 - Story 3.4). A
/// combination shares one Rules/Signs draw across every constituent code and adds one independent
/// VehicleControls module per constituent code, in fixed order. Anonymous learner-facing command -
/// no [RequestAuthorize], mirroring GetAvailableSittingsQuery/Story 3.1.
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

        // (1) Load the Test and reject if missing, not published, or an unsupported Codes value -
        // the same 5-value allowlist GetAvailableSittingsQuery/Story 3.1 already uses (single
        // Code1/Code2/Code3, or a valid Code1+2/Code1+3 combination; Code2+3 and all-three are
        // rejected).
        var test = await db.Tests.SingleOrDefaultAsync(t => t.Id == request.TestId, cancellationToken);
        if (test == null)
            return await Result<AttemptDto>.FailureAsync($"Test with id: [{request.TestId}] not found.");

        if (test.Status != TestStatus.Published)
            return await Result<AttemptDto>.FailureAsync("Test is not published.");

        if (!IsSupportedSitting(test.Codes))
            return await Result<AttemptDto>.FailureAsync(
                $"Test's Codes value [{test.Codes}] is not a supported sitting; must be a single code " +
                "(Code1, Code2, or Code3) or a valid combination (Code1+Code2 or Code1+Code3).");

        // Constituent codes in fixed composition order - Code1 first when present, then
        // Code2/Code3 (the allowlist above guarantees this is exactly one of: [Code1], [Code2],
        // [Code3], [Code1, Code2], [Code1, Code3]).
        var constituentCodes = new List<LicenceCode>();
        if (test.Codes.HasFlag(LicenceCode.Code1)) constituentCodes.Add(LicenceCode.Code1);
        if (test.Codes.HasFlag(LicenceCode.Code2)) constituentCodes.Add(LicenceCode.Code2);
        if (test.Codes.HasFlag(LicenceCode.Code3)) constituentCodes.Add(LicenceCode.Code3);
        var primaryCode = constituentCodes[0];

        // (2) Load the primary (first constituent) code's TestConfig + SectionRules. Rules/Signs
        // are shared across every code in the attempt, so either constituent code's TestConfig
        // works for them - Story 3.2 seeds numerically identical Rules/Signs values for every
        // code. This same TestConfig also supplies the primary code's own VehicleControls
        // SectionRule in step (5) below.
        var primaryConfig = await db.TestConfigs
            .Include(tc => tc.SectionRules)
            .SingleOrDefaultAsync(tc => tc.Code == primaryCode, cancellationToken);
        if (primaryConfig == null)
            return await Result<AttemptDto>.FailureAsync($"No test configuration found for code [{primaryCode}].");

        // Guard against a TestConfig that is missing one of its three expected SectionRules (not
        // reachable today through any admin flow, but not guarded against either) - without this,
        // the handler would silently compose an attempt with fewer than 3 sections.
        var expectedSections = new[] { SectionType.Rules, SectionType.Signs, SectionType.VehicleControls };
        var missingPrimarySections = expectedSections.Except(primaryConfig.SectionRules.Select(r => r.Section)).ToList();
        if (missingPrimarySections.Count > 0)
            return await Result<AttemptDto>.FailureAsync(
                $"Test configuration for code [{primaryCode}] is missing section rule(s) for: {string.Join(", ", missingPrimarySections)}.");

        // (3) Load Test.TestQuestions - the curated pool for this Test - grouped by section. A
        // pool question can legitimately carry more than one code's flag (shared-content edge
        // case); VehicleControls filters this pool per constituent code via HasFlag below, not
        // exact equality, so such a question is independently eligible for every code it flags.
        var pool = await db.TestQuestions
            .Where(tq => tq.TestId == test.Id)
            .Include(tq => tq.Question).ThenInclude(q => q.AnswerOptions)
            .Select(tq => tq.Question)
            .ToListAsync(cancellationToken);
        var poolBySection = pool.GroupBy(q => q.Section).ToDictionary(g => g.Key, g => g.ToList());

        var selections = new List<(SectionType Section, LicenceCode Code, List<Question> Questions)>();

        // (4) Rules and Signs: drawn once and shared across every code in the attempt.
        // AttemptQuestion.Code is set to the FULL attempt.Code (the whole combination, or the
        // single code) since these sections' result applies identically to every code.
        var rulesAndSignsRules = primaryConfig.SectionRules
            .Where(sr => sr.Section == SectionType.Rules || sr.Section == SectionType.Signs)
            .OrderBy(sr => sr.Section)
            .ToList();
        foreach (var rule in rulesAndSignsRules)
        {
            poolBySection.TryGetValue(rule.Section, out var sectionPool);
            sectionPool ??= new List<Question>();

            if (sectionPool.Count < rule.QuestionCount)
                return await Result<AttemptDto>.FailureAsync(
                    $"Section [{rule.Section}] has {sectionPool.Count} question(s) available, fewer than the {rule.QuestionCount} required.");

            var shuffled = sectionPool.OrderBy(_ => Random.Shared.Next()).Take(rule.QuestionCount).ToList();
            selections.Add((rule.Section, test.Codes, shuffled));
        }

        // (5) VehicleControls: one independent module per constituent code, in fixed order
        // (Code1 first when present, then Code2/Code3). Each module is filtered to that code's
        // eligible pool (Question.Codes.HasFlag(code)) and governed by that code's own
        // TestConfig+VehicleControls SectionRule, composed exactly like Story 3.3's per-section
        // logic - AttemptQuestion.Code is the ONE specific constituent code that module belongs to.
        poolBySection.TryGetValue(SectionType.VehicleControls, out var vehicleControlsPool);
        vehicleControlsPool ??= new List<Question>();

        foreach (var code in constituentCodes)
        {
            SectionRule vehicleControlsRule;
            if (code == primaryCode)
            {
                // Already loaded and validated above via missingPrimarySections - but that guard only
                // detects ABSENCE, not duplication. Nothing in the schema enforces uniqueness of
                // (TestConfigId, Section), so a TestConfig with two VehicleControls SectionRule rows
                // is not reachable today through any admin flow, but not guarded against either.
                // SingleOrDefault + an explicit failure keeps that case a graceful rejection instead
                // of an unhandled InvalidOperationException from Single().
                var primaryRule = primaryConfig.SectionRules.SingleOrDefault(sr => sr.Section == SectionType.VehicleControls);
                if (primaryRule == null)
                    return await Result<AttemptDto>.FailureAsync(
                        $"Test configuration for code [{code}] has more than one section rule for: {SectionType.VehicleControls}.");

                vehicleControlsRule = primaryRule;
            }
            else
            {
                var codeConfig = await db.TestConfigs
                    .Include(tc => tc.SectionRules)
                    .SingleOrDefaultAsync(tc => tc.Code == code, cancellationToken);
                if (codeConfig == null)
                    return await Result<AttemptDto>.FailureAsync($"No test configuration found for code [{code}].");

                var rule = codeConfig.SectionRules.SingleOrDefault(sr => sr.Section == SectionType.VehicleControls);
                if (rule == null)
                    return await Result<AttemptDto>.FailureAsync(
                        $"Test configuration for code [{code}] is missing section rule(s) for: {SectionType.VehicleControls}.");

                vehicleControlsRule = rule;
            }

            var codePool = vehicleControlsPool.Where(q => q.Codes.HasFlag(code)).ToList();
            if (codePool.Count < vehicleControlsRule.QuestionCount)
                return await Result<AttemptDto>.FailureAsync(
                    $"Section [{SectionType.VehicleControls}] for code [{code}] has {codePool.Count} question(s) " +
                    $"available, fewer than the {vehicleControlsRule.QuestionCount} required.");

            var shuffled = codePool.OrderBy(_ => Random.Shared.Next()).Take(vehicleControlsRule.QuestionCount).ToList();
            selections.Add((SectionType.VehicleControls, code, shuffled));
        }

        // (6) Upsert the LearnerProfile - this command IS the profile's first write, no separate
        // "create profile" endpoint exists.
        var learnerProfile = await db.LearnerProfiles
            .SingleOrDefaultAsync(lp => lp.Id == request.LearnerProfileId, cancellationToken);
        var learnerProfileWasCreated = learnerProfile == null;
        if (learnerProfile == null)
        {
            learnerProfile = new LearnerProfile { Id = request.LearnerProfileId };
            db.LearnerProfiles.Add(learnerProfile);
        }

        // (7) Build and save the Attempt + AttemptQuestion + AttemptAnswerOption graph with
        // sequential DisplayOrder - a single monotonic counter across Rules, Signs, then each
        // code's VehicleControls block in order (the order `selections` was built in above).
        var attempt = new Attempt
        {
            LearnerProfileId = request.LearnerProfileId,
            TestId = test.Id,
            Code = test.Codes,
            StartedAt = DateTime.UtcNow
        };

        var displayOrder = 1;
        foreach (var (section, code, questions) in selections)
        {
            foreach (var question in questions)
            {
                attempt.AttemptQuestions.Add(new AttemptQuestion
                {
                    QuestionId = question.Id,
                    Section = section,
                    Code = code,
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

    /// <summary>
    /// The same 5-value allowlist GetAvailableSittingsQuery/Story 3.1 already uses: a single code
    /// (Code1/Code2/Code3, unchanged from Story 3.3) or a valid combination (Code1+2, Code1+3).
    /// Code2+3, all-three, and None are all rejected. Mirrors that exact check rather than
    /// reinventing it.
    /// </summary>
    private static bool IsSupportedSitting(LicenceCode codes) =>
        codes == LicenceCode.Code1
        || codes == LicenceCode.Code2
        || codes == LicenceCode.Code3
        || codes == (LicenceCode.Code1 | LicenceCode.Code2)
        || codes == (LicenceCode.Code1 | LicenceCode.Code3);
}
