using System.Text;
using K53Guru.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;

namespace K53Guru.Infrastructure.Services;

/// <summary>
/// Writes admin-authored RoadSign images under {IWebHostEnvironment.WebRootPath}/img/signs/ -
/// the same convention every existing (seeded) sign already uses, resolved back to a URL by
/// RoadSignExtensions.ToImageUrl. Deliberately separate from IUploadService/MinIO
/// (FileUploadService/MinioUploadService write to Files/ or object storage instead).
/// </summary>
public class RoadSignImageStore : IRoadSignImageStore
{
    private const string DefaultExtension = ".svg";
    private const string SignsSubPath = "signs";

    /// <summary>
    /// Characters stripped from the derived base file name in addition to
    /// Path.GetInvalidFileNameChars(). '/' and '\' are included explicitly because
    /// Path.GetInvalidFileNameChars() on Linux only excludes '\0' and '/' - an admin-typed
    /// LegislationCode must never be able to smuggle a path separator through on that platform.
    /// '.' is included so the base name can never contain "..", ruling out path traversal via
    /// the base name entirely (the extension is derived and validated separately).
    /// </summary>
    private static readonly HashSet<char> AdditionalInvalidBaseNameChars = new() { '/', '\\', '.' };

    private readonly IWebHostEnvironment _webHostEnvironment;

    public RoadSignImageStore(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<string> SaveAsync(string legislationCode, string fileName, byte[] bytes, CancellationToken cancellationToken)
    {
        var extension = SanitizeExtension(fileName);
        var baseName = SanitizeBaseName(legislationCode);
        var safeFileName = $"{baseName}{extension}";

        var signsFolderPath = GetSignsFolderPath();
        Directory.CreateDirectory(signsFolderPath);

        var fullPath = ResolveWithinSignsFolder(signsFolderPath, safeFileName);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        return $"{SignsSubPath}/{safeFileName}";
    }

    public Task DeleteAsync(string? imageAssetKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(imageAssetKey))
        {
            try
            {
                var fileName = Path.GetFileName(imageAssetKey);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    var fullPath = ResolveWithinSignsFolder(GetSignsFolderPath(), fileName);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
            }
            catch
            {
                // Best-effort: an orphaned file is a disk-space cleanliness issue, not a
                // correctness one, and must never fail a request whose SaveChangesAsync already
                // committed successfully.
            }
        }

        return Task.CompletedTask;
    }

    private string GetSignsFolderPath()
    {
        return Path.Combine(_webHostEnvironment.WebRootPath, "img", SignsSubPath);
    }

    /// <summary>
    /// Combines <paramref name="signsFolderPath"/> with <paramref name="fileName"/> and verifies
    /// the resolved path still lies inside the signs folder - defense-in-depth against path
    /// traversal even though both SaveAsync's and DeleteAsync's callers already reduce
    /// <paramref name="fileName"/> to a bare file name first.
    /// </summary>
    private static string ResolveWithinSignsFolder(string signsFolderPath, string fileName)
    {
        var fullPath = Path.GetFullPath(Path.Combine(signsFolderPath, fileName));
        var normalizedFolder = Path.GetFullPath(signsFolderPath + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Resolved sign image path '{fullPath}' escapes the signs folder.");
        }

        return fullPath;
    }

    /// <summary>
    /// Derives a safe on-disk base name from an admin-typed LegislationCode: trimmed, lowercased,
    /// '+' replaced with '-' (e.g. "R3+R531" -> "r3-r531"), and every character that is invalid
    /// in a file name - or could enable path traversal - replaced with '-'.
    /// </summary>
    private static string SanitizeBaseName(string legislationCode)
    {
        var normalized = legislationCode.Trim().Replace('+', '-').ToLowerInvariant();

        var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        invalidChars.UnionWith(AdditionalInvalidBaseNameChars);

        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            builder.Append(invalidChars.Contains(ch) ? '-' : ch);
        }

        var sanitized = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "sign" : sanitized;
    }

    /// <summary>
    /// Derives a safe file extension (including the leading '.') from the originally-uploaded
    /// file name, falling back to ".svg" when it carries none or an implausible/unsafe one.
    /// Server-side file-type acceptance is enforced by AddEditRoadSignCommandValidator's
    /// extension allow-list before this is ever reached; this is an independent defensive check
    /// so RoadSignImageStore is never itself the thing trusting untrusted input.
    /// </summary>
    private static string SanitizeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
            return DefaultExtension;

        if (extension.Skip(1).Any(c => !char.IsLetterOrDigit(c)))
            return DefaultExtension;

        return extension.ToLowerInvariant();
    }
}
