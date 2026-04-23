using System.ComponentModel.DataAnnotations;
using Services.Validators;

namespace MGEvents.Tests.Services;

public class UsernamePolicyAttributeTests
{
    private readonly UsernamePolicyAttribute _attribute = new();

    [Theory]
    [InlineData("Student_12")]
    [InlineData("ADMIN.USER")]
    [InlineData("teacher-name")]
    public void IsValid_AcceptsLatinLettersDigitsAndSafeSeparators(string username)
    {
        var result = _attribute.GetValidationResult(username, new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValid_AllowsEmptyValue_ForOptionalDtoFields(string? username)
    {
        var result = _attribute.GetValidationResult(username, new ValidationContext(new object()));

        Assert.Equal(ValidationResult.Success, result);
    }

    [Theory]
    [InlineData("Иван")]
    [InlineData("user name")]
    [InlineData("user<script>")]
    [InlineData("emoji🙂")]
    public void IsValid_RejectsUnsafeOrNonLatinCharacters(string username)
    {
        var result = _attribute.GetValidationResult(username, new ValidationContext(new object()));

        Assert.NotNull(result);
        Assert.Equal("Потребителското име може да съдържа само латински букви, цифри и символите . _ -.", result!.ErrorMessage);
    }
}
