namespace K53Guru.Application.Features.RoadSigns.Caching;

public static class RoadSignCacheKey
{
    public const string GetAllCacheKey = "all-RoadSigns";

    public static string GetByIdCacheKey(string parameters)
    {
        return $"RoadSignCacheKey:GetByIdCacheKey,{parameters}";
    }

    public static string GetPaginationCacheKey(string parameters)
    {
        return $"RoadSignCacheKey:RoadSignsWithPaginationQuery,{parameters}";
    }

    public static IEnumerable<string>? Tags => new string[] { "roadsign" };

    public static void Refresh()
    {
        FusionCacheFactory.RemoveByTags(Tags);
    }
}
