using K53Guru.Application.Common.Constants;
using K53Guru.Application.Features.Questions.Caching;
using K53Guru.Application.Features.Questions.DTOs;
using K53Guru.Domain.Enums;

namespace K53Guru.Application.Features.Questions.Commands.AddEdit;

[RequestAuthorize(Roles = Roles.Admin)]
public class AddEditQuestionCommand : ICacheInvalidatorRequest<Result<int>>
{
    public int Id { get; set; }
    public string? Stem { get; set; }
    public LicenceCode Codes { get; set; }
    public SectionType Section { get; set; }
    public string LanguageCode { get; set; } = "en";

    /// <summary>
    /// The official legislation-code string of the referenced RoadSign. Never an FK - resolved
    /// to exactly one catalog sign by the validator's async rule before the row can be saved.
    /// </summary>
    public string? SignRef { get; set; }

    /// <summary>
    /// Optional explanation shown to the learner in Practice mode after answering (Story 3.6).
    /// Nullable - authoring it is optional; leaving it empty simply means Practice mode's
    /// check-answer response carries no explanation for this question.
    /// </summary>
    public string? Explanation { get; set; }

    public List<AnswerOptionModel> AnswerOptions { get; set; } = new();

    public string CacheKey => QuestionCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => QuestionCacheKey.Tags;

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<AnswerOption, AnswerOptionModel>().ReverseMap();

            // AnswerOptions is reconciled explicitly in the handler (match by Id, add new,
            // remove missing) - it must never be diffed automatically by AutoMapper.
            CreateMap<AddEditQuestionCommand, Question>(MemberList.None)
                .ForMember(d => d.AnswerOptions, opt => opt.Ignore());

            CreateMap<QuestionDto, AddEditQuestionCommand>(MemberList.None);
            CreateMap<AnswerOptionDto, AnswerOptionModel>(MemberList.None);
        }
    }
}

public class AnswerOptionModel
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Ignored on save - the handler always derives the persisted Order from this option's
    /// position in the submitted AnswerOptions list, never from client input.
    /// </summary>
    public int Order { get; set; }
}

public class AddEditQuestionCommandHandler : IRequestHandler<AddEditQuestionCommand, Result<int>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;

    public AddEditQuestionCommandHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
    }

    public async Task<Result<int>> Handle(AddEditQuestionCommand request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);
        if (request.Id > 0)
        {
            var item = await db.Questions
                .Include(q => q.AnswerOptions)
                .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (item == null) return await Result<int>.FailureAsync($"Question with id: [{request.Id}] not found.");

            item = _mapper.Map(request, item);
            ReconcileAnswerOptions(item, request.AnswerOptions);

            // An edit that only touches AnswerOptions (every scalar field unchanged) leaves the
            // change tracker's Question entry at Unchanged - QuestionValidationInterceptor only
            // inspects Added/Modified Question entries, so it would silently skip re-validating
            // sign_ref/codes/correct-count for that edit shape. Force it to Modified on every
            // edit so the interceptor's safety net always runs.
            db.ChangeTracker.Entries<Question>().Single(e => ReferenceEquals(e.Entity, item)).State = EntityState.Modified;

            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
        else
        {
            var item = _mapper.Map<Question>(request);
            item.AnswerOptions = BuildAnswerOptions(request.AnswerOptions);

            db.Questions.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
    }

    /// <summary>
    /// Reconciles the tracked Question's AnswerOptions collection against the submitted list:
    /// options present in the submitted list by Id are updated in place, options missing from
    /// it are removed (and - being a required relationship - deleted by EF Core on save), and
    /// options with no Id (or an Id that no longer matches) are added as new rows. Order is
    /// always the option's index within the submitted list, never client input.
    /// </summary>
    private static void ReconcileAnswerOptions(Question item, List<AnswerOptionModel> submitted)
    {
        var submittedIds = submitted.Where(a => a.Id > 0).Select(a => a.Id).ToHashSet();
        var toRemove = item.AnswerOptions.Where(existing => !submittedIds.Contains(existing.Id)).ToList();
        foreach (var remove in toRemove)
        {
            item.AnswerOptions.Remove(remove);
        }

        for (var index = 0; index < submitted.Count; index++)
        {
            var model = submitted[index];
            var existing = model.Id > 0
                ? item.AnswerOptions.SingleOrDefault(a => a.Id == model.Id)
                : null;

            if (existing != null)
            {
                existing.Text = model.Text ?? string.Empty;
                existing.IsCorrect = model.IsCorrect;
                existing.Order = index;
            }
            else
            {
                item.AnswerOptions.Add(new AnswerOption
                {
                    Text = model.Text ?? string.Empty,
                    IsCorrect = model.IsCorrect,
                    Order = index
                });
            }
        }
    }

    private static List<AnswerOption> BuildAnswerOptions(List<AnswerOptionModel> submitted)
    {
        var options = new List<AnswerOption>();
        for (var index = 0; index < submitted.Count; index++)
        {
            var model = submitted[index];
            options.Add(new AnswerOption
            {
                Text = model.Text ?? string.Empty,
                IsCorrect = model.IsCorrect,
                Order = index
            });
        }

        return options;
    }
}
