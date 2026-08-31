namespace K53Guru.Domain.Enums;

/// <summary>
/// Which of the two learner experiences an <see cref="Entities.Attempt"/> is sitting under,
/// chosen once at <c>StartAttemptCommand</c> time and never changed thereafter (Story 3.6).
/// Practice mode permits immediate per-question correctness/explanation (via
/// <c>CheckAnswerCommand</c>) and retry, with no server-enforced time limit. Test mode withholds
/// correctness/explanations entirely until submission and enforces the sitting's configured
/// time limit server-side.
/// </summary>
public enum AttemptMode
{
    Practice,
    Test
}
