using FluentAssertions;
using OrderApi.Services;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.OrderApi;

public class OrderRepositoryTests
{
    private readonly OrderRepository _repository;

    public OrderRepositoryTests()
    {
        _repository = new OrderRepository();
    }

    [Fact]
    public void CreateAndGetById_StoresAndRetrievesOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new OrderDto(
            orderId,
            "testuser@domain.com",
            new List<OrderItemDto> { new(Guid.NewGuid(), "Gaming Mouse", 79.99m, 1, 79.99m) },
            79.99m,
            "Confirmed",
            DateTime.UtcNow
        );

        // Act
        var created = _repository.Create(order);
        var retrieved = _repository.GetById(orderId);

        // Assert
        created.Should().BeEquivalentTo(order);
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(order);
    }

    [Fact]
    public void GetById_WhenNotFound_ReturnsNull()
    {
        // Act
        var result = _repository.GetById(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAll_WithoutFilter_ReturnsAllOrdersDescendingByCreatedAt()
    {
        // Arrange
        var older = new OrderDto(
            Guid.NewGuid(),
            "alice@example.com",
            new List<OrderItemDto>(),
            100m,
            "Confirmed",
            DateTime.UtcNow.AddMinutes(-10)
        );
        var newer = new OrderDto(
            Guid.NewGuid(),
            "bob@example.com",
            new List<OrderItemDto>(),
            200m,
            "Confirmed",
            DateTime.UtcNow
        );

        _repository.Create(older);
        _repository.Create(newer);

        // Act
        var all = _repository.GetAll().ToList();

        // Assert
        all.Should().HaveCount(2);
        all[0].Id.Should().Be(newer.Id);
        all[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public void GetAll_WithCustomerEmail_FiltersOrdersCaseInsensitively()
    {
        // Arrange
        var user1Order = new OrderDto(
            Guid.NewGuid(),
            "target@domain.com",
            new List<OrderItemDto>(),
            50m,
            "Confirmed",
            DateTime.UtcNow
        );
        var user2Order = new OrderDto(
            Guid.NewGuid(),
            "other@domain.com",
            new List<OrderItemDto>(),
            75m,
            "Confirmed",
            DateTime.UtcNow
        );

        _repository.Create(user1Order);
        _repository.Create(user2Order);

        // Act
        var filtered = _repository.GetAll("TARGET@DOMAIN.COM").ToList();

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].CustomerEmail.Should().Be("target@domain.com");
    }

    [Fact]
    public void UpdateStatus_WhenOrderExists_UpdatesAndReturnsOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new OrderDto(
            orderId,
            "cancelme@example.com",
            new List<OrderItemDto>(),
            100m,
            "Confirmed",
            DateTime.UtcNow
        );
        _repository.Create(order);

        // Act
        var updated = _repository.UpdateStatus(orderId, "Cancelled");

        // Assert
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Cancelled");

        var retrieved = _repository.GetById(orderId);
        retrieved!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public void UpdateStatus_WhenOrderDoesNotExist_ReturnsNull()
    {
        // Act
        var result = _repository.UpdateStatus(Guid.NewGuid(), "Cancelled");

        // Assert
        result.Should().BeNull();
    }
}
