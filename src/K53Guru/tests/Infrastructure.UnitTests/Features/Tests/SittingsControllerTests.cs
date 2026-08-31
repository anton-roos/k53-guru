using K53Guru.Application.Features.Tests.DTOs;
using K53Guru.Application.Features.Tests.Queries.AvailableSittings;
using K53Guru.Domain.Enums;
using K53Guru.Server.UI.Controllers.Api.V1;
using MediatR;
using Moq;
using Xunit;

namespace K53Guru.Infrastructure.UnitTests.Features.Tests;

/// <summary>
/// Covers spec-3-1-discover-available-sittings.md's controller layer directly - the first
/// controller-level test in the repo (see deferred-work.md's note that no WebApplicationFactory/
/// TestServer harness exists anywhere yet). Every other test targeting this story calls
/// GetAvailableSittingsQueryHandler directly, bypassing SittingsController entirely, so nothing
/// would previously have caught a wrong HTTP verb/route, a controller that never calls
/// ISender.Send, or one that wraps/mangles the mediator's response. This test does not exercise
/// the ASP.NET Core pipeline (routing, [EnableRateLimiting], model binding) - only the
/// controller's own logic, via direct construction + a mocked ISender.
/// </summary>
public class SittingsControllerTests
{
    [Fact]
    public async Task GetAvailableSittings_SendsQuery_AndReturnsMediatorResultUnchanged()
    {
        // Arrange
        var expected = new List<AvailableSittingDto>
        {
            new() { Id = 42, Name = "Code1 Sitting", Codes = LicenceCode.Code1 }
        };

        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(s => s.Send(It.IsAny<GetAvailableSittingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new SittingsController(senderMock.Object);

        // Act
        var actionResult = await controller.GetAvailableSittings();

        // Assert
        senderMock.Verify(
            s => s.Send(It.IsAny<GetAvailableSittingsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Same(expected, actionResult.Value);
    }
}
