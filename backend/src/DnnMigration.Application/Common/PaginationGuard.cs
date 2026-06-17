using FluentValidation;
using FluentValidation.Results;

namespace DnnMigration.Application.Common;

// MIGRATION: PaginationGuard is a NEW construct introduced to satisfy the API input-validation hardening
// required by the code review (unbounded externally-supplied pageIndex/pageSize on the Portals and Users
// list endpoints could trigger runtime errors or unbounded reads / DoS). There is no legacy equivalent:
// the DNN *Controller.vb paged data methods accepted pageIndex/pageSize without bounds checks. This guard
// centralizes the bound enforcement at the API/Application boundary so every paged resource endpoint
// applies the identical contract. It throws a FluentValidation.ValidationException, which the Api's
// ExceptionHandlingMiddleware maps to an RFC 7807 400 ValidationProblemDetails (errors keyed by the
// offending parameter name, camelCased on the wire: "pageIndex" / "pageSize"). Documented in root
// MIGRATION_NOTES.md.

/// <summary>
/// Validates externally-supplied pagination parameters (<c>pageIndex</c> / <c>pageSize</c>) before they are
/// used to compose a paged database read, enforcing a uniform, safe contract across every paged REST
/// resource endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The contract is: <c>pageIndex</c> must be zero or greater (the legacy <c>-1</c> "return all rows" sentinel
/// is an INTERNAL-only convention and is deliberately rejected when supplied by an external caller), and
/// <c>pageSize</c> must be between <c>1</c> and <see cref="MaxPageSize"/> inclusive. Violations raise a
/// <see cref="ValidationException"/> carrying one <see cref="ValidationFailure"/> per offending parameter;
/// the Api's global exception middleware translates that into an RFC 7807
/// <c>application/problem+json</c> <c>400 Bad Request</c> whose <c>errors</c> map is keyed by the parameter
/// name.
/// </para>
/// <para>
/// Repositories retain their own defensive clamps as a second line of defence, but this guard is the
/// authoritative boundary that returns a well-formed 400 to the client instead of letting bad input reach EF.
/// </para>
/// </remarks>
public static class PaginationGuard
{
    /// <summary>
    /// The maximum number of items a single page may request. Caps unbounded reads to protect the API from
    /// denial-of-service via an extreme <c>pageSize</c>.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>The wire/parameter name reported for an invalid page index.</summary>
    private const string PageIndexParameter = "pageIndex";

    /// <summary>The wire/parameter name reported for an invalid page size.</summary>
    private const string PageSizeParameter = "pageSize";

    /// <summary>
    /// Validates <paramref name="pageIndex"/> and <paramref name="pageSize"/> against the pagination contract,
    /// throwing a <see cref="ValidationException"/> (one failure per offending parameter) when either is out of
    /// range. Both parameters are checked so the caller receives every problem at once.
    /// </summary>
    /// <param name="pageIndex">The zero-based page index supplied by the caller. Must be <c>&gt;= 0</c>.</param>
    /// <param name="pageSize">The requested page size supplied by the caller. Must be <c>1..<see cref="MaxPageSize"/></c>.</param>
    /// <exception cref="ValidationException">
    /// Thrown when <paramref name="pageIndex"/> is negative and/or <paramref name="pageSize"/> is outside the
    /// inclusive range <c>1..<see cref="MaxPageSize"/></c>.
    /// </exception>
    public static void Validate(int pageIndex, int pageSize)
    {
        var failures = new List<ValidationFailure>(2);

        if (pageIndex < 0)
        {
            failures.Add(new ValidationFailure(
                PageIndexParameter,
                $"The {PageIndexParameter} must be greater than or equal to 0.")
            {
                AttemptedValue = pageIndex
            });
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            failures.Add(new ValidationFailure(
                PageSizeParameter,
                $"The {PageSizeParameter} must be between 1 and {MaxPageSize}.")
            {
                AttemptedValue = pageSize
            });
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
