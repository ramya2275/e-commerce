using FluentAssertions;
using SharedContracts;
using Xunit;

namespace MicroservicesDemo.UnitTests.Common;

public class ApiResponseTests
{
    [Fact]
    public void Ok_WithDataOnly_SetsSuccessTrueAndDefaultMessage()
    {
        // Arrange
        var data = "test-payload";

        // Act
        var response = ApiResponse<string>.Ok(data);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().Be(data);
        response.Message.Should().BeNull();
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void Ok_WithDataAndMessage_SetsMessageProperly()
    {
        // Arrange
        var data = 42;
        var message = "Operation completed successfully.";

        // Act
        var response = ApiResponse<int>.Ok(data, message);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().Be(42);
        response.Message.Should().Be(message);
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void Fail_WithErrorOnly_SetsSuccessFalseAndSingleErrorList()
    {
        // Arrange
        var errorMessage = "Item not found";

        // Act
        var response = ApiResponse<string>.Fail(errorMessage);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().NotBeNull();
        response.Errors.Should().ContainSingle().Which.Should().Be(errorMessage);
    }

    [Fact]
    public void Fail_WithErrorAndCustomErrorDetails_PopulatesDetailsList()
    {
        // Arrange
        var errorMessage = "Validation failed";
        var errorDetails = new List<string> { "Field 'Name' is required", "Field 'Price' must be > 0" };

        // Act
        var response = ApiResponse<object>.Fail(errorMessage, errorDetails);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Be(errorMessage);
        response.Errors.Should().BeEquivalentTo(errorDetails);
    }
}
