using DnnMigration.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace DnnMigration.UnitTests.Domain;

/// <summary>
/// Enum-preservation tests for <see cref="UserCreateStatus"/>.
/// DNN 4.9.0.85 persisted/compared these as integers, so the verbatim values must be preserved
/// to keep behavioral parity (AAP §0.6.3, §0.7.1).
/// Source of truth: Library/Components/Users/Membership/UserCreateStatus.vb (explicit values 0-17).
/// </summary>
public class UserCreateStatusTests
{
    [Theory]
    [InlineData(UserCreateStatus.AddUser, 0)]
    [InlineData(UserCreateStatus.UsernameAlreadyExists, 1)]
    [InlineData(UserCreateStatus.UserAlreadyRegistered, 2)]
    [InlineData(UserCreateStatus.DuplicateEmail, 3)]
    [InlineData(UserCreateStatus.DuplicateProviderUserKey, 4)]
    [InlineData(UserCreateStatus.DuplicateUserName, 5)]
    [InlineData(UserCreateStatus.InvalidAnswer, 6)]
    [InlineData(UserCreateStatus.InvalidEmail, 7)]
    [InlineData(UserCreateStatus.InvalidPassword, 8)]
    [InlineData(UserCreateStatus.InvalidProviderUserKey, 9)]
    [InlineData(UserCreateStatus.InvalidQuestion, 10)]
    [InlineData(UserCreateStatus.InvalidUserName, 11)]
    [InlineData(UserCreateStatus.ProviderError, 12)]
    [InlineData(UserCreateStatus.Success, 13)]
    [InlineData(UserCreateStatus.UnexpectedError, 14)]
    [InlineData(UserCreateStatus.UserRejected, 15)]
    [InlineData(UserCreateStatus.PasswordMismatch, 16)]
    [InlineData(UserCreateStatus.AddUserToPortal, 17)]
    public void Member_HasVerbatimLegacyIntegerValue(UserCreateStatus member, int expectedValue)
    {
        ((int)member).Should().Be(expectedValue);
    }

    [Fact]
    public void Enum_DefinesExactlyEighteenMembers()
    {
        Enum.GetValues<UserCreateStatus>().Should().HaveCount(18);
    }
}
