using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Application.Features.Tests.Specifications;

namespace K53Guru.Application.Features.Tests.Queries.Pagination;

public class TestsWithPaginationQuery : PaginationFilter, ICacheableRequest<PaginatedData<TestDto>>
{
    public TestAdvancedSpecification Specification => new(this);
    public string CacheKey => TestCacheKey.GetPaginationCacheKey($"{this}");
    public IEnumerable<string>? Tags => TestCacheKey.Tags;

    public override string ToString()
    {
        return $"Search:{Keyword},OrderBy:{OrderBy},SortDirection:{SortDirection},{PageNumber},{PageSize}";
    }
}

public class TestsWithPaginationQueryHandler :
    IRequestHandler<TestsWithPaginationQuery, PaginatedData<TestDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public TestsWithPaginationQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<PaginatedData<TestDto>> Handle(TestsWithPaginationQuery request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var data = await db.Tests.OrderBy($"{request.OrderBy} {request.SortDirection}")
            .ProjectToPaginatedDataAsync<Test, TestDto>(request.Specification, request.PageNumber,
                request.PageSize, _mapper.ConfigurationProvider, cancellationToken);
        return data;
    }
}
