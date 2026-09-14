using System.Collections.Concurrent;
using SharedContracts;

namespace ProductApi.Services;

public interface IProductRepository
{
    IEnumerable<ProductDto> GetAll();
    ProductDto? GetById(Guid id);
    ProductDto Create(CreateProductRequest request);
    (bool Success, string? Error, ProductDto? Product) UpdateStock(Guid id, int quantityChange);
}

public class ProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<Guid, ProductDto> _products = new();
    private readonly object _lock = new();

    public ProductRepository()
    {
        // Seed default catalog products
        SeedData();
    }

    private void SeedData()
    {
        var sampleProducts = new[]
        {
            new ProductDto(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "UltraBook Pro 15",
                "High performance laptop with 32GB RAM and 1TB SSD",
                1299.99m,
                50,
                DateTime.UtcNow
            ),
            new ProductDto(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Wireless Mechanical Keyboard",
                "Ergonomic RGB mechanical keyboard with low-latency Bluetooth",
                149.50m,
                100,
                DateTime.UtcNow
            ),
            new ProductDto(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "Precision Ergonomic Mouse",
                "Rechargeable optical mouse with customizable thumb buttons",
                79.99m,
                75,
                DateTime.UtcNow
            ),
            new ProductDto(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "4K UltraHD Monitor 27-inch",
                "IPS panel with HDR400, 144Hz refresh rate, and USB-C hub",
                399.00m,
                30,
                DateTime.UtcNow
            )
        };

        foreach (var product in sampleProducts)
        {
            _products[product.Id] = product;
        }
    }

    public IEnumerable<ProductDto> GetAll() => _products.Values.OrderBy(p => p.Name);

    public ProductDto? GetById(Guid id)
    {
        _products.TryGetValue(id, out var product);
        return product;
    }

    public ProductDto Create(CreateProductRequest request)
    {
        var product = new ProductDto(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Price,
            request.StockQuantity,
            DateTime.UtcNow
        );

        _products[product.Id] = product;
        return product;
    }

    public (bool Success, string? Error, ProductDto? Product) UpdateStock(Guid id, int quantityChange)
    {
        lock (_lock)
        {
            if (!_products.TryGetValue(id, out var current))
            {
                return (false, $"Product with ID '{id}' was not found.", null);
            }

            var newStock = current.StockQuantity + quantityChange;
            if (newStock < 0)
            {
                return (false, $"Insufficient stock for product '{current.Name}'. Current stock: {current.StockQuantity}, Requested reduction: {Math.Abs(quantityChange)}", null);
            }

            var updated = current with { StockQuantity = newStock };
            _products[id] = updated;
            return (true, null, updated);
        }
    }
}
