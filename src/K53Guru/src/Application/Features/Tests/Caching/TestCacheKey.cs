namespace K53Guru.Application.Features.Tests.Caching;

public static class TestCacheKey
{
    public const string GetAllCacheKey = "all-Tests";

    public static string GetByIdCacheKey(string parameters)
    {
        return $"TestCacheKey:GetByIdCacheKey,{parameters}";
    }

    public static string GetPaginationCacheKey(string parameters)
    {
        return $"TestCacheKey:TestsWithPaginationQuery,{parameters}";
    }

    public static IEnumerable<string>? Tags => new string[] { "test" };

    public static void Refresh()
    {
        FusionCacheFactory.RemoveByTags(Tags);
    }
}
