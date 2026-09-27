using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CouponApi.Services;
using SharedContracts;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<ICouponRepository, CouponRepository>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("CouponApi service is running smoothly"));

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
   .WithName("CouponLivenessProbe")
   .WithTags("Health");

// Readiness probe: returns 200 OK if service is ready to accept incoming traffic
app.MapHealthChecks("/health/ready");

// -------------------------------------------------------------
// REST API Endpoints for Coupons
// -------------------------------------------------------------
var couponsGroup = app.MapGroup("/api/coupons").WithTags("Coupons");

// GET /api/coupons - Get all coupons
couponsGroup.MapGet("/", (ICouponRepository repo) =>
{
    var coupons = repo.GetAll();
    return Results.Ok(ApiResponse<IEnumerable<CouponDto>>.Ok(coupons));
})
.WithName("GetCoupons")
.WithSummary("Retrieve all coupons");

// GET /api/coupons/{code} - Lookup coupon details by code
couponsGroup.MapGet("/{code}", (string code, ICouponRepository repo) =>
{
    var coupon = repo.GetByCode(code);
    return coupon is not null
        ? Results.Ok(ApiResponse<CouponDto>.Ok(coupon))
        : Results.NotFound(ApiResponse<CouponDto>.Fail($"Coupon with code '{code}' was not found."));
})
.WithName("GetCouponByCode")
.WithSummary("Retrieve details of a specific coupon by its code");

// POST /api/coupons/validate - Validate coupon against order total
couponsGroup.MapPost("/validate", (ValidateCouponRequest request, ICouponRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Code))
    {
        return Results.BadRequest(ApiResponse<CouponValidationResult>.Fail("Coupon code is required."));
    }

    if (request.OrderTotal < 0)
    {
        return Results.BadRequest(ApiResponse<CouponValidationResult>.Fail("Order total cannot be negative."));
    }

    var result = repo.Validate(request.Code, request.OrderTotal);
    if (!result.IsValid)
    {
        return Results.BadRequest(ApiResponse<CouponValidationResult>.Fail(result.Message ?? "Invalid coupon.", [result.Message ?? "Invalid coupon."]));
    }

    return Results.Ok(ApiResponse<CouponValidationResult>.Ok(result, result.Message));
})
.WithName("ValidateCoupon")
.WithSummary("Validate coupon code against an order total and calculate discount");

// POST /api/coupons - Admin creation of new coupon codes
couponsGroup.MapPost("/", (CreateCouponRequest request, ICouponRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Code))
    {
        return Results.BadRequest(ApiResponse<CouponDto>.Fail("Coupon code is required."));
    }

    if (request.DiscountPercentage <= 0 || request.DiscountPercentage > 100)
    {
        return Results.BadRequest(ApiResponse<CouponDto>.Fail("Discount percentage must be between 1 and 100."));
    }

    if (request.MinimumOrderAmount < 0)
    {
        return Results.BadRequest(ApiResponse<CouponDto>.Fail("Minimum order amount cannot be negative."));
    }

    if (request.ExpiryDate <= DateTime.UtcNow)
    {
        return Results.BadRequest(ApiResponse<CouponDto>.Fail("Expiry date must be in the future."));
    }

    var existing = repo.GetByCode(request.Code);
    if (existing is not null)
    {
        return Results.Conflict(ApiResponse<CouponDto>.Fail($"Coupon with code '{request.Code}' already exists."));
    }

    var created = repo.Create(request);
    return Results.Created($"/api/coupons/{created.Code}", ApiResponse<CouponDto>.Ok(created, "Coupon created successfully."));
})
.WithName("CreateCoupon")
.WithSummary("Create a new coupon code");

app.Run();
