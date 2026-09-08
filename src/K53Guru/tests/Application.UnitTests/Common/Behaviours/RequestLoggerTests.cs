using System.Threading;
using System.Threading.Tasks;
using K53Guru.Application.Common.Interfaces.Identity;
using K53Guru.Application.Features.Tests.Commands.Publish;
using K53Guru.Application.Pipeline.PreProcessors;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace K53Guru.Application.UnitTests.Common.Behaviours;

public class RequestLoggerTests
{
    private readonly Mock<IUserContextAccessor> _userContextAccessor;
    private readonly Mock<IIdentityService> _identityService;
    private readonly Mock<ILogger<PublishTestCommand>> _logger;

    public RequestLoggerTests()
    {
        _userContextAccessor = new Mock<IUserContextAccessor>();
        _identityService = new Mock<IIdentityService>();
        _logger = new Mock<ILogger<PublishTestCommand>>();
    }

    [Test]
    public async Task ShouldCallGetUserNameAsyncOnceIfAuthenticated()
    {
        var userContext = new UserContext("Administrator", "Administrator");
        _userContextAccessor.Setup(x => x.Current).Returns(userContext);
        var requestLogger = new LoggingPreProcessor<PublishTestCommand>(_logger.Object, _userContextAccessor.Object);
        await requestLogger.Process(
            new PublishTestCommand { Id = 1 },
            new CancellationToken());
        _userContextAccessor.Verify(i => i.Current, Times.Once);
    }

    [Test]
    public async Task ShouldNotCallGetUserNameAsyncOnceIfUnauthenticated()
    {
        var requestLogger = new LoggingPreProcessor<PublishTestCommand>(_logger.Object, _userContextAccessor.Object);
        await requestLogger.Process(
            new PublishTestCommand { Id = 1 },
            new CancellationToken());
        _identityService.Verify(i => i.GetUserNameAsync(It.IsAny<string>(), CancellationToken.None), Times.Never);
    }
}
