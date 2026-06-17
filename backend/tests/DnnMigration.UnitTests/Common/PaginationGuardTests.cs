using Xunit;
using FluentAssertions;
using FluentValidation;
using DnnMigration.Application.Common;

namespace DnnMigration.UnitTests.Common;

// MIGRATION: Unit coverage for PaginationGuard, the API-boundary bounds check introduced to close the code
// review's "unbounded externally-supplied pageIndex/pageSize" finding on the Portals and Users list
// endpoints. The guard rejects a negative page index (including the internal-only -1 "return all" sentinel
// when supplied externally) and a page size outside 1..MaxPageSize by throwing a FluentValidation
// ValidationException, which the Api's ExceptionHandlingMiddleware renders as an RFC 7807 400.
public class PaginationGuardTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 10)]
    [InlineData(5, 50)]
    [InlineData(0, PaginationGuard.MaxPageSize)] // upper bound is inclusive
    public void Validate_WithInRangeValues_DoesNotThrow(int pageIndex, int pageSize)
    {
        var act = () => PaginationGuard.Validate(pageIndex, pageSize);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)] // the legacy "return all rows" sentinel is rejected from external callers
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    public void Validate_WithNegativePageIndex_ThrowsForPageIndex(int pageIndex)
    {
        var act = () => PaginationGuard.Validate(pageIndex, 10);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(failure => failure.PropertyName == "pageIndex");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(PaginationGuard.MaxPageSize + 1)]
    [InlineData(int.MaxValue)]
    public void Validate_WithOutOfRangePageSize_ThrowsForPageSize(int pageSize)
    {
        var act = () => PaginationGuard.Validate(0, pageSize);

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().Contain(failure => failure.PropertyName == "pageSize");
    }

    [Fact]
    public void Validate_WithBothInvalid_ReportsBothParameters()
    {
        var act = () => PaginationGuard.Validate(-1, 0);

        var failures = act.Should().Throw<ValidationException>().Which.Errors.ToList();
        failures.Should().Contain(failure => failure.PropertyName == "pageIndex");
        failures.Should().Contain(failure => failure.PropertyName == "pageSize");
    }
}
