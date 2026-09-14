namespace SharedContracts;

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice
);

public record OrderDto(
    Guid Id,
    string CustomerEmail,
    List<OrderItemDto> Items,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAt
);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity
);

public record CreateOrderRequest(
    string CustomerEmail,
    List<CreateOrderItemRequest> Items
);
