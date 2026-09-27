using System.Net.Http.Json;
using SharedContracts;
namespace OrderApi.Services;


public interface ICouponServiceClient
{
    Task<(bool Success, CouponValidationResult? Result, string? Error)> ValidateCouponAsync(string code, decimal orderTotal, CancellationToken ct = default);
}

public class CouponServiceClient : ICouponServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CouponServiceClient> _logger;

    public CouponServiceClient(HttpClient httpClient, ILogger<CouponServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool Success, CouponValidationResult? Result, string? Error)> ValidateCouponAsync(string code, decimal orderTotal, CancellationToken ct = default)
    {
        try
        {
            var request = new ValidateCouponRequest(code, orderTotal);
            var response = await _httpClient.PostAsJsonAsync("/api/coupons/validate", request, ct);

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<CouponValidationResult>>(cancellationToken: ct);
            if (!response.IsSuccessStatusCode || apiResponse is null || !apiResponse.Success)
            {
                var error = apiResponse?.Errors?.FirstOrDefault() ?? apiResponse?.Message ?? $"Coupon validation failed with status {(int)response.StatusCode}.";
                return (false, null, error);
            }

            return (true, apiResponse.Data, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Coupon Service for code {CouponCode}", code);
            return (false, null, $"Unable to reach Coupon Service: {ex.Message}");
        }
    }
}
