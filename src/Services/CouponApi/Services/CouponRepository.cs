using System.Collections.Concurrent;
using SharedContracts;

namespace CouponApi.Services;

public interface ICouponRepository
{
    IEnumerable<CouponDto> GetAll();
    CouponDto? GetByCode(string code);
    CouponDto Create(CreateCouponRequest request);
    CouponValidationResult Validate(string code, decimal orderTotal);
}

public class CouponRepository : ICouponRepository
{
    // Use StringComparer.OrdinalIgnoreCase so codes like "welcome10" and "WELCOME10" match
    private readonly ConcurrentDictionary<string, CouponDto> _coupons = new(StringComparer.OrdinalIgnoreCase);

    public CouponRepository()
    {
        // Seed default initial coupons for testing
        SeedData();
    }

    private void SeedData()
    {
        var sampleCoupons = new[]
        {
            new CouponDto(
                Guid.NewGuid(),
                "WELCOME10",
                10m, // 10% discount
                50m, // Minimum order $50
                DateTime.UtcNow.AddMonths(3),
                true
            ),
            new CouponDto(
                Guid.NewGuid(),
                "SAVE20",
                20m, // 20% discount
                100m, // Minimum order $100
                DateTime.UtcNow.AddMonths(1),
                true
            ),
            new CouponDto(
                Guid.NewGuid(),
                "EXPIRED50",
                50m,
                30m,
                DateTime.UtcNow.AddDays(-5), // Expired
                true
            )
        };

        foreach (var coupon in sampleCoupons)
        {
            _coupons[coupon.Code] = coupon;
        }
    }

    public IEnumerable<CouponDto> GetAll() => _coupons.Values.OrderBy(c => c.Code);

    public CouponDto? GetByCode(string code)
    {
        _coupons.TryGetValue(code.Trim(), out var coupon);
        return coupon;
    }

    public CouponDto Create(CreateCouponRequest request)
    {
        var coupon = new CouponDto(
            Guid.NewGuid(),
            request.Code.Trim().ToUpperInvariant(),
            request.DiscountPercentage,
            request.MinimumOrderAmount,
            request.ExpiryDate,
            true
        );

        _coupons[coupon.Code] = coupon;
        return coupon;
    }

    public CouponValidationResult Validate(string code, decimal orderTotal)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CouponValidationResult(false, 0, orderTotal, "Coupon code is empty.");
        }

        if (!_coupons.TryGetValue(code.Trim(), out var coupon))
        {
            return new CouponValidationResult(false, 0, orderTotal, $"Coupon code '{code}' does not exist.");
        }

        if (!coupon.IsActive)
        {
            return new CouponValidationResult(false, 0, orderTotal, $"Coupon '{coupon.Code}' is inactive.");
        }

        if (coupon.ExpiryDate < DateTime.UtcNow)
        {
            return new CouponValidationResult(false, 0, orderTotal, $"Coupon '{coupon.Code}' has expired.");
        }

        if (orderTotal < coupon.MinimumOrderAmount)
        {
            return new CouponValidationResult(
                false,
                0,
                orderTotal,
                $"Order total of ${orderTotal:F2} is below the minimum required amount of ${coupon.MinimumOrderAmount:F2} for this coupon."
            );
        }

        // Calculate discount
        var discountAmount = Math.Round(orderTotal * (coupon.DiscountPercentage / 100m), 2);
        var finalTotal = Math.Max(0, orderTotal - discountAmount);

        return new CouponValidationResult(
            true,
            discountAmount,
            finalTotal,
            $"Coupon '{coupon.Code}' applied successfully! ({coupon.DiscountPercentage}% discount)"
        );
    }
}
