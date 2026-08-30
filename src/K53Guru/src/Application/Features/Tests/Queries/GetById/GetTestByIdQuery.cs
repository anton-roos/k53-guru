using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Application.Features.Tests.Specifications;

namespace K53Guru.Application.Features.Tests.Queries.GetById;

public class GetTestByIdQuery : ICacheableRequest<Result<TestDto>>
{
    public required int Id { get; set; }
    public string CacheKey => TestCacheKey.GetByIdCacheKey($"{Id}");
    public IEnumerable<string>? Tags => TestCacheKey.Tags;
}

public class GetTestByIdQueryHandler :
    IRequestHandler<GetTestByIdQuery, Result<TestDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public GetTestByIdQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<TestDto>> Handle(GetTestByIdQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        // TestByIdSpecification includes TestQuestions.Question so the associated-questions list
        // (grouped/counted client-side by the Razor code-behind) is returned flat and complete.
        var data = await db.Tests.ApplySpecification(new TestByIdSpecification(request.Id))
                                  .ProjectTo<TestDto>(_mapper.ConfigurationProvider)
                                  .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new NotFoundException($"Test with id: [{request.Id}] not found.");
        return await Result<TestDto>.SuccessAsync(data);
    }
}
