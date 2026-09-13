using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation.Results;
using K53Guru.Application.Common.Constants;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.RoadSigns.Commands.AddEdit;
using K53Guru.Domain.Entities;
using K53Guru.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.RoadSigns;

/// <summary>
/// Covers spec-1-4-author-combination-signs.md I/O &amp; Edge-Case Matrix rows directly against
/// the production validator + handler, bypassing the DI+Respawn integration harness used
/// elsewhere in this solution (mirrors AddEditQuestionCommandHandlerTests.cs's rationale: no
/// live MSSQL/PostgreSQL instance is reachable in this sandbox).
///
/// Uses a shared-connection SQLite in-memory ApplicationDbContext - schema derived from the EF
/// model via EnsureCreated() - and invokes the real AddEditRoadSignCommandValidator then
/// AddEditRoadSignCommandHandler production classes in sequence, the same way MediatR's
/// ValidationPreProcessor -> handler pipeline would.
///
/// Matrix rows covered:
///   - Create a combination sign (image written via IRoadSignImageStore, ComponentSignCodes set)
///   - Duplicate LegislationCode
///   - Edit without a new image (ImageAssetKey unchanged)
///   - Unresolved component code
///   - Plain sign (no components)
/// </summary>
public class AddEditRoadSignCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IMapper _mapper;

    public AddEditRoadSignCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var schemaContext = new ApplicationDbContext(_options))
        {
            schemaContext.Database.EnsureCreated();
        }

        var mapperConfiguration =
            new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(AddEditRoadSignCommand))));
        _mapper = mapperConfiguration.CreateMapper();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IApplicationDbContextFactory CreateFactory()
    {
        var factoryMock = new Mock<IApplicationDbContextFactory>();
        factoryMock
            .Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (IApplicationDbContext)new ApplicationDbContext(_options));
        return factoryMock.Object;
    }

    private static IRoadSignImageStore CreateImageStore(string returnedAssetKey = "signs/r3-r531.svg")
    {
        var storeMock = new Mock<IRoadSignImageStore>();
        storeMock
            .Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedAssetKey);
        storeMock
            .Setup(s => s.DeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return storeMock.Object;
    }

    private static AddEditRoadSignCommand ValidCommand(string legislationCode = "R3+R531") => new()
    {
        LegislationCode = legislationCode,
        Description = "No Entry except vehicles under 3t",
    };

    private static byte[] ImageBytesOfSize(long size) => new byte[size];

    private async Task<List<ValidationFailure>> ValidateAsync(AddEditRoadSignCommand command)
    {
        var validator = new AddEditRoadSignCommandValidator(CreateFactory());
        var result = await validator.ValidateAsync(command);
        return result.Errors;
    }

    [Fact]
    public async Task Create_CombinationSign_SavesRowWithImageAndComponentCodes()
    {
        // Arrange: seed the two components this composite is assembled from.
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            seedContext.RoadSigns.AddRange(
                new RoadSign { LegislationCode = "R3", Description = "No Entry" },
                new RoadSign { LegislationCode = "R531", Description = "Except vehicles under 3t" });
            await seedContext.SaveChangesAsync();
        }

        var command = ValidCommand();
        command.ComponentSignCodes = "R3,R531";
        command.ImageBytes = new byte[] { 1, 2, 3 };
        command.ImageFileName = "combo.svg";

        // Act
        Assert.Empty(await ValidateAsync(command));

        var imageStore = CreateImageStore("signs/r3-r531.svg");
        var handler = new AddEditRoadSignCommandHandler(CreateFactory(), _mapper, imageStore);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.Data > 0);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.RoadSigns.SingleAsync(r => r.LegislationCode == "R3+R531");
        Assert.Equal("No Entry except vehicles under 3t", saved.Description);
        Assert.Equal("signs/r3-r531.svg", saved.ImageAssetKey);
        Assert.Equal("R3,R531", saved.ComponentSignCodes);
    }

    [Fact]
    public async Task Create_PlainSign_NoComponents_SavedExactlyLikeTodaysSeededSigns()
    {
        // Arrange
        var command = ValidCommand("R1");

        // Act
        Assert.Empty(await ValidateAsync(command));

        var handler = new AddEditRoadSignCommandHandler(CreateFactory(), _mapper, CreateImageStore());
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.RoadSigns.SingleAsync(r => r.LegislationCode == "R1");
        Assert.Null(saved.ComponentSignCodes);
        Assert.Null(saved.ImageAssetKey);
    }

    [Fact]
    public async Task Create_DuplicateLegislationCode_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            seedContext.RoadSigns.Add(new RoadSign { LegislationCode = "R1", Description = "Stop" });
            await seedContext.SaveChangesAsync();
        }

        var command = ValidCommand("R1");

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditRoadSignCommand.LegislationCode));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(1, await verifyContext.RoadSigns.CountAsync());
    }

    [Fact]
    public async Task Edit_SameLegislationCodeAsSelf_NotRejectedAsDuplicate()
    {
        // Arrange: uniqueness must exclude the row being edited.
        int id;
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            var sign = new RoadSign { LegislationCode = "R1", Description = "Stop" };
            seedContext.RoadSigns.Add(sign);
            await seedContext.SaveChangesAsync();
            id = sign.Id;
        }

        var command = ValidCommand("R1");
        command.Id = id;
        command.Description = "Stop - updated";

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Empty(failures);
    }

    [Fact]
    public async Task Edit_WithoutNewImage_ImageAssetKeyUnchanged_OtherFieldsUpdate()
    {
        // Arrange
        int id;
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            var sign = new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.svg" };
            seedContext.RoadSigns.Add(sign);
            await seedContext.SaveChangesAsync();
            id = sign.Id;
        }

        var command = ValidCommand("R1");
        command.Id = id;
        command.Description = "Stop - updated description";
        command.ImageAssetKey = "signs/r1.svg"; // as returned by GetRoadSignByIdQuery, no new file picked
        command.ImageBytes = null;

        // Act
        Assert.Empty(await ValidateAsync(command));

        var imageStore = CreateImageStore();
        var handler = new AddEditRoadSignCommandHandler(CreateFactory(), _mapper, imageStore);
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var updated = await verifyContext.RoadSigns.SingleAsync(r => r.Id == id);
        Assert.Equal("Stop - updated description", updated.Description);
        Assert.Equal("signs/r1.svg", updated.ImageAssetKey);

        Mock.Get(imageStore).Verify(
            s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_UnresolvedComponentCode_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: "R3" exists, but "R531" does not - the resolution is Ask-First hard-reject,
        // matching the SignRef precedent (spec-1-4-author-combination-signs.md).
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            seedContext.RoadSigns.Add(new RoadSign { LegislationCode = "R3", Description = "No Entry" });
            await seedContext.SaveChangesAsync();
        }

        var command = ValidCommand();
        command.ComponentSignCodes = "R3,R531";

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditRoadSignCommand.ComponentSignCodes));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(1, await verifyContext.RoadSigns.CountAsync());
    }

    [Fact]
    public async Task Create_LegislationCodeDiffersOnlyByCase_RejectedAsDuplicate()
    {
        // Arrange: RoadSignImageStore lowercases LegislationCode to derive its on-disk file
        // name, so "R1" and "r1" must not be allowed to coexist - they would silently overwrite
        // each other's image file.
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            seedContext.RoadSigns.Add(new RoadSign { LegislationCode = "R1", Description = "Stop" });
            await seedContext.SaveChangesAsync();
        }

        var command = ValidCommand("r1");

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditRoadSignCommand.LegislationCode));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(1, await verifyContext.RoadSigns.CountAsync());
    }

    [Fact]
    public async Task Create_LegislationCodeWithSurroundingWhitespace_TrimmedBeforeSave()
    {
        // Arrange
        var command = ValidCommand("  R1  ");

        // Act
        Assert.Empty(await ValidateAsync(command));

        var handler = new AddEditRoadSignCommandHandler(CreateFactory(), _mapper, CreateImageStore());
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var saved = await verifyContext.RoadSigns.SingleAsync();
        Assert.Equal("R1", saved.LegislationCode);
    }

    [Fact]
    public async Task Edit_ComponentSignCodesReferencesItself_RejectedBeforeSave()
    {
        // Arrange
        int id;
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            var sign = new RoadSign { LegislationCode = "R3+R531", Description = "No Entry except <3t" };
            seedContext.RoadSigns.Add(sign);
            await seedContext.SaveChangesAsync();
            id = sign.Id;
        }

        var command = ValidCommand("R3+R531");
        command.Id = id;
        // A composite cannot legitimately list its own LegislationCode as one of its own
        // components, even though "R3+R531" trivially "resolves" (it matches itself).
        command.ComponentSignCodes = "R3+R531";

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditRoadSignCommand.ComponentSignCodes));
    }

    [Fact]
    public async Task Create_ImageBytesExceedsMaxAllowedSize_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange
        var command = ValidCommand();
        command.ImageBytes = ImageBytesOfSize(GlobalVariables.MaxAllowedSize + 1);
        command.ImageFileName = "too-big.svg";

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditRoadSignCommand.ImageBytes));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.RoadSigns.CountAsync());
    }

    [Fact]
    public async Task Create_ImageFileNameHasDisallowedExtension_RejectedBeforeSave_NothingPersisted()
    {
        // Arrange: MudFileUpload's Accept is a client-side hint only - the server must reject an
        // extension outside RoadSignFormDialog.razor's allow-list on a direct MediatR request too.
        var command = ValidCommand();
        command.ImageBytes = new byte[] { 1, 2, 3 };
        command.ImageFileName = "payload.exe";

        // Act
        var failures = await ValidateAsync(command);

        // Assert
        Assert.Contains(failures, f => f.PropertyName == nameof(AddEditRoadSignCommand.ImageFileName));

        await using var verifyContext = new ApplicationDbContext(_options);
        Assert.Equal(0, await verifyContext.RoadSigns.CountAsync());
    }

    [Fact]
    public async Task Edit_NewImageReplacesOldOne_OldFileDeletedAfterSaveSucceeds()
    {
        // Arrange
        int id;
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            var sign = new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1-old.svg" };
            seedContext.RoadSigns.Add(sign);
            await seedContext.SaveChangesAsync();
            id = sign.Id;
        }

        var command = ValidCommand("R1");
        command.Id = id;
        command.ImageAssetKey = "signs/r1-old.svg";
        command.ImageBytes = new byte[] { 9, 9, 9 };
        command.ImageFileName = "new.svg";

        Assert.Empty(await ValidateAsync(command));

        var imageStore = CreateImageStore("signs/r1.svg");
        var handler = new AddEditRoadSignCommandHandler(CreateFactory(), _mapper, imageStore);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);

        await using var verifyContext = new ApplicationDbContext(_options);
        var updated = await verifyContext.RoadSigns.SingleAsync(r => r.Id == id);
        Assert.Equal("signs/r1.svg", updated.ImageAssetKey);

        Mock.Get(imageStore).Verify(s => s.DeleteAsync("signs/r1-old.svg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Edit_NewImageSavesToSamePath_OldFileNotDeleted()
    {
        // Arrange: editing without changing LegislationCode re-derives the same file name, so
        // the "old" and "new" ImageAssetKey are identical - nothing to delete.
        int id;
        await using (var seedContext = new ApplicationDbContext(_options))
        {
            var sign = new RoadSign { LegislationCode = "R1", Description = "Stop", ImageAssetKey = "signs/r1.svg" };
            seedContext.RoadSigns.Add(sign);
            await seedContext.SaveChangesAsync();
            id = sign.Id;
        }

        var command = ValidCommand("R1");
        command.Id = id;
        command.ImageAssetKey = "signs/r1.svg";
        command.ImageBytes = new byte[] { 9, 9, 9 };
        command.ImageFileName = "replacement.svg";

        Assert.Empty(await ValidateAsync(command));

        var imageStore = CreateImageStore("signs/r1.svg");
        var handler = new AddEditRoadSignCommandHandler(CreateFactory(), _mapper, imageStore);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Succeeded);
        Mock.Get(imageStore).Verify(s => s.DeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
