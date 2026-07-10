namespace DnnMigration.Domain.Common;

/// <summary>
/// Normalized, validated pagination request parameters. Produced from the raw, caller-supplied
/// <c>?page=</c>/<c>?pageSize=</c> query values via <see cref="Normalize(int?, int?)"/> so that the
/// hard bounds (a sane default page size and an enforced maximum) are applied in ONE place and every
/// downstream layer (service, repository) receives already-safe values.
/// </summary>
/// <remarks>
/// MIGRATION (QA finding — R6 Issue 1, unbounded list endpoints): the R6 "Expected Outcome" accepts
/// either true server-side pagination OR an enforced maximum row cap. This type delivers both — it
/// derives a bounded <see cref="Skip"/>/<see cref="PageSize"/> window AND clamps the page size to
/// <see cref="MaxPageSize"/> so a caller can never coerce the server into materializing an unbounded
/// result set (e.g. <c>?pageSize=100000</c> is clamped to <see cref="MaxPageSize"/>). It is a pure,
/// framework-free value object in the dependency-free Domain layer, so it is trivially unit-testable and
/// reusable by every controller.
/// </remarks>
public sealed class PaginationParameters
{
    /// <summary>The page number used when the caller supplies none (or an invalid value). Pages are 1-based.</summary>
    public const int DefaultPage = 1;

    /// <summary>The page size used when the caller supplies none (or an invalid value).</summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// The hard upper bound on page size. A request for more than this is clamped down to it, which is
    /// what bounds the server-side fetch and closes the unbounded-list finding.
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>The normalized, 1-based page number (always &gt;= <see cref="DefaultPage"/>).</summary>
    public int Page { get; }

    /// <summary>The normalized page size (always within <c>[1, <see cref="MaxPageSize"/>]</c>).</summary>
    public int PageSize { get; }

    /// <summary>
    /// The number of rows to skip to reach the requested page — <c>(Page - 1) * PageSize</c>. This is the
    /// value handed to EF Core's <c>Skip(...)</c>; <see cref="PageSize"/> is handed to <c>Take(...)</c>.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    private PaginationParameters(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// Normalizes raw, caller-supplied pagination values into safe, bounded parameters.
    /// </summary>
    /// <param name="page">
    /// The requested 1-based page number, or <see langword="null"/> when omitted. A <see langword="null"/>
    /// or sub-1 value falls back to <see cref="DefaultPage"/>.
    /// </param>
    /// <param name="pageSize">
    /// The requested page size, or <see langword="null"/> when omitted. A <see langword="null"/> or sub-1
    /// value falls back to <see cref="DefaultPageSize"/>; a value greater than <see cref="MaxPageSize"/> is
    /// clamped down to <see cref="MaxPageSize"/>.
    /// </param>
    /// <returns>A validated <see cref="PaginationParameters"/> instance.</returns>
    public static PaginationParameters Normalize(int? page, int? pageSize)
    {
        var normalizedPage = page.GetValueOrDefault(DefaultPage);
        if (normalizedPage < 1)
        {
            normalizedPage = DefaultPage;
        }

        var normalizedPageSize = pageSize.GetValueOrDefault(DefaultPageSize);
        if (normalizedPageSize < 1)
        {
            normalizedPageSize = DefaultPageSize;
        }
        else if (normalizedPageSize > MaxPageSize)
        {
            normalizedPageSize = MaxPageSize;
        }

        return new PaginationParameters(normalizedPage, normalizedPageSize);
    }
}
