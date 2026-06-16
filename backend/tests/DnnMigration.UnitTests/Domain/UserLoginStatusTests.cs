using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Domain;

/// <summary>
/// Enum-preservation tests for <see cref="UserLoginStatus"/>.
/// DNN 4.9.0.85 persisted/compared these as integers, so the verbatim values must be preserved
/// to keep behavioral parity (AAP §0.6.3, §0.7.1).
/// Source of truth: Library/Components/Users/Membership/UserLoginStatus.vb (explicit values 0-6).
/// </summary>
public class UserLoginStatusTests
{
    [Theory]
    [InlineData(UserLoginStatus.LOGIN_FAILURE, 0)]
    [InlineData(UserLoginStatus.LOGIN_SUCCESS, 1)]
    [InlineData(UserLoginStatus.LOGIN_SUPERUSER, 2)]
    [InlineData(UserLoginStatus.LOGIN_USERLOCKEDOUT, 3)]
    [InlineData(UserLoginStatus.LOGIN_USERNOTAPPROVED, 4)]
    [InlineData(UserLoginStatus.LOGIN_INSECUREADMINPASSWORD, 5)]
    [InlineData(UserLoginStatus.LOGIN_INSECUREHOSTPASSWORD, 6)]
    public void Member_HasVerbatimLegacyIntegerValue(UserLoginStatus member, int expectedValue)
    {
        ((int)member).Should().Be(expectedValue);
    }

    [Fact]
    public void Enum_DefinesExactlySevenMembers()
    {
        Enum.GetValues<UserLoginStatus>().Should().HaveCount(7);
    }
}
