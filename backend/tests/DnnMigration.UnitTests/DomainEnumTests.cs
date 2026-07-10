namespace DnnMigration.UnitTests;

using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

/// <summary>
/// Contract-locking unit tests for the migrated <c>DnnMigration.Domain.Enums</c> types.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: These enums back existing SQL Server <c>int</c> columns and therefore constitute a
/// persistence / wire contract. Per the Minimal Change Clause of the DotNetNuke → .NET 8 migration,
/// the exact underlying integer values were carried over verbatim from the legacy VB.NET source
/// (for example <see cref="SecurityAccessLevel"/> from
/// <c>Library/Components/Security/PortalSecurity.vb</c>). This test class locks both the underlying
/// integer of every member <em>and</em> the exact set of member names, so that any accidental
/// reordering, renumbering, renaming, addition, or removal of a member — or a change to the
/// underlying type — fails the build rather than silently corrupting data written to (or read from)
/// the existing database.
/// </para>
/// <para>
/// The tests deliberately depend only on the Domain project (plus xUnit and FluentAssertions); no
/// mocking, mapping, or persistence dependencies are involved because enums are pure value contracts.
/// </para>
/// </remarks>
public class DomainEnumTests
{
    // =============================================================================================
    // SecurityAccessLevel
    // Legacy source: Library/Components/Security/PortalSecurity.vb (Public Enum SecurityAccessLevel
    // As Integer). Seven members, INCLUDING three negative values, mapping module/action access
    // rights. The negatives (ControlPanel = -3, SkinObject = -2, Anonymous = -1) are the highest-risk
    // part of the contract and are asserted explicitly.
    // =============================================================================================

    /// <summary>
    /// Every <see cref="SecurityAccessLevel"/> member must cast to its exact legacy integer,
    /// including the three negative values.
    /// </summary>
    [Theory]
    [InlineData(SecurityAccessLevel.ControlPanel, -3)]
    [InlineData(SecurityAccessLevel.SkinObject, -2)]
    [InlineData(SecurityAccessLevel.Anonymous, -1)]
    [InlineData(SecurityAccessLevel.View, 0)]
    [InlineData(SecurityAccessLevel.Edit, 1)]
    [InlineData(SecurityAccessLevel.Admin, 2)]
    [InlineData(SecurityAccessLevel.Host, 3)]
    public void SecurityAccessLevel_member_maps_to_expected_underlying_integer(
        SecurityAccessLevel level, int expected)
    {
        ((int)level).Should().Be(expected);
    }

    /// <summary>
    /// The enum must declare exactly seven members — no more, no fewer.
    /// </summary>
    [Fact]
    public void SecurityAccessLevel_declares_exactly_seven_members()
    {
        Enum.GetValues<SecurityAccessLevel>().Length.Should().Be(7);
    }

    /// <summary>
    /// The exact set of member names must match the legacy contract. Comparison is order-independent
    /// because <see cref="Enum.GetNames{TEnum}()"/> orders by unsigned magnitude, which places the
    /// negative-valued members after the non-negative ones rather than in declaration order.
    /// </summary>
    [Fact]
    public void SecurityAccessLevel_declares_exactly_the_expected_member_names()
    {
        Enum.GetNames<SecurityAccessLevel>()
            .Should()
            .BeEquivalentTo(
                "ControlPanel",
                "SkinObject",
                "Anonymous",
                "View",
                "Edit",
                "Admin",
                "Host");
    }

    /// <summary>
    /// The underlying type must remain <see cref="int"/> so the enum stays compatible with the
    /// existing 4-byte integer database columns.
    /// </summary>
    [Fact]
    public void SecurityAccessLevel_underlying_type_is_int32()
    {
        Enum.GetUnderlyingType(typeof(SecurityAccessLevel)).Should().Be(typeof(int));
    }

    /// <summary>
    /// Every integer in the contiguous legacy range [-3, 3] must be a defined member.
    /// </summary>
    [Theory]
    [InlineData(-3)]
    [InlineData(-2)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SecurityAccessLevel_defines_every_contract_integer(int definedValue)
    {
        Enum.IsDefined(typeof(SecurityAccessLevel), definedValue).Should().BeTrue();
    }

    /// <summary>
    /// Integers immediately outside the legacy range must NOT be defined, guarding against a stray
    /// member sneaking onto either end of the enum.
    /// </summary>
    [Theory]
    [InlineData(-4)]
    [InlineData(4)]
    public void SecurityAccessLevel_does_not_define_out_of_range_integers(int undefinedValue)
    {
        Enum.IsDefined(typeof(SecurityAccessLevel), undefinedValue).Should().BeFalse();
    }

    // =============================================================================================
    // BannerType
    // Legacy source: the DotNetNuke Vendors domain (Public Enum BannerType As Integer). Seven members
    // numbered 1..7 describing a vendor banner's rendering format. Only the enum is migrated into the
    // Domain layer; the surrounding vendor classes are out of scope. The 1-based numbering is part of
    // the contract, so value 0 must remain undefined.
    // =============================================================================================

    /// <summary>
    /// Every <see cref="BannerType"/> member must cast to its exact legacy integer (1..7).
    /// </summary>
    [Theory]
    [InlineData(BannerType.Banner, 1)]
    [InlineData(BannerType.MicroButton, 2)]
    [InlineData(BannerType.Button, 3)]
    [InlineData(BannerType.Block, 4)]
    [InlineData(BannerType.Skyscraper, 5)]
    [InlineData(BannerType.Text, 6)]
    [InlineData(BannerType.Script, 7)]
    public void BannerType_member_maps_to_expected_underlying_integer(BannerType value, int expected)
    {
        ((int)value).Should().Be(expected);
    }

    /// <summary>
    /// The enum must declare exactly seven members.
    /// </summary>
    [Fact]
    public void BannerType_declares_exactly_seven_members()
    {
        Enum.GetValues<BannerType>().Length.Should().Be(7);
    }

    /// <summary>
    /// The last member, <see cref="BannerType.Script"/>, must be 7 — pinning the top of the 1..7 range.
    /// </summary>
    [Fact]
    public void BannerType_last_member_Script_is_seven()
    {
        ((int)BannerType.Script).Should().Be(7);
    }

    /// <summary>
    /// The exact set of member names must match the legacy contract (order-independent). This also
    /// locks the exact spelling of <c>MicroButton</c>.
    /// </summary>
    [Fact]
    public void BannerType_declares_exactly_the_expected_member_names()
    {
        Enum.GetNames<BannerType>()
            .Should()
            .BeEquivalentTo(
                "Banner",
                "MicroButton",
                "Button",
                "Block",
                "Skyscraper",
                "Text",
                "Script");
    }

    /// <summary>
    /// The underlying type must remain <see cref="int"/>.
    /// </summary>
    [Fact]
    public void BannerType_underlying_type_is_int32()
    {
        Enum.GetUnderlyingType(typeof(BannerType)).Should().Be(typeof(int));
    }

    /// <summary>
    /// Every integer in the legacy range [1, 7] must be a defined member.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void BannerType_defines_every_contract_integer(int definedValue)
    {
        Enum.IsDefined(typeof(BannerType), definedValue).Should().BeTrue();
    }

    /// <summary>
    /// Integers just outside the 1..7 range must NOT be defined. Value 0 is asserted specifically
    /// because <see cref="BannerType"/> is 1-based (there is no zero member).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void BannerType_does_not_define_out_of_range_integers(int undefinedValue)
    {
        Enum.IsDefined(typeof(BannerType), undefinedValue).Should().BeFalse();
    }

    // =============================================================================================
    // UserRegistrationType
    // The portal's user-registration mode. Four members numbered 0..3. NOTE: this is intentionally
    // distinct from the legacy operation-RESULT status enum (which used 0 and negative values); only
    // the registration-TYPE contract belongs in the Domain, so value 4 (and any negative) must remain
    // undefined here.
    // =============================================================================================

    /// <summary>
    /// Every <see cref="UserRegistrationType"/> member must cast to its exact legacy integer (0..3).
    /// </summary>
    [Theory]
    [InlineData(UserRegistrationType.NoRegistration, 0)]
    [InlineData(UserRegistrationType.PrivateRegistration, 1)]
    [InlineData(UserRegistrationType.PublicRegistration, 2)]
    [InlineData(UserRegistrationType.VerifiedRegistration, 3)]
    public void UserRegistrationType_member_maps_to_expected_underlying_integer(
        UserRegistrationType value, int expected)
    {
        ((int)value).Should().Be(expected);
    }

    /// <summary>
    /// The enum must declare exactly four members.
    /// </summary>
    [Fact]
    public void UserRegistrationType_declares_exactly_four_members()
    {
        Enum.GetValues<UserRegistrationType>().Length.Should().Be(4);
    }

    /// <summary>
    /// The first member, <see cref="UserRegistrationType.NoRegistration"/>, must be 0 — pinning the
    /// bottom of the 0..3 range.
    /// </summary>
    [Fact]
    public void UserRegistrationType_first_member_NoRegistration_is_zero()
    {
        ((int)UserRegistrationType.NoRegistration).Should().Be(0);
    }

    /// <summary>
    /// The exact set of member names must match the legacy contract (order-independent). This locks
    /// the exact spelling of <c>NoRegistration</c> and its peers.
    /// </summary>
    [Fact]
    public void UserRegistrationType_declares_exactly_the_expected_member_names()
    {
        Enum.GetNames<UserRegistrationType>()
            .Should()
            .BeEquivalentTo(
                "NoRegistration",
                "PrivateRegistration",
                "PublicRegistration",
                "VerifiedRegistration");
    }

    /// <summary>
    /// The underlying type must remain <see cref="int"/>.
    /// </summary>
    [Fact]
    public void UserRegistrationType_underlying_type_is_int32()
    {
        Enum.GetUnderlyingType(typeof(UserRegistrationType)).Should().Be(typeof(int));
    }

    /// <summary>
    /// The highest contract value (3) must be defined.
    /// </summary>
    [Fact]
    public void UserRegistrationType_defines_the_highest_contract_value_three()
    {
        Enum.IsDefined(typeof(UserRegistrationType), 3).Should().BeTrue();
    }

    /// <summary>
    /// Value 4 must NOT be defined — guarding against an extra member being appended past the top of
    /// the 0..3 range.
    /// </summary>
    [Fact]
    public void UserRegistrationType_does_not_define_value_four()
    {
        Enum.IsDefined(typeof(UserRegistrationType), 4).Should().BeFalse();
    }
}
