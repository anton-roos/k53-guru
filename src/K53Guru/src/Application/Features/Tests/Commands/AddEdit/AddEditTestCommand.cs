using K53Guru.Application.Common.Constants;
using K53Guru.Application.Features.Tests.Caching;
using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Tests.Commands.AddEdit;

[RequestAuthorize(Roles = Roles.Admin)]
public class AddEditTestCommand : ICacheInvalidatorRequest<Result<int>>
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public LicenceCode Codes { get; set; }
    public TestSectionScope Sections { get; set; }
    public List<int> QuestionIds { get; set; } = new();

    public string CacheKey => TestCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => TestCacheKey.Tags;

    private class Mapping : Profile
    {
        public Mapping()
        {
            // TestQuestions is reconciled explicitly in the handler (diff submitted QuestionIds
            // against tracked join rows - add missing, remove extra) - it must never be diffed
            // automatically by AutoMapper. Status is likewise never touched by this mapping: it
            // has no source member on this command, so only the handler's explicit
            // "Status = TestStatus.Draft on create" ever sets it.
            CreateMap<AddEditTestCommand, Test>(MemberList.None)
                .ForMember(d => d.TestQuestions, opt => opt.Ignore());

            CreateMap<TestDto, AddEditTestCommand>(MemberList.None)
                .ForMember(d => d.QuestionIds, opt => opt.MapFrom(s => s.Questions.Select(q => q.Id)));
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
            var item = await db.Tests
                .Include(t => t.TestQuestions)
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (item == null) return await Result<int>.FailureAsync($"Test with id: [{request.Id}] not found.");

            // Editing a Test must never change its Status - the command carries no Status
            // property, so this mapping cannot touch it; only Story 2.3's publish/unpublish
            // command ever transitions it away from whatever it already is.
            item = _mapper.Map(request, item);
            ReconcileTestQuestions(item, request.QuestionIds);

            // An edit that only touches TestQuestions (every scalar field unchanged) leaves the
            // change tracker's Test entry at Unchanged - force it to Modified on every edit so
            // any future SaveChangesAsync-time safety net always runs (same fix Story 2.1 needed
            // for Question/AnswerOptions).
            db.ChangeTracker.Entries<Test>().Single(e => ReferenceEquals(e.Entity, item)).State = EntityState.Modified;

            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
        else
        {
            var item = _mapper.Map<Test>(request);
            item.Status = TestStatus.Draft;
            item.TestQuestions = request.QuestionIds
                .Distinct()
                .Select(id => new TestQuestion { QuestionId = id })
                .ToList();

            db.Tests.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
    }

    /// <summary>
    /// Reconciles the tracked Test's TestQuestions join rows against the submitted QuestionIds:
    /// rows whose QuestionId is no longer submitted are removed (and deleted by EF Core on
    /// save), and submitted ids with no existing row are added as new join rows. Never diffed via
    /// AutoMapper.
    /// </summary>
    private static void ReconcileTestQuestions(Test item, List<int> submittedQuestionIds)
    {
        var submittedIds = submittedQuestionIds.ToHashSet();
        var toRemove = item.TestQuestions.Where(tq => !submittedIds.Contains(tq.QuestionId)).ToList();
        foreach (var remove in toRemove)
        {
            item.TestQuestions.Remove(remove);
        }

        var existingIds = item.TestQuestions.Select(tq => tq.QuestionId).ToHashSet();
        foreach (var id in submittedIds.Where(id => !existingIds.Contains(id)))
        {
            item.TestQuestions.Add(new TestQuestion { QuestionId = id });
        }
    }
}
