using K53Guru.Application.Features.Attempts.DTOs;

namespace K53Guru.Application.Common.Extensions;

/// <summary>
/// Maps a <see cref="RoadSign.ImageAssetKey"/> (a relative path under wwwroot/img, e.g.
/// "signs/r2.svg") to the publicly-servable URL clients fetch it from. Mirrors
/// Server.UI/Pages/RoadSigns/Components/RoadSignDetailDialog.razor's ImageUrl logic exactly --
/// both the Admin UI and the learner-facing API resolve the same stored key the same way, so
/// keep them in sync if this ever changes.
/// </summary>
public static class RoadSignExtensions
{
    public static string? ToImageUrl(this string? imageAssetKey)
    {
        if (string.IsNullOrWhiteSpace(imageAssetKey))
            return null;

        var escapedSegments = imageAssetKey.Split('/').Select(Uri.EscapeDataString);
        return $"/img/{string.Join('/', escapedSegments)}";
    }

    /// <summary>
    /// Batch-resolves every distinct <see cref="AttemptQuestionDto.SignRef"/> in
    /// <paramref name="questions"/> against the RoadSigns catalog and sets each question's
    /// <see cref="AttemptQuestionDto.SignImageUrl"/> in place. Shared by
    /// StartAttemptCommandHandler and GetAttemptQueryHandler so both return an identical shape
    /// (GetAttemptQuery's resume contract requires it). A SignRef with no matching catalog row,
    /// or a matching row with no ImageAssetKey, is left null rather than failing the request --
    /// AttemptQuestion.SignRef is a frozen snapshot (Question.SignRef at start time), and the
    /// catalog is mutable afterward, so a "missing image" here is a display gap, not a data
    /// integrity error.
    /// </summary>
    public static async Task PopulateSignImageUrlsAsync(
        this IApplicationDbContext db,
        List<AttemptQuestionDto> questions,
        CancellationToken cancellationToken)
    {
        var signRefs = questions
            .Where(q => q.SignRef != null)
            .Select(q => q.SignRef!)
            .Distinct()
            .ToList();
        if (signRefs.Count == 0)
            return;

        var imageAssetKeysByCode = await db.RoadSigns
            .Where(r => signRefs.Contains(r.LegislationCode))
            .ToDictionaryAsync(r => r.LegislationCode, r => r.ImageAssetKey, cancellationToken);

        foreach (var question in questions)
        {
            if (question.SignRef != null && imageAssetKeysByCode.TryGetValue(question.SignRef, out var assetKey))
                question.SignImageUrl = assetKey.ToImageUrl();
        }
    }
}
