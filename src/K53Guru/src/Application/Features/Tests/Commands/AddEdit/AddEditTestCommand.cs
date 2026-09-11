using K53Guru.Application.Common.Constants;
using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.Commands.AddEdit;

/// <summary>
/// Creates or edits a Test's scope only (Name/Codes/Sections) - a Test no longer curates an
/// explicit question pool. Attempt composition (<c>StartAttemptCommand</c>) draws its questions
/// directly from the bank, filtered by this Test's Codes/Sections, at the moment a learner starts
/// a sitting - see the removal of <c>TestQuestion</c>/<c>QuestionIds</c> and StartAttemptCommand's
/// pool query.
/// </summary>
[RequestAuthorize(Roles = Roles.Admin)]
public class AddEditTestCommand : ICacheInvalidatorRequest<Result<int>>
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public LicenceCode Codes { get; set; }
    public TestSectionScope Sections { get; set; }

    public string CacheKey => TestCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => TestCacheKey.Tags;

    private class Mapping : Profile
    {
        public Mapping()
        {
            // Status is never touched by this mapping: it has no source member on this command, so
            // only the handler's explicit "Status = TestStatus.Draft on create" ever sets it.
            CreateMap<AddEditTestCommand, Test>(MemberList.None);

            CreateMap<TestDto, AddEditTestCommand>(MemberList.None);
        }
    }
}

public class AddEditTestCommandHandler : IRequestHandler<AddEditTestCommand, Result<int>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public AddEditTestCommandHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<int>> Handle(AddEditTestCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        if (request.Id > 0)
        {
            var item = await db.Tests.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (item == null) return await Result<int>.FailureAsync($"Test with id: [{request.Id}] not found.");

            // Editing a Test must never change its Status - the command carries no Status
            // property, so this mapping cannot touch it; only Story 2.3's publish/unpublish
            // command ever transitions it away from whatever it already is.
            item = _mapper.Map(request, item);

            // An edit that only re-saves scalar fields to the same values leaves the change
            // tracker's Test entry at Unchanged - force it to Modified on every edit so any future
            // SaveChangesAsync-time safety net always runs (same fix Story 2.1 needed for
            // Question/AnswerOptions).
            db.ChangeTracker.Entries<Test>().Single(e => ReferenceEquals(e.Entity, item)).State = EntityState.Modified;

            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
        else
        {
            var item = _mapper.Map<Test>(request);
            item.Status = TestStatus.Draft;

            db.Tests.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
    }
}
