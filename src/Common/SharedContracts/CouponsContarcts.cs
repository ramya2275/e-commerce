namespace SharedContracts;

public record CouponDto(
    Guid Id,
    string Code,
    decimal DiscountPercentage, // or fixed amount
    decimal MinimumOrderAmount,
    DateTime ExpiryDate,
    bool IsActive
);

public record CreateCouponRequest(
    string Code,
    decimal DiscountPercentage,
    decimal MinimumOrderAmount,
    DateTime ExpiryDate
);

public record ValidateCouponRequest(
    string Code,
    decimal OrderTotal
);

public record CouponValidationResult(
    bool IsValid,
    decimal DiscountAmount,
    decimal FinalTotal,
    string? Message
);
