using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using OrderApi.Services;
using Xunit;

namespace MicroservicesDemo.UnitTests.OrderApi;

public class ProductServiceHealthCheckTests
{
    private readonly Mock<IProductServiceClient> _productClientMock;
    private readonly ProductServiceHealthCheck _healthCheck;

    public ProductServiceHealthCheckTests()
    {
        _productClientMock = new Mock<IProductServiceClient>();
        _healthCheck = new ProductServiceHealthCheck(_productClientMock.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenProductServiceIsReachable_ReturnsHealthy()
    {
        // Arrange
        _productClientMock
            .Setup(c => c.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("healthy and reachable");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenProductServiceIsDown_ReturnsDegraded()
    {
        // Arrange
        _productClientMock
            .Setup(c => c.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("unreachable or unhealthy");
    }
}
