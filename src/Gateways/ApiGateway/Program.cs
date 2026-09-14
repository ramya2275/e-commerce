var builder = WebApplication.CreateBuilder(args);

// Add YARP Reverse Proxy configured from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Serve frontend static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Gateway info endpoint (for programmatic inspection)
app.MapGet("/api/gateway/info", () => Results.Ok(new
{
    Service = "Microservices API Gateway (YARP)",
    Status = "Online",
    Architecture = "Microservices with Reverse Proxy",
    AvailableRoutes = new[]
    {
        new { Route = "/api/products", Downstream = "Product Microservice (Catalog & Stock)" },
        new { Route = "/api/orders", Downstream = "Order Microservice (Orders & Processing)" },
        new { Route = "/health/live", Downstream = "Gateway Liveness Probe" },
        new { Route = "/health/ready", Downstream = "Gateway Readiness Probe" }
    }
}));

// Kubernetes Liveness & Readiness probes for Gateway
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", service = "ApiGateway", timestamp = DateTime.UtcNow }));
app.MapHealthChecks("/health/ready");

// Map YARP reverse proxy pipeline for downstream /api/products and /api/orders
app.MapReverseProxy();

app.Run();
