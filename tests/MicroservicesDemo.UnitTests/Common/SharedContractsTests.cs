using FluentAssertions;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.Common;

public class SharedContractsTests
{
    [Fact]
    public void ProductDto_WithExpression_ReturnsModifiedCopy()
    {
        // Arrange
        var original = new ProductDto(
            Guid.NewGuid(),
            "Mechanical Keyboard",
            "Wireless RGB keyboard",
            99.99m,
            50,
            DateTime.UtcNow
        );

        // Act
        var updated = original with { StockQuantity = 45 };

        // Assert
        updated.Id.Should().Be(original.Id);
        updated.Name.Should().Be(original.Name);
        updated.StockQuantity.Should().Be(45);
        original.StockQuantity.Should().Be(50);
    }

    [Fact]
    public void OrderDto_Creation_PopulatesAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = "buyer@example.com";
        var items = new List<OrderItemDto>
        {
            new(Guid.NewGuid(), "Laptop", 1200m, 1, 1200m)
        };
        var createdAt = DateTime.UtcNow;

        // Act
        var order = new OrderDto(
            id,
            email,
            items,
            1080m,
            "Confirmed",
            createdAt,
            "SAVE10",
            120m
        );

        // Assert
        order.Id.Should().Be(id);
        order.CustomerEmail.Should().Be(email);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(1080m);
        order.Status.Should().Be("Confirmed");
        order.CouponCode.Should().Be("SAVE10");
        order.DiscountAmount.Should().Be(120m);
    }

    [Fact]
    public void CouponDto_WithExpression_AllowsStatusToggling()
    {
        // Arrange
        var coupon = new CouponDto(
            Guid.NewGuid(),
            "WELCOME10",
            10m,
            50m,
            DateTime.UtcNow.AddDays(30),
            true
        );

        // Act
        var deactivated = coupon with { IsActive = false };

        // Assert
        deactivated.IsActive.Should().BeFalse();
        coupon.IsActive.Should().BeTrue();
    }
}
