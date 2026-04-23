using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Services.Validators
{
    public class UsernamePolicyAttribute : ValidationAttribute
    {
        private static readonly Regex AllowedUsernamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        public UsernamePolicyAttribute()
        {
            ErrorMessage = "Потребителското име може да съдържа само латински букви, цифри и символите . _ -.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            var username = value as string;
            if (string.IsNullOrWhiteSpace(username))
            {
                return ValidationResult.Success;
            }

            return AllowedUsernamePattern.IsMatch(username.Trim())
                ? ValidationResult.Success
                : new ValidationResult(ErrorMessage);
        }
    }
}
