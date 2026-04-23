using System.ComponentModel.DataAnnotations;
using Services.Validators;

namespace MGEvents.Tests.Services;

public class SchoolEmailAttributeTests
{
    private readonly SchoolEmailAttribute _attribute = new();

    [Theory]
    [InlineData("student@schoolmath.eu")]
    [InlineData(" Student@SchoolMath.EU ")]
    public void IsValid_AcceptsSchoolDomainEmail(string email)
    {
        var result = _attribute.GetValidationResult(email, new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValid_AllowsEmptyValue_ForOptionalDtoFields(string? email)
    {
        var result = _attribute.GetValidationResult(email, new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Theory]
    [InlineData("student@gmail.com", "Имейл адресът трябва да завършва на @schoolmath.eu.")]
    [InlineData("invalid-email", "Невалиден имейл адрес.")]
    public void IsValid_RejectsInvalidOrNonSchoolEmail(string email, string expectedMessage)
    {
        var result = _attribute.GetValidationResult(email, new ValidationContext(new object()));

        Assert.NotNull(result);
        Assert.Equal(expectedMessage, result!.ErrorMessage);
    }
}
