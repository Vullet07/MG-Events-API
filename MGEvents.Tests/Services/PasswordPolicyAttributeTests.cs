using System.ComponentModel.DataAnnotations;
using Services.Validators;

namespace MGEvents.Tests.Services;

public class PasswordPolicyAttributeTests
{
    private static ValidationResult? Validate(string? password, PasswordPolicyAttribute? attribute = null)
    {
        var policy = attribute ?? new PasswordPolicyAttribute();
        var context = new ValidationContext(new object());
        return policy.GetValidationResult(password, context);
    }

    [Fact]
    public void IsValid_ShouldFail_WhenPasswordMissing()
    {
        var result = Validate(null);

        Assert.NotNull(result);
        Assert.Equal("Password is required.", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_ShouldFail_WhenPasswordTooShort()
    {
        var result = Validate("Abc123");

        Assert.NotNull(result);
        Assert.Equal("Password must be at least 8 characters long.", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_ShouldFail_WhenPasswordHasNoDigit()
    {
        var result = Validate("Abcdefgh");

        Assert.NotNull(result);
        Assert.Equal("Password must contain at least one digit.", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_ShouldFail_WhenPasswordHasNoUppercase()
    {
        var result = Validate("abcdefg1");

        Assert.NotNull(result);
        Assert.Equal("Password must contain at least one uppercase letter.", result!.ErrorMessage);
    }

    [Fact]
    public void IsValid_ShouldSucceed_WhenPasswordMeetsDefaultPolicy()
    {
        var result = Validate("ValidPass1");

        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_ShouldAllowPasswordWithoutDigit_WhenDigitRequirementDisabled()
    {
        var policy = new PasswordPolicyAttribute(minLength: 6, requireDigit: false, requireUppercase: true);

        var result = Validate("StrongA", policy);

        Assert.Equal(ValidationResult.Success, result);
    }
}
