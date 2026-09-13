namespace K53Guru.Application.Common.Interfaces;

/// <summary>
/// Saves an admin-authored RoadSign image under wwwroot/img/signs/ - the same convention every
/// existing (seeded) sign already uses and that RoadSignExtensions.ToImageUrl resolves against.
/// Deliberately not IUploadService/MinIO: that path writes to Files/ or object storage, which
/// would break every existing sign's resolution (spec-1-4-author-combination-signs.md).
/// </summary>
public interface IRoadSignImageStore
{
    /// <summary>
    /// Writes <paramref name="bytes"/> under wwwroot/img/signs/, deriving the on-disk file name
    /// from <paramref name="legislationCode"/> (lowercased, '+' replaced with '-', e.g.
    /// "R3+R531" -> "r3-r531") with the extension taken from <paramref name="fileName"/> (falling
    /// back to ".svg" when it carries none). Returns the resulting ImageAssetKey (e.g.
    /// "signs/r3-r531.svg") for storage on RoadSign.ImageAssetKey.
    /// </summary>
    Task<string> SaveAsync(string legislationCode, string fileName, byte[] bytes, CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort deletion of a previously-saved image at <paramref name="imageAssetKey"/> (e.g.
    /// "signs/r1.svg") - used when an edit's new image supersedes an old file at a different
    /// path, to avoid orphaned files accumulating under wwwroot/img/signs/. Never throws; a
    /// failed delete must not fail a request whose SaveChangesAsync already committed.
    /// </summary>
    Task DeleteAsync(string? imageAssetKey, CancellationToken cancellationToken);
}
