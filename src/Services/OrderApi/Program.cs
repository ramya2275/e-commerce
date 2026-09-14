using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderApi.Services;
using SharedContracts;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

// Configure HttpClient for inter-service communication to ProductApi
var productApiBaseUrl = builder.Configuration["Services:ProductApi:Url"] ?? "http://localhost:5101";
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(productApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddOpenApi();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("OrderApi service is alive."))
    .AddCheck<ProductServiceHealthCheck>("product_service_dependency");

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.MapOpenApi();
}

// -------------------------------------------------------------
// Kubernetes Health Probes
// -------------------------------------------------------------
// Liveness probe: returns 200 OK if service process is alive
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .WithName("OrderLivenessProbe")
   .WithTags("Health");

// Readiness probe: returns 200 OK if service and dependencies are ready
app.MapHealthChecks("/health/ready");

// -------------------------------------------------------------
// REST API Endpoints for Orders
// -------------------------------------------------------------
var ordersGroup = app.MapGroup("/api/orders").WithTags("Orders");

// GET /api/orders - List orders with optional customerEmail filter
ordersGroup.MapGet("/", (IOrderRepository repo, string? customerEmail) =>
{
    var orders = repo.GetAll(customerEmail);
    return Results.Ok(ApiResponse<IEnumerable<OrderDto>>.Ok(orders));
})
.WithName("GetOrders")
.WithSummary("Retrieve orders with optional customer email filtering");

// GET /api/orders/{id} - Get order details by GUID
ordersGroup.MapGet("/{id:guid}", (Guid id, IOrderRepository repo) =>
{
    var order = repo.GetById(id);
    return order is not null
        ? Results.Ok(ApiResponse<OrderDto>.Ok(order))
        : Results.NotFound(ApiResponse<OrderDto>.Fail($"Order with ID '{id}' was not found."));
})
.WithName("GetOrderById")
.WithSummary("Retrieve a specific order by its unique ID");

// POST /api/orders - Place a new order (interacts with ProductApi microservice)
ordersGroup.MapPost("/", async (CreateOrderRequest request, IOrderRepository repo, IProductServiceClient productClient, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.CustomerEmail))
    {
        return Results.BadRequest(ApiResponse<OrderDto>.Fail("Customer email is required."));
    }

    if (request.Items is null || request.Items.Count == 0)
    {
        return Results.BadRequest(ApiResponse<OrderDto>.Fail("Order must contain at least one item."));
    }

    if (request.Items.Any(i => i.Quantity <= 0))
    {
        return Results.BadRequest(ApiResponse<OrderDto>.Fail("Each item quantity must be at least 1."));
    }

    // Step 1: Pre-validate each product exists and check stock via ProductApi
    var orderItems = new List<OrderItemDto>();
    decimal totalAmount = 0;

    foreach (var item in request.Items)
    {
        var (success, product, error) = await productClient.GetProductAsync(item.ProductId, ct);
        if (!success || product is null)
        {
            return Results.BadRequest(ApiResponse<OrderDto>.Fail($"Product validation failed: {error}"));
        }

        if (product.StockQuantity < item.Quantity)
        {
            return Results.BadRequest(ApiResponse<OrderDto>.Fail(
                $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}"));
        }

        var itemTotal = product.Price * item.Quantity;
        totalAmount += itemTotal;

        orderItems.Add(new OrderItemDto(
            product.Id,
            product.Name,
            product.Price,
            item.Quantity,
            itemTotal
        ));
    }

    // Step 2: Deduct stock via ProductApi
    var deductedItems = new List<(Guid ProductId, int Quantity)>();
    foreach (var item in request.Items)
    {
        var (success, _, error) = await productClient.UpdateStockAsync(item.ProductId, -item.Quantity, ct);
        if (!success)
        {
            // Rollback previously deducted items (compensating transaction)
            foreach (var deducted in deductedItems)
            {
                await productClient.UpdateStockAsync(deducted.ProductId, deducted.Quantity, ct);
            }

            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        deductedItems.Add((item.ProductId, item.Quantity));
    }

    // Step 3: Record Order
    var order = new OrderDto(
        Guid.NewGuid(),
        request.CustomerEmail,
        orderItems,
        totalAmount,
        "Confirmed",
        DateTime.UtcNow
    );

    var created = repo.Create(order);
    return Results.Created($"/api/orders/{created.Id}", ApiResponse<OrderDto>.Ok(created, "Order placed successfully."));
})
.WithName("CreateOrder")
.WithSummary("Place a new order and decrement inventory via Product Service");

// POST /api/orders/{id}/cancel - Cancel order and restore product stock
ordersGroup.MapPost("/{id:guid}/cancel", async (Guid id, IOrderRepository repo, IProductServiceClient productClient, CancellationToken ct) =>
{
    var order = repo.GetById(id);
    if (order is null)
    {
        return Results.NotFound(ApiResponse<OrderDto>.Fail($"Order with ID '{id}' was not found."));
    }

    if (order.Status == "Cancelled")
    {
        return Results.BadRequest(ApiResponse<OrderDto>.Fail("Order has already been cancelled."));
    }

    // Restore stock for all items in order
    foreach (var item in order.Items)
    {
        await productClient.UpdateStockAsync(item.ProductId, item.Quantity, ct);
    }

    var updated = repo.UpdateStatus(id, "Cancelled");
    return Results.Ok(ApiResponse<OrderDto>.Ok(updated!, "Order cancelled and inventory restored."));
})
.WithName("CancelOrder")
.WithSummary("Cancel an existing order and restore product inventory");

app.Run();
