using K53Guru.Application.Features.RoadSigns.Caching;
using K53Guru.Application.Features.RoadSigns.DTOs;
using K53Guru.Application.Features.RoadSigns.Specifications;

namespace K53Guru.Application.Features.RoadSigns.Queries.Pagination;

public class RoadSignsWithPaginationQuery : PaginationFilter, ICacheableRequest<PaginatedData<RoadSignDto>>
{
    public RoadSignAdvancedSpecification Specification => new(this);
    public string CacheKey => RoadSignCacheKey.GetPaginationCacheKey($"{this}");
    public IEnumerable<string>? Tags => RoadSignCacheKey.Tags;

    public override string ToString()
    {
        return $"Search:{Keyword},OrderBy:{OrderBy},SortDirection:{SortDirection},{PageNumber},{PageSize}";
    }
}

public class RoadSignsWithPaginationQueryHandler :
    IRequestHandler<RoadSignsWithPaginationQuery, PaginatedData<RoadSignDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public RoadSignsWithPaginationQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<PaginatedData<RoadSignDto>> Handle(RoadSignsWithPaginationQuery request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var data = await db.RoadSigns.OrderBy($"{request.OrderBy} {request.SortDirection}")
            .ProjectToPaginatedDataAsync<RoadSign, RoadSignDto>(request.Specification, request.PageNumber,
                request.PageSize, _mapper.ConfigurationProvider, cancellationToken);
        return data;
    }
}
