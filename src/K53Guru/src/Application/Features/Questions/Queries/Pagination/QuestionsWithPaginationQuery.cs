using K53Guru.Application.Features.Questions.Caching;
using K53Guru.Application.Features.Questions.DTOs;
using K53Guru.Application.Features.Questions.Specifications;

namespace K53Guru.Application.Features.Questions.Queries.Pagination;

public class QuestionsWithPaginationQuery : PaginationFilter, ICacheableRequest<PaginatedData<QuestionDto>>
{
    public QuestionAdvancedSpecification Specification => new(this);
    public string CacheKey => QuestionCacheKey.GetPaginationCacheKey($"{this}");
    public IEnumerable<string>? Tags => QuestionCacheKey.Tags;

    public override string ToString()
    {
        return $"Search:{Keyword},OrderBy:{OrderBy},SortDirection:{SortDirection},{PageNumber},{PageSize}";
    }
}

public class QuestionsWithPaginationQueryHandler :
    IRequestHandler<QuestionsWithPaginationQuery, PaginatedData<QuestionDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public QuestionsWithPaginationQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<PaginatedData<QuestionDto>> Handle(QuestionsWithPaginationQuery request,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var data = await db.Questions.OrderBy($"{request.OrderBy} {request.SortDirection}")
            .ProjectToPaginatedDataAsync<Question, QuestionDto>(request.Specification, request.PageNumber,
                request.PageSize, _mapper.ConfigurationProvider, cancellationToken);
        return data;
    }
}
