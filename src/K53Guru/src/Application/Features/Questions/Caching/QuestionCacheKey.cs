namespace K53Guru.Application.Features.Questions.Caching;

public static class QuestionCacheKey
{
    public const string GetAllCacheKey = "all-Questions";

    public static string GetByIdCacheKey(string parameters)
    {
        return $"QuestionCacheKey:GetByIdCacheKey,{parameters}";
    }

    public static string GetPaginationCacheKey(string parameters)
    {
        return $"QuestionCacheKey:QuestionsWithPaginationQuery,{parameters}";
    }

    public static IEnumerable<string>? Tags => new string[] { "question" };

    public static void Refresh()
    {
        FusionCacheFactory.RemoveByTags(Tags);
    }
}
