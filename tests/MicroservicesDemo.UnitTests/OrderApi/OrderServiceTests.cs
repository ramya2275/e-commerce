using FluentAssertions;
using Moq;
using OrderApi.Services;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.OrderApi;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IProductServiceClient> _productClientMock;
    private readonly Mock<ICouponServiceClient> _couponClientMock;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _productClientMock = new Mock<IProductServiceClient>();
        _couponClientMock = new Mock<ICouponServiceClient>();

        _orderService = new OrderService(
            _orderRepoMock.Object,
            _productClientMock.Object,
            _couponClientMock.Object
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateOrderAsync_WhenCustomerEmailIsMissing_ReturnsBadRequest(string? email)
    {
        // Arrange
        var request = new CreateOrderRequest(
            email!,
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), 1) }
        );

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        order.Should().BeNull();
        error.Should().Be("Customer email is required.");
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenItemsListIsEmpty_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest("customer@example.com", new List<CreateOrderItemRequest>());

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        error.Should().Be("Order must contain at least one item.");
        statusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task CreateOrderAsync_WhenAnyItemQuantityIsZeroOrNegative_ReturnsBadRequest(int invalidQty)
    {
        // Arrange
        var request = new CreateOrderRequest(
            "customer@example.com",
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), invalidQty) }
        );

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        error.Should().Be("Each item quantity must be at least 1.");
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenProductDoesNotExist_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new CreateOrderRequest(
            "customer@example.com",
            new List<CreateOrderItemRequest> { new(productId, 1) }
        );

        _productClientMock
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Product not found"));

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("Product validation failed");
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenProductHasInsufficientStock_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new ProductDto(productId, "Curved Monitor", "34-inch ultrawide", 500m, 2, DateTime.UtcNow);
        var request = new CreateOrderRequest(
            "customer@example.com",
            new List<CreateOrderItemRequest> { new(productId, 5) } // Requesting 5 when only 2 available
        );

        _productClientMock
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product, null));

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("Insufficient stock for product 'Curved Monitor'");
        error.Should().Contain("Available: 2, Requested: 5");
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenCouponValidationFails_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new ProductDto(productId, "Keycap Set", "PBT keycaps", 40m, 10, DateTime.UtcNow);
        var request = new CreateOrderRequest(
            "customer@example.com",
            new List<CreateOrderItemRequest> { new(productId, 1) },
            "MIN50REQUIRED"
        );

        _productClientMock
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product, null));

        var couponResult = new CouponValidationResult(false, 0, 40m, "Order total of $40.00 is below the minimum required amount.");
        _couponClientMock
            .Setup(c => c.ValidateCouponAsync("MIN50REQUIRED", 40m, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, couponResult, null));

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("Coupon validation failed");
        statusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateOrderAsync_WithoutCoupon_SuccessfullyDeductsStockAndCreatesOrder()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new ProductDto(productId, "Studio Microphone", "USB condenser mic", 120m, 15, DateTime.UtcNow);
        var request = new CreateOrderRequest(
            "buyer@example.com",
            new List<CreateOrderItemRequest> { new(productId, 2) }
        );

        _productClientMock
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product, null));

        _productClientMock
            .Setup(c => c.UpdateStockAsync(productId, -2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product with { StockQuantity = 13 }, null));

        _orderRepoMock
            .Setup(r => r.Create(It.IsAny<OrderDto>()))
            .Returns<OrderDto>(o => o);

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeTrue();
        statusCode.Should().Be(201);
        error.Should().BeNull();
        order.Should().NotBeNull();
        order!.CustomerEmail.Should().Be("buyer@example.com");
        order.TotalAmount.Should().Be(240m); // 2 * $120
        order.Status.Should().Be("Confirmed");
        order.CouponCode.Should().BeNull();
        order.DiscountAmount.Should().Be(0);

        _productClientMock.Verify(c => c.UpdateStockAsync(productId, -2, It.IsAny<CancellationToken>()), Times.Once);
        _orderRepoMock.Verify(r => r.Create(It.IsAny<OrderDto>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidCoupon_AppliesDiscountAndDeductsStock()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var product = new ProductDto(productId, "Ergonomic Desk", "Standing motorized desk", 500m, 10, DateTime.UtcNow);
        var request = new CreateOrderRequest(
            "buyer@example.com",
            new List<CreateOrderItemRequest> { new(productId, 1) },
            "WELCOME10"
        );

        _productClientMock
            .Setup(c => c.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product, null));

        var couponResult = new CouponValidationResult(true, 50m, 450m, "10% applied");
        _couponClientMock
            .Setup(c => c.ValidateCouponAsync("WELCOME10", 500m, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, couponResult, null));

        _productClientMock
            .Setup(c => c.UpdateStockAsync(productId, -1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product with { StockQuantity = 9 }, null));

        _orderRepoMock
            .Setup(r => r.Create(It.IsAny<OrderDto>()))
            .Returns<OrderDto>(o => o);

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeTrue();
        statusCode.Should().Be(201);
        order.Should().NotBeNull();
        order!.TotalAmount.Should().Be(450m);
        order.DiscountAmount.Should().Be(50m);
        order.CouponCode.Should().Be("WELCOME10");
    }

    [Fact]
    public async Task CreateOrderAsync_WhenSecondItemDeductionFails_ExecutesCompensatingRollback()
    {
        // Arrange: 2 items in order
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var product1 = new ProductDto(product1Id, "Webcam 4K", "UltraHD streaming camera", 150m, 10, DateTime.UtcNow);
        var product2 = new ProductDto(product2Id, "Ring Light", "LED light with stand", 50m, 5, DateTime.UtcNow);

        var request = new CreateOrderRequest(
            "streamer@example.com",
            new List<CreateOrderItemRequest>
            {
                new(product1Id, 2),
                new(product2Id, 1)
            }
        );

        _productClientMock
            .Setup(c => c.GetProductAsync(product1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product1, null));
        _productClientMock
            .Setup(c => c.GetProductAsync(product2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product2, null));

        // First item stock deduction succeeds
        _productClientMock
            .Setup(c => c.UpdateStockAsync(product1Id, -2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product1 with { StockQuantity = 8 }, null));

        // Second item stock deduction fails!
        _productClientMock
            .Setup(c => c.UpdateStockAsync(product2Id, -1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, null, "Database timeout updating stock."));

        // Compensating rollback mock setup for product1
        _productClientMock
            .Setup(c => c.UpdateStockAsync(product1Id, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, product1, null));

        // Act
        var (success, order, error, statusCode) = await _orderService.CreateOrderAsync(request);

        // Assert
        success.Should().BeFalse();
        order.Should().BeNull();
        statusCode.Should().Be(500);

        // VERIFY COMPENSATING TRANSACTION: Product 1 stock restored with +2
        _productClientMock.Verify(c => c.UpdateStockAsync(product1Id, 2, It.IsAny<CancellationToken>()), Times.Once);

        // VERIFY Order was NEVER saved in repository
        _orderRepoMock.Verify(r => r.Create(It.IsAny<OrderDto>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderNotFound_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _orderRepoMock
            .Setup(r => r.GetById(nonExistentId))
            .Returns((OrderDto?)null);

        // Act
        var (success, order, error, statusCode) = await _orderService.CancelOrderAsync(nonExistentId);

        // Assert
        success.Should().BeFalse();
        statusCode.Should().Be(404);
        error.Should().Contain($"Order with ID '{nonExistentId}' was not found.");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderAlreadyCancelled_ReturnsBadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new OrderDto(orderId, "user@test.com", new List<OrderItemDto>(), 100m, "Cancelled", DateTime.UtcNow);

        _orderRepoMock
            .Setup(r => r.GetById(orderId))
            .Returns(order);

        // Act
        var (success, _, error, statusCode) = await _orderService.CancelOrderAsync(orderId);

        // Assert
        success.Should().BeFalse();
        statusCode.Should().Be(400);
        error.Should().Be("Order has already been cancelled.");
    }

    [Fact]
    public async Task CancelOrderAsync_WhenOrderIsConfirmed_RestoresInventoryAndUpdatesStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var prod1Id = Guid.NewGuid();
        var prod2Id = Guid.NewGuid();
        var items = new List<OrderItemDto>
        {
            new(prod1Id, "Product A", 50m, 3, 150m),
            new(prod2Id, "Product B", 25m, 2, 50m)
        };
        var activeOrder = new OrderDto(orderId, "user@test.com", items, 200m, "Confirmed", DateTime.UtcNow);
        var cancelledOrder = activeOrder with { Status = "Cancelled" };

        _orderRepoMock
            .Setup(r => r.GetById(orderId))
            .Returns(activeOrder);

        _orderRepoMock
            .Setup(r => r.UpdateStatus(orderId, "Cancelled"))
            .Returns(cancelledOrder);

        // Act
        var (success, order, error, statusCode) = await _orderService.CancelOrderAsync(orderId);

        // Assert
        success.Should().BeTrue();
        statusCode.Should().Be(200);
        order.Should().NotBeNull();
        order!.Status.Should().Be("Cancelled");

        // Verify inventory restoration calls with positive quantities
        _productClientMock.Verify(c => c.UpdateStockAsync(prod1Id, 3, It.IsAny<CancellationToken>()), Times.Once);
        _productClientMock.Verify(c => c.UpdateStockAsync(prod2Id, 2, It.IsAny<CancellationToken>()), Times.Once);
        _orderRepoMock.Verify(r => r.UpdateStatus(orderId, "Cancelled"), Times.Once);
    }
}
