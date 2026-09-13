using K53Guru.Application.Common.Constants;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.RoadSigns.Caching;
using K53Guru.Application.Features.RoadSigns.DTOs;

namespace K53Guru.Application.Features.RoadSigns.Commands.AddEdit;

[RequestAuthorize(Roles = Roles.Admin)]
public class AddEditRoadSignCommand : ICacheInvalidatorRequest<Result<int>>
{
    public int Id { get; set; }
    public string? LegislationCode { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// The currently-persisted image key (e.g. "signs/r1.svg"). Read-only from the authoring
    /// UI's perspective - it is never set directly by the form; it only changes when
    /// <see cref="ImageBytes"/> is submitted and the handler writes a new file via
    /// IRoadSignImageStore.
    /// </summary>
    public string? ImageAssetKey { get; set; }

    /// <summary>
    /// New image bytes picked in the authoring UI, if any. Null/empty on an edit that doesn't
    /// re-pick a file, in which case <see cref="ImageAssetKey"/> is left exactly as it was on
    /// the existing row.
    /// </summary>
    public byte[]? ImageBytes { get; set; }

    /// <summary>
    /// Original file name of the picked upload (used only to derive the saved file's
    /// extension) - the on-disk base name is always derived from LegislationCode, never from
    /// this.
    /// </summary>
    public string? ImageFileName { get; set; }

    /// <summary>
    /// Admin-display provenance only (SPEC.md CAP-10): the existing catalog LegislationCodes
    /// this composite was assembled from, comma-separated (e.g. "R3,R531"). Never read by the
    /// learner API, AttemptQuestion, or the Flutter client.
    /// </summary>
    public string? ComponentSignCodes { get; set; }

    public string CacheKey => RoadSignCacheKey.GetAllCacheKey;
    public IEnumerable<string>? Tags => RoadSignCacheKey.Tags;

    private class Mapping : Profile
    {
        public Mapping()
        {
            // ImageAssetKey is derived by the handler (via IRoadSignImageStore) whenever
            // ImageBytes is submitted, and left untouched otherwise - it must never be
            // clobbered by a straight AutoMapper copy from the command.
            CreateMap<AddEditRoadSignCommand, RoadSign>(MemberList.None)
                .ForMember(d => d.ImageAssetKey, opt => opt.Ignore());

            CreateMap<RoadSignDto, AddEditRoadSignCommand>(MemberList.None);
        }
    }
}

public class AddEditRoadSignCommandHandler : IRequestHandler<AddEditRoadSignCommand, Result<int>>
{
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly IMapper _mapper;
    private readonly IRoadSignImageStore _imageStore;

    public AddEditRoadSignCommandHandler(
        IApplicationDbContextFactory dbContextFactory,
        IMapper mapper,
        IRoadSignImageStore imageStore
    )
    {
        _dbContextFactory = dbContextFactory;
        _mapper = mapper;
        _imageStore = imageStore;
    }

    public async Task<Result<int>> Handle(AddEditRoadSignCommand request, CancellationToken cancellationToken)
    {
        // Trimmed once here so the persisted LegislationCode, the image store's derived file
        // name, and every downstream comparison in this method all agree - mirrors the
        // validator's own trimming for its uniqueness check.
        request.LegislationCode = request.LegislationCode?.Trim();

        await using var db = await _dbContextFactory.CreateAsync(cancellationToken);

        string? newImageAssetKey = null;
        if (request.ImageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(request.LegislationCode))
        {
            newImageAssetKey = await _imageStore.SaveAsync(
                request.LegislationCode,
                request.ImageFileName ?? string.Empty,
                request.ImageBytes,
                cancellationToken);
        }

        if (request.Id > 0)
        {
            var item = await db.RoadSigns.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (item == null) return await Result<int>.FailureAsync($"Road sign with id: [{request.Id}] not found.");

            var previousImageAssetKey = item.ImageAssetKey;

            item = _mapper.Map(request, item);

            // Edit without a new image re-picked -> ImageAssetKey stays exactly as it was.
            if (newImageAssetKey != null)
            {
                item.ImageAssetKey = newImageAssetKey;
            }

            await db.SaveChangesAsync(cancellationToken);

            // Only after the new row has actually committed: clean up the file the new image
            // just superseded, so wwwroot/img/signs/ doesn't accumulate orphans. Best-effort -
            // IRoadSignImageStore.DeleteAsync never throws.
            if (newImageAssetKey != null &&
                !string.IsNullOrWhiteSpace(previousImageAssetKey) &&
                !string.Equals(previousImageAssetKey, newImageAssetKey, StringComparison.OrdinalIgnoreCase))
            {
                await _imageStore.DeleteAsync(previousImageAssetKey, cancellationToken);
            }

            return await Result<int>.SuccessAsync(item.Id);
        }
        else
        {
            var item = _mapper.Map<RoadSign>(request);
            item.ImageAssetKey = newImageAssetKey;

            db.RoadSigns.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
    }
}
