using K53Guru.Application.Common.Constants;
using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.Commands.Unpublish;

[RequestAuthorize(Roles = Roles.Admin)]
public class UnpublishTestCommand : ICacheInvalidatorRequest<Result>
{
    public int Id { get; set; }

    public string CacheKey => TestCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => TestCacheKey.Tags;
}

public class UnpublishTestCommandHandler : IRequestHandler<UnpublishTestCommand, Result>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;

    public UnpublishTestCommandHandler(IApplicationDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result> Handle(UnpublishTestCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var item = await db.Tests.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (item == null) return await Result.FailureAsync($"Test with id: [{request.Id}] not found.");

        if (item.Status == TestStatus.Draft)
            return await Result.FailureAsync("Test is already a draft.");

        item.Status = TestStatus.Draft;
        await db.SaveChangesAsync(cancellationToken);
        return await Result.SuccessAsync();
    }
}
