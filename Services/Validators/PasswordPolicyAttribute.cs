using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Services.Validators
{
    public class PasswordPolicyAttribute : ValidationAttribute
    {
        private readonly int _minLength;
        private readonly bool _requireDigit;
        private readonly bool _requireUppercase;

        public PasswordPolicyAttribute(int minLength = 8, bool requireDigit = true, bool requireUppercase = true)
        {
            _minLength = minLength;
            _requireDigit = requireDigit;
            _requireUppercase = requireUppercase;
            ErrorMessage = "Password does not meet the policy requirements.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var password = value as string;

            if (string.IsNullOrEmpty(password))
                return new ValidationResult("Password is required.");

            if (password.Length < _minLength)
                return new ValidationResult($"Password must be at least {_minLength} characters long.");

            if (_requireDigit && !Regex.IsMatch(password, @"\d"))
                return new ValidationResult("Password must contain at least one digit.");

            if (_requireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
                return new ValidationResult("Password must contain at least one uppercase letter.");

            return ValidationResult.Success;
        }
    }
}
