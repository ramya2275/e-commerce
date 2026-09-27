using FluentAssertions;
using ProductApi.Services;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.ProductApi;

public class ProductRepositoryTests
{
    private readonly ProductRepository _repository;

    public ProductRepositoryTests()
    {
        _repository = new ProductRepository();
    }

    [Fact]
    public void SeedData_InitializesDefaultProducts()
    {
        // Act
        var products = _repository.GetAll().ToList();

        // Assert
        products.Should().NotBeEmpty();
        products.Should().HaveCount(4);
        products.Should().BeInAscendingOrder(p => p.Name);
    }

    [Fact]
    public void GetById_WithExistingId_ReturnsMatchingProduct()
    {
        // Arrange
        var knownId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // Act
        var product = _repository.GetById(knownId);

        // Assert
        product.Should().NotBeNull();
        product!.Id.Should().Be(knownId);
        product.Name.Should().Be("UltraBook Pro 15");
        product.Price.Should().Be(1299.99m);
        product.StockQuantity.Should().Be(50);
    }

    [Fact]
    public void GetById_WithNonExistentId_ReturnsNull()
    {
        // Act
        var product = _repository.GetById(Guid.NewGuid());

        // Assert
        product.Should().BeNull();
    }

    [Fact]
    public void Create_WithValidRequest_AddsNewProductToRepository()
    {
        // Arrange
        var request = new CreateProductRequest("Noise Cancelling Headphones", "Over-ear wireless headphones", 249.99m, 25);

        // Act
        var created = _repository.Create(request);
        var retrieved = _repository.GetById(created.Id);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().NotBeEmpty();
        created.Name.Should().Be(request.Name);
        created.Description.Should().Be(request.Description);
        created.Price.Should().Be(request.Price);
        created.StockQuantity.Should().Be(request.StockQuantity);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
    }

    [Fact]
    public void UpdateStock_IncreaseStock_SuccessfullyIncrementsStockQuantity()
    {
        // Arrange
        var knownId = Guid.Parse("22222222-2222-2222-2222-222222222222"); // Initial stock is 100
        var initialStock = _repository.GetById(knownId)!.StockQuantity;

        // Act
        var (success, error, updated) = _repository.UpdateStock(knownId, 20);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        updated.Should().NotBeNull();
        updated!.StockQuantity.Should().Be(initialStock + 20);

        var freshProduct = _repository.GetById(knownId);
        freshProduct!.StockQuantity.Should().Be(initialStock + 20);
    }

    [Fact]
    public void UpdateStock_DecreaseStockSufficient_SuccessfullyDecrementsStockQuantity()
    {
        // Arrange
        var knownId = Guid.Parse("33333333-3333-3333-3333-333333333333"); // Initial stock is 75

        // Act
        var (success, error, updated) = _repository.UpdateStock(knownId, -15);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        updated.Should().NotBeNull();
        updated!.StockQuantity.Should().Be(60);
    }

    [Fact]
    public void UpdateStock_DecreaseStockInsufficient_ReturnsFailureWithErrorMessage()
    {
        // Arrange
        var knownId = Guid.Parse("44444444-4444-4444-4444-444444444444"); // Initial stock is 30

        // Act
        var (success, error, updated) = _repository.UpdateStock(knownId, -50);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("Insufficient stock");
        updated.Should().BeNull();

        // Verify stock was not changed
        var untouched = _repository.GetById(knownId);
        untouched!.StockQuantity.Should().Be(30);
    }

    [Fact]
    public void UpdateStock_ProductNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var (success, error, updated) = _repository.UpdateStock(nonExistentId, -5);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain($"Product with ID '{nonExistentId}' was not found.");
        updated.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStock_UnderConcurrentReductions_MaintainsThreadSafety()
    {
        // Arrange
        var request = new CreateProductRequest("Limited Edition GPU", "Top tier GPU", 999m, 100);
        var product = _repository.Create(request);
        var concurrencyCount = 50;

        // Act - 50 concurrent tasks each decrementing 1 unit
        var tasks = Enumerable.Range(0, concurrencyCount).Select(_ => Task.Run(() =>
        {
            return _repository.UpdateStock(product.Id, -1);
        }));

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(r => r.Success);
        var finalProduct = _repository.GetById(product.Id);
        finalProduct!.StockQuantity.Should().Be(50);
    }
}
