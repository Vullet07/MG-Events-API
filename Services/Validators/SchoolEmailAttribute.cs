using System.ComponentModel.DataAnnotations;

namespace Services.Validators
{
    public class SchoolEmailAttribute : ValidationAttribute
    {
        private const string Domain = "@schoolmath.eu";

        public SchoolEmailAttribute()
        {
            ErrorMessage = $"Имейл адресът трябва да завършва на {Domain}.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var email = value as string;
            if (string.IsNullOrWhiteSpace(email))
            {
                return new ValidationResult("Имейл адресът е задължителен.");
            }

            var normalized = email.Trim();
            if (!new EmailAddressAttribute().IsValid(normalized))
            {
                return new ValidationResult("Невалиден имейл адрес.");
            }

            if (!normalized.EndsWith(Domain, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(ErrorMessage);
            }

            return ValidationResult.Success;
        }
    }
}
