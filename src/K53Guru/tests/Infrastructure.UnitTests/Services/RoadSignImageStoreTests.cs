using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K53Guru.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Services;

/// <summary>
/// Covers spec-1-4-author-combination-signs.md's patch-category findings against
/// RoadSignImageStore directly:
///   - An admin-typed LegislationCode containing path separators or ".." must never escape
///     wwwroot/img/signs/ (path traversal).
///   - DeleteAsync is a real, best-effort no-throw removal used to clean up a superseded image.
/// </summary>
public class RoadSignImageStoreTests : IDisposable
{
    private readonly string _webRootPath;
    private readonly RoadSignImageStore _store;

    public RoadSignImageStoreTests()
    {
        _webRootPath = Path.Combine(Path.GetTempPath(), "k53guru-roadsign-image-store-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_webRootPath);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.SetupGet(e => e.WebRootPath).Returns(_webRootPath);

        _store = new RoadSignImageStore(envMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRootPath))
        {
            Directory.Delete(_webRootPath, recursive: true);
        }
    }

    private string SignsFolder => Path.Combine(_webRootPath, "img", "signs");

    [Fact]
    public async Task SaveAsync_PlainLegislationCode_WritesFileUnderSignsFolder()
    {
        var assetKey = await _store.SaveAsync("R3+R531", "combo.svg", new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal("signs/r3-r531.svg", assetKey);
        Assert.True(File.Exists(Path.Combine(SignsFolder, "r3-r531.svg")));
    }

    [Theory]
    [InlineData("../../evil")]
    [InlineData("..\\..\\evil")]
    [InlineData("R1/../../evil")]
    [InlineData("R1\\..\\..\\evil")]
    public async Task SaveAsync_LegislationCodeAttemptsPathTraversal_FileStaysInsideSignsFolder(string maliciousCode)
    {
        var assetKey = await _store.SaveAsync(maliciousCode, "evil.svg", new byte[] { 1 }, CancellationToken.None);

        // The returned key must be a bare "signs/<file>" key - never anything that could resolve
        // outside the signs folder - and the file actually written must live inside it.
        Assert.StartsWith("signs/", assetKey);
        Assert.DoesNotContain("..", assetKey);

        var writtenFiles = Directory.GetFiles(SignsFolder, "*", SearchOption.AllDirectories);
        Assert.Single(writtenFiles);
        Assert.StartsWith(Path.GetFullPath(SignsFolder), Path.GetFullPath(writtenFiles[0]), StringComparison.OrdinalIgnoreCase);

        // Nothing was written anywhere above wwwroot/img/signs/.
        Assert.False(File.Exists(Path.Combine(_webRootPath, "evil")));
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "evil")));
    }

    [Fact]
    public async Task SaveAsync_LegislationCodeWithInvalidFileNameChars_Sanitized()
    {
        var assetKey = await _store.SaveAsync("R1:*?\"<>|", "sign.png", new byte[] { 1 }, CancellationToken.None);

        Assert.StartsWith("signs/", assetKey);
        var fileName = assetKey.Substring("signs/".Length);
        Assert.All(fileName, c => Assert.DoesNotContain(c, Path.GetInvalidFileNameChars()));
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        var assetKey = await _store.SaveAsync("R1", "r1.svg", new byte[] { 1 }, CancellationToken.None);
        var fullPath = Path.Combine(SignsFolder, "r1.svg");
        Assert.True(File.Exists(fullPath));

        await _store.DeleteAsync(assetKey, CancellationToken.None);

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        await _store.DeleteAsync("signs/does-not-exist.svg", CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_NullOrEmptyKey_DoesNotThrow()
    {
        await _store.DeleteAsync(null, CancellationToken.None);
        await _store.DeleteAsync(string.Empty, CancellationToken.None);
    }
}
