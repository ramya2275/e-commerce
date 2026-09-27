using SharedContracts;

namespace OrderApi.Services;

public interface IOrderService
{
    Task<(bool Success, OrderDto? Order, string? Error, int StatusCode)> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<(bool Success, OrderDto? Order, string? Error, int StatusCode)> CancelOrderAsync(Guid id, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly IProductServiceClient _productClient;
    private readonly ICouponServiceClient _couponClient;

    public OrderService(IOrderRepository repo, IProductServiceClient productClient, ICouponServiceClient couponClient)
    {
        _repo = repo;
        _productClient = productClient;
        _couponClient = couponClient;
    }

    public async Task<(bool Success, OrderDto? Order, string? Error, int StatusCode)> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            return (false, null, "Customer email is required.", 400);
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return (false, null, "Order must contain at least one item.", 400);
        }

        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return (false, null, "Each item quantity must be at least 1.", 400);
        }

        // Step 1: Pre-validate each product exists and check stock via ProductApi
        var orderItems = new List<OrderItemDto>();
        decimal subtotal = 0;

        foreach (var item in request.Items)
        {
            var (success, product, error) = await _productClient.GetProductAsync(item.ProductId, ct);
            if (!success || product is null)
            {
                return (false, null, $"Product validation failed: {error}", 400);
            }

            if (product.StockQuantity < item.Quantity)
            {
                return (false, null,
                    $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}", 400);
            }

            var itemTotal = product.Price * item.Quantity;
            subtotal += itemTotal;

            orderItems.Add(new OrderItemDto(
                product.Id,
                product.Name,
                product.Price,
                item.Quantity,
                itemTotal
            ));
        }

        // Step 2: Validate coupon (if provided) via CouponApi
        decimal discountAmount = 0;
        string? appliedCouponCode = null;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var (couponSuccess, couponResult, couponError) = await _couponClient.ValidateCouponAsync(request.CouponCode, subtotal, ct);
            if (!couponSuccess || couponResult is null || !couponResult.IsValid)
            {
                return (false, null, $"Coupon validation failed: {couponError ?? couponResult?.Message}", 400);
            }

            discountAmount = couponResult.DiscountAmount;
            appliedCouponCode = request.CouponCode.Trim().ToUpperInvariant();
        }

        var finalAmount = Math.Max(0, subtotal - discountAmount);

        // Step 3: Deduct stock via ProductApi
        var deductedItems = new List<(Guid ProductId, int Quantity)>();
        foreach (var item in request.Items)
        {
            var (success, _, error) = await _productClient.UpdateStockAsync(item.ProductId, -item.Quantity, ct);
            if (!success)
            {
                // Rollback previously deducted items (compensating transaction)
                foreach (var deducted in deductedItems)
                {
                    await _productClient.UpdateStockAsync(deducted.ProductId, deducted.Quantity, ct);
                }

                return (false, null, error ?? "Failed to deduct inventory stock.", 500);
            }

            deductedItems.Add((item.ProductId, item.Quantity));
        }

        // Step 4: Record Order with applied coupon and discount
        var order = new OrderDto(
            Guid.NewGuid(),
            request.CustomerEmail,
            orderItems,
            finalAmount,
            "Confirmed",
            DateTime.UtcNow,
            appliedCouponCode,
            discountAmount
        );

        var created = _repo.Create(order);
        return (true, created, null, 201);
    }

    public async Task<(bool Success, OrderDto? Order, string? Error, int StatusCode)> CancelOrderAsync(Guid id, CancellationToken ct = default)
    {
        var order = _repo.GetById(id);
        if (order is null)
        {
            return (false, null, $"Order with ID '{id}' was not found.", 404);
        }

        if (order.Status == "Cancelled")
        {
            return (false, null, "Order has already been cancelled.", 400);
        }

        // Restore stock for all items in order
        foreach (var item in order.Items)
        {
            await _productClient.UpdateStockAsync(item.ProductId, item.Quantity, ct);
        }

        var updated = _repo.UpdateStatus(id, "Cancelled");
        return (true, updated, null, 200);
    }
}
