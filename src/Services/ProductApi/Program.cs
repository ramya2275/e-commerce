using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProductApi.Services;
using SharedContracts;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IProductRepository, ProductRepository>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("ProductApi service is running smoothly"));

// Configure CORS for web frontends
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Configure OpenAPI
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.MapOpenApi();
}

// -------------------------------------------------------------
// Kubernetes Health Probes
// -------------------------------------------------------------
// Liveness probe: returns 200 OK if service process is alive
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .WithName("ProductLivenessProbe")
   .WithTags("Health");

// Readiness probe: returns 200 OK if service is ready to accept incoming traffic
app.MapHealthChecks("/health/ready");

// -------------------------------------------------------------
// REST API Endpoints for Products
// -------------------------------------------------------------
var productsGroup = app.MapGroup("/api/products").WithTags("Products");

// GET /api/products - Get all products, optional search term
productsGroup.MapGet("/", (IProductRepository repo, string? search) =>
{
    var products = repo.GetAll();
    if (!string.IsNullOrWhiteSpace(search))
    {
        products = products.Where(p =>
            p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    return Results.Ok(ApiResponse<IEnumerable<ProductDto>>.Ok(products));
})
.WithName("GetProducts")
.WithSummary("Retrieve all products with optional text filtering");

// GET /api/products/{id} - Get single product by GUID
productsGroup.MapGet("/{id:guid}", (Guid id, IProductRepository repo) =>
{
    var product = repo.GetById(id);
    return product is not null
        ? Results.Ok(ApiResponse<ProductDto>.Ok(product))
        : Results.NotFound(ApiResponse<ProductDto>.Fail($"Product with ID '{id}' was not found."));
})
.WithName("GetProductById")
.WithSummary("Retrieve a specific product by its unique GUID");

// POST /api/products - Create a new product
productsGroup.MapPost("/", (CreateProductRequest request, IProductRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(ApiResponse<ProductDto>.Fail("Product name is required."));
    }

    if (request.Price <= 0)
    {
        return Results.BadRequest(ApiResponse<ProductDto>.Fail("Price must be greater than zero."));
    }

    if (request.StockQuantity < 0)
    {
        return Results.BadRequest(ApiResponse<ProductDto>.Fail("Stock quantity cannot be negative."));
    }

    var created = repo.Create(request);
    return Results.Created($"/api/products/{created.Id}", ApiResponse<ProductDto>.Ok(created, "Product created successfully"));
})
.WithName("CreateProduct")
.WithSummary("Create a new product in the catalog");

// PUT /api/products/{id}/stock - Adjust inventory stock
productsGroup.MapPut("/{id:guid}/stock", (Guid id, UpdateStockRequest request, IProductRepository repo) =>
{
    var (success, error, product) = repo.UpdateStock(id, request.QuantityChange);
    if (!success)
    {
        return error!.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? Results.NotFound(ApiResponse<ProductDto>.Fail(error))
            : Results.BadRequest(ApiResponse<ProductDto>.Fail(error));
    }

    return Results.Ok(ApiResponse<ProductDto>.Ok(product!, "Stock updated successfully."));
})
.WithName("UpdateProductStock")
.WithSummary("Update stock inventory for a product (positive to add, negative to consume)");

app.Run();
