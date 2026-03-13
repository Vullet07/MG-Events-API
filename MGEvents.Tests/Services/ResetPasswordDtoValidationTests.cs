using System.ComponentModel.DataAnnotations;
using Services.Dtos;

namespace MGEvents.Tests.Services;

public class ResetPasswordDtoValidationTests
{
    [Fact]
    public void ShouldFailValidation_WhenNewPasswordViolatesPolicy()
    {
        var dto = new ResetPasswordDto
        {
            Token = "reset-token",
            NewPassword = "weak"
        };

        var results = Validate(dto);

        Assert.Contains(results, r => r.ErrorMessage == "Password must be at least 8 characters long.");
    }

    [Fact]
    public void ShouldPassValidation_WhenNewPasswordIsStrong()
    {
        var dto = new ResetPasswordDto
        {
            Token = "reset-token",
            NewPassword = "StrongPass1"
        };

        var results = Validate(dto);

        Assert.Empty(results);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }
}
