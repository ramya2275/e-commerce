using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderApi.Services;
using SharedContracts;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Configure HttpClient for inter-service communication to ProductApi
var productApiBaseUrl = builder.Configuration["Services:ProductApi:Url"] ?? "http://localhost:5101";
builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(productApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Configure HttpClient for inter-service communication to CouponApi
var couponApiBaseUrl = builder.Configuration["Services:CouponApi:Url"] ?? "http://localhost:5103";
builder.Services.AddHttpClient<ICouponServiceClient, CouponServiceClient>(client =>
{
    client.BaseAddress = new Uri(couponApiBaseUrl);
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

// POST /api/orders - Place a new order (interacts with ProductApi and CouponApi microservices)
ordersGroup.MapPost("/", async (CreateOrderRequest request, IOrderService orderService, CancellationToken ct) =>
{
    var (success, order, error, statusCode) = await orderService.CreateOrderAsync(request, ct);
    if (!success)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => Results.BadRequest(ApiResponse<OrderDto>.Fail(error!)),
            StatusCodes.Status500InternalServerError => Results.StatusCode(StatusCodes.Status500InternalServerError),
            _ => Results.BadRequest(ApiResponse<OrderDto>.Fail(error ?? "An error occurred."))
        };
    }

    return Results.Created($"/api/orders/{order!.Id}", ApiResponse<OrderDto>.Ok(order, "Order placed successfully."));
})
.WithName("CreateOrder")
.WithSummary("Place a new order, validate optional coupon, and decrement inventory via Product Service");

// POST /api/orders/{id}/cancel - Cancel order and restore product stock
ordersGroup.MapPost("/{id:guid}/cancel", async (Guid id, IOrderService orderService, CancellationToken ct) =>
{
    var (success, order, error, statusCode) = await orderService.CancelOrderAsync(id, ct);
    if (!success)
    {
        return statusCode switch
        {
            StatusCodes.Status404NotFound => Results.NotFound(ApiResponse<OrderDto>.Fail(error!)),
            StatusCodes.Status400BadRequest => Results.BadRequest(ApiResponse<OrderDto>.Fail(error!)),
            _ => Results.BadRequest(ApiResponse<OrderDto>.Fail(error ?? "An error occurred."))
        };
    }

    return Results.Ok(ApiResponse<OrderDto>.Ok(order!, "Order cancelled and inventory restored."));
})
.WithName("CancelOrder")
.WithSummary("Cancel an existing order and restore product inventory");

app.Run();
