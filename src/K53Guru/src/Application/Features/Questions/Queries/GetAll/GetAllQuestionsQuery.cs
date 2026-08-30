using K53Guru.Application.Features.Questions.Caching;
using K53Guru.Application.Features.Questions.DTOs;

namespace K53Guru.Application.Features.Questions.Queries.GetAll;

public class GetAllQuestionsQuery : ICacheableRequest<IEnumerable<QuestionDto>>
{
    public string CacheKey => QuestionCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => QuestionCacheKey.Tags;
}

public class GetAllQuestionsQueryHandler : IRequestHandler<GetAllQuestionsQuery, IEnumerable<QuestionDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public GetAllQuestionsQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<IEnumerable<QuestionDto>> Handle(GetAllQuestionsQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var data = await db.Questions.OrderBy(x => x.Stem)
            .ProjectTo<QuestionDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        return data;
    }
}
