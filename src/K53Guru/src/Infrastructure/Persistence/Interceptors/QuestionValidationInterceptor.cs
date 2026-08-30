using FluentValidation;
using K53Guru.Domain.Enums;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace K53Guru.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Enforces the two-axis question content model's save-time invariants:
///   (a) a non-null <see cref="Question.SignRef"/> resolves to exactly one catalog <see cref="RoadSign"/>;
///   (b) each <see cref="Question"/>'s <see cref="Question.AnswerOptions"/> has exactly one
///       <see cref="AnswerOption.IsCorrect"/> set to <c>true</c>;
///   (c) each <see cref="Question"/> carries at least one applicable <see cref="LicenceCode"/>.
/// Mirrors <see cref="AuditableEntityInterceptor"/>'s <c>SavingChangesAsync</c> override.
/// </summary>
public class QuestionValidationInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var questionEntries = context.ChangeTracker.Entries<Question>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in questionEntries)
        {
            var question = entry.Entity;

            if (question.Codes == LicenceCode.None)
            {
                throw new ValidationException("Question must have at least one applicable licence code.");
            }

            if (!string.IsNullOrEmpty(question.SignRef))
            {
                var matchCount = await context.Set<RoadSign>()
                    .Where(r => r.LegislationCode == question.SignRef)
                    .CountAsync(cancellationToken);

                if (matchCount == 0)
                {
                    throw new ValidationException(
                        $"Question sign_ref '{question.SignRef}' is unresolved: no matching RoadSign.LegislationCode was found.");
                }

                if (matchCount > 1)
                {
                    throw new ValidationException(
                        $"Question sign_ref '{question.SignRef}' is ambiguous: it matches more than one RoadSign.LegislationCode.");
                }
            }

            var correctCount = question.AnswerOptions.Count(a => a.IsCorrect);
            if (correctCount != 1)
            {
                throw new ValidationException(
                    $"Question must have exactly one correct AnswerOption, but found {correctCount}.");
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
