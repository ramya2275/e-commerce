namespace SharedContracts;

public record ProductDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAt
);

public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity
);

public record UpdateStockRequest(
    int QuantityChange
);
