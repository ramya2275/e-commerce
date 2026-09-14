using System.Net.Http.Json;
using SharedContracts;

namespace OrderApi.Services;

public interface IProductServiceClient
{
    Task<(bool Success, ProductDto? Product, string? Error)> GetProductAsync(Guid productId, CancellationToken ct = default);
    Task<(bool Success, ProductDto? Product, string? Error)> UpdateStockAsync(Guid productId, int quantityChange, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool Success, ProductDto? Product, string? Error)> GetProductAsync(Guid productId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/products/{productId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, null, $"Product with ID '{productId}' does not exist in catalog.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Product service returned status code {(int)response.StatusCode}.");
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(cancellationToken: ct);
            if (apiResponse is null || !apiResponse.Success || apiResponse.Data is null)
            {
                return (false, null, apiResponse?.Message ?? "Failed to deserialize product details.");
            }

            return (true, apiResponse.Data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Product Service for product {ProductId}", productId);
            return (false, null, $"Unable to reach Product Service: {ex.Message}");
        }
    }

    public async Task<(bool Success, ProductDto? Product, string? Error)> UpdateStockAsync(Guid productId, int quantityChange, CancellationToken ct = default)
    {
        try
        {
            var request = new UpdateStockRequest(quantityChange);
            var response = await _httpClient.PutAsJsonAsync($"/api/products/{productId}/stock", request, ct);

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>(cancellationToken: ct);
            if (!response.IsSuccessStatusCode || apiResponse is null || !apiResponse.Success)
            {
                var error = apiResponse?.Errors?.FirstOrDefault() ?? apiResponse?.Message ?? "Failed to update stock in Product Service.";
                return (false, null, error);
            }

            return (true, apiResponse.Data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock in Product Service for product {ProductId}", productId);
            return (false, null, $"Unable to update stock in Product Service: {ex.Message}");
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await _httpClient.GetAsync("/health/live", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
