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
    DateTime CreatedAt,
    string? CouponCode = null,
    decimal DiscountAmount = 0
);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity
);

public record CreateOrderRequest(
    string CustomerEmail,
    List<CreateOrderItemRequest> Items,
    string? CouponCode = null,
    decimal DiscountAmount = 0
);

