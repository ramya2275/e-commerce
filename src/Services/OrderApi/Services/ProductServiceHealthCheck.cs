using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderApi.Services;

public class ProductServiceHealthCheck : IHealthCheck
{
    private readonly IProductServiceClient _client;

    public ProductServiceHealthCheck(IProductServiceClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var isReachable = await _client.IsHealthyAsync(cancellationToken);
        if (isReachable)
        {
            return HealthCheckResult.Healthy("Product Service dependency is healthy and reachable.");
        }

        return HealthCheckResult.Degraded("Product Service is unreachable or unhealthy.");
    }
}
