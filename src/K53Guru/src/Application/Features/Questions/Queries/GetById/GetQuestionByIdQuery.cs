using K53Guru.Application.Features.Questions.Caching;
using K53Guru.Application.Features.Questions.DTOs;
using K53Guru.Application.Features.Questions.Specifications;

namespace K53Guru.Application.Features.Questions.Queries.GetById;

public class GetQuestionByIdQuery : ICacheableRequest<Result<QuestionDto>>
{
    public required int Id { get; set; }
    public string CacheKey => QuestionCacheKey.GetByIdCacheKey($"{Id}");
    public IEnumerable<string>? Tags => QuestionCacheKey.Tags;
}

public class GetQuestionByIdQueryHandler :
    IRequestHandler<GetQuestionByIdQuery, Result<QuestionDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public GetQuestionByIdQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<QuestionDto>> Handle(GetQuestionByIdQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        // QuestionByIdSpecification includes AnswerOptions so the edit dialog gets the full
        // child list; ProjectTo also projects the AnswerOptions collection on its own, but the
        // explicit Include keeps this handler correct even if the DTO projection changes shape.
        var data = await db.Questions.ApplySpecification(new QuestionByIdSpecification(request.Id))
                                      .ProjectTo<QuestionDto>(_mapper.ConfigurationProvider)
                                      .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new NotFoundException($"Question with id: [{request.Id}] not found.");
        return await Result<QuestionDto>.SuccessAsync(data);
    }
}
