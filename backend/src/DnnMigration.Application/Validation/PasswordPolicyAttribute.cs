// -----------------------------------------------------------------------------
// DnnMigration - Modern .NET 8 Migration of DotNetNuke
// Copyright (c) DnnMigration Project. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: This custom validation attribute enforces password complexity requirements
// derived from legacy DNN membership provider configuration.
// Original patterns from:
//   - Library/Components/Security/PortalSecurity.vb (password validation)
//   - Website/web.config membership provider settings
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DnnMigration.Application.Validation;

/// <summary>
/// Custom validation attribute that enforces password complexity requirements.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: This attribute replicates the password policy enforcement from the legacy
/// DNN membership provider configuration and PortalSecurity validation.
/// </para>
/// <para>
/// Password requirements:
/// <list type="bullet">
///   <item><description>Minimum 6 characters (configurable via MinimumLength)</description></item>
///   <item><description>At least one uppercase letter (A-Z)</description></item>
///   <item><description>At least one lowercase letter (a-z)</description></item>
///   <item><description>At least one digit (0-9)</description></item>
///   <item><description>At least one special character (!@#$%^&amp;*(),.?":{}|&lt;&gt;)</description></item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class PasswordPolicyAttribute : ValidationAttribute
{
    /// <summary>
    /// Gets or sets the minimum password length.
    /// </summary>
    /// <value>The minimum length requirement. Defaults to 6 characters.</value>
    public int MinimumLength { get; set; } = 6;

    /// <summary>
    /// Gets or sets whether an uppercase letter is required.
    /// </summary>
    /// <value><c>true</c> if uppercase is required; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a lowercase letter is required.
    /// </summary>
    /// <value><c>true</c> if lowercase is required; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a digit is required.
    /// </summary>
    /// <value><c>true</c> if a digit is required; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a special character is required.
    /// </summary>
    /// <value><c>true</c> if special character is required; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool RequireSpecialCharacter { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordPolicyAttribute"/> class.
    /// </summary>
    public PasswordPolicyAttribute()
    {
        ErrorMessage = "Password does not meet the security requirements.";
    }

    /// <summary>
    /// Validates the password against the configured policy requirements.
    /// </summary>
    /// <param name="value">The password value to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>
    /// <see cref="ValidationResult.Success"/> if the password meets all requirements;
    /// otherwise, a <see cref="ValidationResult"/> with an error message.
    /// </returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            // Let [Required] handle null validation
            return ValidationResult.Success;
        }

        var password = value.ToString() ?? string.Empty;

        // Check minimum length
        if (password.Length < MinimumLength)
        {
            return new ValidationResult(
                $"Password must be at least {MinimumLength} characters long.",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        // Check uppercase requirement
        // MIGRATION: Pattern matching from legacy password validation
        if (RequireUppercase && !Regex.IsMatch(password, @"[A-Z]"))
        {
            return new ValidationResult(
                "Password must contain at least one uppercase letter (A-Z).",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        // Check lowercase requirement
        if (RequireLowercase && !Regex.IsMatch(password, @"[a-z]"))
        {
            return new ValidationResult(
                "Password must contain at least one lowercase letter (a-z).",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        // Check digit requirement
        if (RequireDigit && !Regex.IsMatch(password, @"[0-9]"))
        {
            return new ValidationResult(
                "Password must contain at least one digit (0-9).",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        // Check special character requirement
        // MIGRATION: Special characters pattern from DNN membership configuration
        if (RequireSpecialCharacter && !Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>_\-+=\[\]\\;/~`]"))
        {
            return new ValidationResult(
                "Password must contain at least one special character (!@#$%^&*(),.?\":{}|<>).",
                new[] { validationContext.MemberName ?? string.Empty });
        }

        return ValidationResult.Success;
    }
}
