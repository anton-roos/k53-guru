using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.Queries.AvailableSittings;

/// <summary>
/// Discovers currently published, servable sittings (single-code or valid Code1+2/Code1+3
/// combination Tests) for the anonymous learner-facing API - see spec-3-1-discover-available-sittings.md.
/// Cache-tagged with <see cref="TestCacheKey.Tags"/> so Story 2.3's PublishTestCommand/
/// UnpublishTestCommand (already ICacheInvalidatorRequest) automatically invalidate/refresh this
/// query's cache with no new wiring.
/// </summary>
public class GetAvailableSittingsQuery : ICacheableRequest<List<AvailableSittingDto>>
{
    public string CacheKey => TestCacheKey.GetAvailableSittingsCacheKey;
    public IEnumerable<string>? Tags => TestCacheKey.Tags;
}

public class GetAvailableSittingsQueryHandler : IRequestHandler<GetAvailableSittingsQuery, List<AvailableSittingDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public GetAvailableSittingsQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<List<AvailableSittingDto>> Handle(GetAvailableSittingsQuery request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var data = await db.Tests
            .Where(t => t.Status == TestStatus.Published
                        && (t.Codes == LicenceCode.Code1
                            || t.Codes == LicenceCode.Code2
                            || t.Codes == LicenceCode.Code3
                            || t.Codes == (LicenceCode.Code1 | LicenceCode.Code2)
                            || t.Codes == (LicenceCode.Code1 | LicenceCode.Code3)))
            .ProjectTo<AvailableSittingDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        return data;
    }
}
