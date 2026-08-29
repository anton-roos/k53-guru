using K53Guru.Application.Features.RoadSigns.Caching;
using K53Guru.Application.Features.RoadSigns.DTOs;
using K53Guru.Application.Features.RoadSigns.Specifications;

namespace K53Guru.Application.Features.RoadSigns.Queries.GetById;

public class GetRoadSignByIdQuery : ICacheableRequest<Result<RoadSignDto>>
{
    public required int Id { get; set; }
    public string CacheKey => RoadSignCacheKey.GetByIdCacheKey($"{Id}");
    public IEnumerable<string>? Tags => RoadSignCacheKey.Tags;
}

public class GetRoadSignByIdQueryHandler :
    IRequestHandler<GetRoadSignByIdQuery, Result<RoadSignDto>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public GetRoadSignByIdQueryHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<RoadSignDto>> Handle(GetRoadSignByIdQuery request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var data = await db.RoadSigns.ApplySpecification(new RoadSignByIdSpecification(request.Id))
                                      .ProjectTo<RoadSignDto>(_mapper.ConfigurationProvider)
                                      .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new NotFoundException($"RoadSign with id: [{request.Id}] not found.");
        return await Result<RoadSignDto>.SuccessAsync(data);
    }
}
