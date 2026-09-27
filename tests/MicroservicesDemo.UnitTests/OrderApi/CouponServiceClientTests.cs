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

public class CouponServiceClientTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<ILogger<CouponServiceClient>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly CouponServiceClient _client;

    public CouponServiceClientTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<CouponServiceClient>>();

        _httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5103")
        };

        _client = new CouponServiceClient(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task ValidateCouponAsync_WhenCouponIsValid_ReturnsValidationResult()
    {
        // Arrange
        var couponResult = new CouponValidationResult(true, 15m, 85m, "Coupon applied successfully!");
        var envelope = ApiResponse<CouponValidationResult>.Ok(couponResult);
        var json = JsonSerializer.Serialize(envelope);

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().EndsWith("/api/coupons/validate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        var (success, result, error) = await _client.ValidateCouponAsync("WELCOME10", 100m);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
        result.DiscountAmount.Should().Be(15m);
        result.FinalTotal.Should().Be(85m);
    }

    [Fact]
    public async Task ValidateCouponAsync_WhenCouponIsInvalid_ReturnsError()
    {
        // Arrange
        var envelope = ApiResponse<CouponValidationResult>.Fail("Coupon 'EXPIRED' has expired.");
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
        var (success, result, error) = await _client.ValidateCouponAsync("EXPIRED", 100m);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
        error.Should().Contain("has expired");
    }

    [Fact]
    public async Task ValidateCouponAsync_WhenNetworkFails_ReturnsGracefulError()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection timed out"));

        // Act
        var (success, result, error) = await _client.ValidateCouponAsync("WELCOME10", 100m);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
        error.Should().Contain("Unable to reach Coupon Service");
    }
}
