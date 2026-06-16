using AutoMapper;
using DnnMigration.Application.DTOs.User;
using DnnMigration.Application.Mapping;
using FluentAssertions;
using Xunit;
using UserEntity = DnnMigration.Domain.Entities.User;

namespace DnnMigration.UnitTests.Mapping;

/// <summary>
/// Round-trip tests for <see cref="UserProfile"/>. Beyond field-preservation, these tests
/// enforce the security contract of the DTO anti-corruption boundary: the read DTO never
/// exposes password material, and the create map never copies the plaintext password onto
/// the entity (the service layer hashes it with BCrypt instead).
/// </summary>
public class UserProfileTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>()).CreateMapper();

    [Fact]
    public void Configuration_IsValid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>());

        configuration.Invoking(c => c.AssertConfigurationIsValid()).Should().NotThrow();
    }

    [Fact]
    public void Map_User_To_UserDto_PreservesProjectedFields_AndComputesFullName()
    {
        var mapper = CreateMapper();
        var entity = new UserEntity
        {
            UserID = 11,
            PortalID = 0,
            AffiliateID = 4,
            Username = "jdoe",
            DisplayName = "John Doe",
            Email = "jdoe@contoso.com",
            FirstName = "John",
            LastName = "Doe",
            IsSuperUser = false,
            Approved = true,
            Roles = new[] { "Administrators", "Registered Users" },
            CreatedDate = new DateTime(2024, 1, 1),
            LastLoginDate = new DateTime(2024, 6, 1),
            LastPasswordChangeDate = new DateTime(2024, 5, 1),
            LastActivityDate = new DateTime(2024, 6, 2),
            Password = "should-not-be-projected"
        };

        var dto = mapper.Map<UserDto>(entity);

        dto.Should().BeEquivalentTo(entity, options => options.ExcludingMissingMembers());
        dto.FullName.Should().Be("John Doe", "FullName is computed from FirstName and LastName");
    }

    [Fact]
    public void Map_CreateUserDto_To_User_CopiesProfileFields_ButNeverCopiesPlaintextPassword()
    {
        var mapper = CreateMapper();
        var dto = new CreateUserDto
        {
            PortalID = 0,
            Username = "newuser",
            Password = "PlaintextSecret!123",
            DisplayName = "New User",
            Email = "newuser@contoso.com",
            FirstName = "New",
            LastName = "User",
            IsSuperUser = false,
            Approved = false
        };

        var entity = mapper.Map<UserEntity>(dto);

        // All non-sensitive create fields are copied across by name (Password excluded below).
        entity.Should().BeEquivalentTo(dto, options => options
            .ExcludingMissingMembers()
            .Excluding(d => d.Password));

        // SECURITY: the profile ignores Password so the plaintext value is never copied onto
        // the entity by AutoMapper; the service layer is responsible for BCrypt hashing.
        entity.Password.Should().BeNull(
            "the mapping profile must ignore Password so plaintext is never persisted via AutoMapper");

        // Server-managed members are ignored on create.
        entity.UserID.Should().Be(0, "UserID is database-generated and ignored on create");
        entity.Roles.Should().BeNull("Roles are assigned via the role subsystem, not on create");
        entity.CreatedDate.Should().BeNull("CreatedDate is server-stamped and ignored on create");
    }

    [Fact]
    public void Map_UpdateUserDto_To_User_CopiesEditableFields_AndIgnoresIdentityAndCredentials()
    {
        var mapper = CreateMapper();
        var dto = new UpdateUserDto
        {
            UserID = 11,
            DisplayName = "John Q. Doe",
            Email = "john.doe@contoso.com",
            FirstName = "John",
            LastName = "Doe",
            IsSuperUser = true,
            Approved = true
        };

        var entity = mapper.Map<UserEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.UserID.Should().Be(11, "UpdateUserDto carries the identity and the profile maps it");
        entity.Username.Should().BeNull("Username is immutable and ignored on update");
        entity.PortalID.Should().Be(0, "PortalID is immutable and ignored on update");
        entity.Password.Should().BeNull("Password is never set through the update profile");
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("PasswordAnswer")]
    [InlineData("PasswordQuestion")]
    public void UserDto_DoesNotExposePasswordMaterial(string propertyName)
    {
        var propertyNames = typeof(UserDto).GetProperties().Select(p => p.Name);

        propertyNames.Should().NotContain(propertyName,
            "the read DTO must never expose password material to API consumers");
    }
}
