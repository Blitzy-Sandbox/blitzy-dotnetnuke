using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Domain;

/// <summary>
/// Enum-preservation tests for <see cref="VisibilityState"/>.
/// DNN 4.9.0.85 persisted/compared these as integers, so the verbatim values must be preserved
/// to keep behavioral parity (AAP §0.6.3, §0.7.1).
/// MIGRATION: The authoritative source order (Library/Components/Modules/ModuleInfo.vb, VB default
/// 0-based numbering, no explicit values) is Maximized=0, Minimized=1, None=2. The AAP §0.3.1 layout
/// diagram's casual "None/Minimized/Maximized" listing is a cosmetic reordering and is intentionally
/// NOT followed; persisted/compared integers depend on the source order, which the migrated
/// DnnMigration.Domain.Enums.VisibilityState preserves.
/// </summary>
public class VisibilityStateTests
{
    [Theory]
    [InlineData(VisibilityState.Maximized, 0)]
    [InlineData(VisibilityState.Minimized, 1)]
    [InlineData(VisibilityState.None, 2)]
    public void Member_HasVerbatimLegacyIntegerValue(VisibilityState member, int expectedValue)
    {
        ((int)member).Should().Be(expectedValue);
    }

    [Fact]
    public void Enum_DefinesExactlyThreeMembers()
    {
        Enum.GetValues<VisibilityState>().Should().HaveCount(3);
    }
}
