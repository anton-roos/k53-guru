using K53Guru.Application.Common.Constants;
using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.Commands.Publish;

[RequestAuthorize(Roles = Roles.Admin)]
public class PublishTestCommand : ICacheInvalidatorRequest<Result>
{
    public int Id { get; set; }

    public string CacheKey => TestCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => TestCacheKey.Tags;
}

public class PublishTestCommandHandler : IRequestHandler<PublishTestCommand, Result>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;

    public PublishTestCommandHandler(IApplicationDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result> Handle(PublishTestCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        var item = await db.Tests.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (item == null) return await Result.FailureAsync($"Test with id: [{request.Id}] not found.");

        if (item.Status == TestStatus.Published)
            return await Result.FailureAsync("Test is already published.");

        item.Status = TestStatus.Published;
        await db.SaveChangesAsync(cancellationToken);
        return await Result.SuccessAsync();
    }
}
