using FluentAssertions;
using CouponApi.Services;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.CouponApi;

public class CouponRepositoryTests
{
    private readonly CouponRepository _repository;

    public CouponRepositoryTests()
    {
        _repository = new CouponRepository();
    }

    [Fact]
    public void SeedData_InitializesDefaultCoupons()
    {
        // Act
        var coupons = _repository.GetAll().ToList();

        // Assert
        coupons.Should().HaveCount(3);
        coupons.Should().Contain(c => c.Code == "WELCOME10");
        coupons.Should().Contain(c => c.Code == "SAVE20");
        coupons.Should().Contain(c => c.Code == "EXPIRED50");
    }

    [Theory]
    [InlineData("WELCOME10")]
    [InlineData("welcome10")]
    [InlineData("  WELCOME10  ")]
    [InlineData("wElCoMe10")]
    public void GetByCode_MatchesCaseInsensitivelyAndTrimsWhitespace(string searchCode)
    {
        // Act
        var coupon = _repository.GetByCode(searchCode);

        // Assert
        coupon.Should().NotBeNull();
        coupon!.Code.Should().Be("WELCOME10");
        coupon.DiscountPercentage.Should().Be(10m);
    }

    [Fact]
    public void GetByCode_WithUnknownCode_ReturnsNull()
    {
        // Act
        var coupon = _repository.GetByCode("NONEXISTENT99");

        // Assert
        coupon.Should().BeNull();
    }

    [Fact]
    public void Create_NormalizesCodeToUppercaseAndTrims()
    {
        // Arrange
        var request = new CreateCouponRequest(
            "  blackfriday30  ",
            30m,
            200m,
            DateTime.UtcNow.AddDays(14)
        );

        // Act
        var created = _repository.Create(request);
        var fetched = _repository.GetByCode("BLACKFRIDAY30");

        // Assert
        created.Code.Should().Be("BLACKFRIDAY30");
        created.DiscountPercentage.Should().Be(30m);
        created.MinimumOrderAmount.Should().Be(200m);
        created.IsActive.Should().BeTrue();

        fetched.Should().NotBeNull();
        fetched!.Code.Should().Be("BLACKFRIDAY30");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrWhitespaceCode_ReturnsInvalid(string? code)
    {
        // Act
        var result = _repository.Validate(code!, 100m);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DiscountAmount.Should().Be(0);
        result.FinalTotal.Should().Be(100m);
        result.Message.Should().Be("Coupon code is empty.");
    }

    [Fact]
    public void Validate_WithNonExistentCode_ReturnsInvalid()
    {
        // Act
        var result = _repository.Validate("DOESNOTEXIST", 100m);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("does not exist");
    }

    [Fact]
    public void Validate_WithExpiredCoupon_ReturnsInvalid()
    {
        // Act - EXPIRED50 is seeded with an expiry in the past
        var result = _repository.Validate("EXPIRED50", 150m);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DiscountAmount.Should().Be(0);
        result.Message.Should().Contain("has expired");
    }

    [Fact]
    public void Validate_WithInactiveCoupon_ReturnsInvalid()
    {
        // Arrange: Create a coupon then deactivate it
        var request = new CreateCouponRequest("INACTIVE15", 15m, 50m, DateTime.UtcNow.AddDays(10));
        var created = _repository.Create(request);
        
        // We simulate inactive by validating an inactive coupon or modifying it
        // CouponDto is stored in internal dictionary; let's verify inactive condition
        // CouponRepository exposes Create which sets IsActive=true.
        // Let's create an inactive coupon by updating the internal dictionary or via record
        var inactive = created with { IsActive = false };
        // Test repository with inactive coupon:
        // We can test the validate logic for inactive coupon
        var testRepo = new CouponRepository();
        var c = testRepo.Create(new CreateCouponRequest("DEACT10", 10m, 50m, DateTime.UtcNow.AddDays(10)));
        // Replace in testRepo via private reflection or verify Validate logic:
        var field = typeof(CouponRepository).GetField("_coupons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, CouponDto>)field!.GetValue(testRepo)!;
        dict["DEACT10"] = c with { IsActive = false };

        // Act
        var result = testRepo.Validate("DEACT10", 100m);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Message.Should().Contain("is inactive");
    }

    [Fact]
    public void Validate_WithOrderTotalBelowMinimumSpend_ReturnsInvalid()
    {
        // Arrange: WELCOME10 requires minimum order of $50
        var orderTotal = 49.99m;

        // Act
        var result = _repository.Validate("WELCOME10", orderTotal);

        // Assert
        result.IsValid.Should().BeFalse();
        result.DiscountAmount.Should().Be(0);
        result.FinalTotal.Should().Be(orderTotal);
        result.Message.Should().Contain("is below the minimum required amount");
    }

    [Fact]
    public void Validate_WithValidCouponAndOrderTotal_CalculatesCorrectDiscountAndFinalTotal()
    {
        // Arrange: WELCOME10 is 10% off with $50 min order
        var orderTotal = 150.00m;

        // Act
        var result = _repository.Validate("welcome10", orderTotal);

        // Assert
        result.IsValid.Should().BeTrue();
        result.DiscountAmount.Should().Be(15.00m);
        result.FinalTotal.Should().Be(135.00m);
        result.Message.Should().Contain("applied successfully");
    }

    [Fact]
    public void Validate_RoundsDiscountToTwoDecimalPlaces()
    {
        // Arrange: SAVE20 is 20% off with $100 min order
        var orderTotal = 125.55m; // 20% of 125.55 = 25.11

        // Act
        var result = _repository.Validate("SAVE20", orderTotal);

        // Assert
        result.IsValid.Should().BeTrue();
        result.DiscountAmount.Should().Be(25.11m);
        result.FinalTotal.Should().Be(100.44m);
    }
}
