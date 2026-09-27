using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using OrderApi.Services;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.OrderApi;

public class ProductServiceClientTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<ILogger<ProductServiceClient>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly ProductServiceClient _client;

    public ProductServiceClientTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<ProductServiceClient>>();

        _httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5101")
        };

        _client = new ProductServiceClient(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetProductAsync_WhenProductExists_ReturnsProductSuccessfully()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new ProductDto(productId, "4K Monitor", "27 inch UHD", 350m, 10, DateTime.UtcNow);
        var envelope = ApiResponse<ProductDto>.Ok(product);
        var json = JsonSerializer.Serialize(envelope);

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().EndsWith($"/api/products/{productId}")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var (success, result, error) = await _client.GetProductAsync(productId);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.Id.Should().Be(productId);
        result.Name.Should().Be("4K Monitor");
    }

    [Fact]
    public async Task GetProductAsync_WhenProductNotFound_ReturnsNotFoundErrorMessage()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().EndsWith($"/api/products/{productId}")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act
        var (success, result, error) = await _client.GetProductAsync(productId);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
        error.Should().Contain("does not exist in catalog");
    }

    [Fact]
    public async Task GetProductAsync_WhenNetworkFails_CatchesExceptionAndReturnsGracefulError()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var (success, result, error) = await _client.GetProductAsync(productId);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
        error.Should().Contain("Unable to reach Product Service");
    }

    [Fact]
    public async Task UpdateStockAsync_WhenSuccessful_ReturnsUpdatedProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var updatedProduct = new ProductDto(productId, "4K Monitor", "27 inch UHD", 350m, 8, DateTime.UtcNow);
        var envelope = ApiResponse<ProductDto>.Ok(updatedProduct);
        var json = JsonSerializer.Serialize(envelope);

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put &&
                    req.RequestUri!.ToString().EndsWith($"/api/products/{productId}/stock")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var (success, product, error) = await _client.UpdateStockAsync(productId, -2);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        product.Should().NotBeNull();
        product!.StockQuantity.Should().Be(8);
    }

    [Fact]
    public async Task UpdateStockAsync_WhenInsufficientStock_ReturnsFailureWithError()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var envelope = ApiResponse<ProductDto>.Fail("Insufficient stock for product.");
        var json = JsonSerializer.Serialize(envelope);

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var (success, product, error) = await _client.UpdateStockAsync(productId, -99);

        // Assert
        success.Should().BeFalse();
        product.Should().BeNull();
        error.Should().Contain("Insufficient stock");
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceReturns200_ReturnsTrue()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith("/health/live")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Act
        var isHealthy = await _client.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceThrowsException_ReturnsFalse()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Server offline"));

        // Act
        var isHealthy = await _client.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeFalse();
    }
}
