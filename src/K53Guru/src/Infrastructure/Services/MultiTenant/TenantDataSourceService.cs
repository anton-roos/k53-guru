using K53Guru.Application.Features.Tenants.Caching;
using K53Guru.Application.Features.Tenants.DTOs;
using ZiggyCreatures.Caching.Fusion;

namespace K53Guru.Infrastructure.Services.MultiTenant;

public class TenantDataSourceService : DataSourceServiceBase<TenantDto>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public TenantDataSourceService(
        IMapper mapper,
        IFusionCache fusionCache,
        IApplicationDbContextFactory dbContextFactory)
        : base(fusionCache, TenantCacheKey.TenantsCacheKey)
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    protected override async Task<List<TenantDto>?> LoadAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        return await db.Tenants.ProjectTo<TenantDto>(_mapper.ConfigurationProvider)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}
