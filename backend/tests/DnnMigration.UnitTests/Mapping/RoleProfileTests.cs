using AutoMapper;
using DnnMigration.Application.DTOs.Role;
using DnnMigration.Application.Mapping;
using FluentAssertions;
using Xunit;
using RoleEntity = DnnMigration.Domain.Entities.Role;

namespace DnnMigration.UnitTests.Mapping;

/// <summary>
/// Round-trip tests for <see cref="RoleProfile"/>. Confirms Role -> RoleDto widens the
/// non-nullable entity RoleGroupID to int?, CreateRoleDto -> Role ignores the generated
/// RoleID, and UpdateRoleDto -> Role copies every field including the identity.
/// </summary>
public class RoleProfileTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<RoleProfile>()).CreateMapper();

    [Fact]
    public void Configuration_IsValid()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<RoleProfile>());

        configuration.Invoking(c => c.AssertConfigurationIsValid()).Should().NotThrow();
    }

    [Fact]
    public void Map_Role_To_RoleDto_PreservesAllProjectedFields()
    {
        var mapper = CreateMapper();
        var entity = new RoleEntity
        {
            RoleID = 5,
            PortalID = 0,
            RoleGroupID = 3,
            RoleName = "Administrators",
            Description = "Portal administrators",
            ServiceFee = 0f,
            BillingFrequency = "N",
            TrialPeriod = 0,
            TrialFrequency = "N",
            BillingPeriod = 1,
            TrialFee = 0f,
            IsPublic = false,
            AutoAssignment = false,
            RSVPCode = "RSVP123",
            IconFile = "role.png"
        };

        var dto = mapper.Map<RoleDto>(entity);

        dto.Should().BeEquivalentTo(entity, options => options.ExcludingMissingMembers());
        dto.RoleGroupID.Should().Be(3, "the read DTO widens the non-nullable entity RoleGroupID to int?");
    }

    [Fact]
    public void Map_CreateRoleDto_To_Role_CopiesFields_AndIgnoresRoleId()
    {
        var mapper = CreateMapper();
        var dto = new CreateRoleDto
        {
            PortalID = 0,
            RoleGroupID = 2,
            RoleName = "Members",
            Description = "Registered members",
            ServiceFee = 1.5f,
            BillingFrequency = "M",
            TrialPeriod = 7,
            TrialFrequency = "D",
            BillingPeriod = 1,
            TrialFee = 0f,
            IsPublic = true,
            AutoAssignment = true,
            RSVPCode = "JOIN",
            IconFile = "members.png"
        };

        var entity = mapper.Map<RoleEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.RoleID.Should().Be(0, "RoleID is database-generated and ignored on create");
    }

    [Fact]
    public void Map_UpdateRoleDto_To_Role_CopiesAllFields()
    {
        var mapper = CreateMapper();
        var dto = new UpdateRoleDto
        {
            RoleID = 9,
            PortalID = 1,
            RoleGroupID = 4,
            RoleName = "Editors",
            Description = "Content editors",
            ServiceFee = 2.25f,
            BillingFrequency = "Y",
            TrialPeriod = 30,
            TrialFrequency = "D",
            BillingPeriod = 12,
            TrialFee = 5f,
            IsPublic = false,
            AutoAssignment = false,
            RSVPCode = "EDIT",
            IconFile = "editors.png"
        };

        var entity = mapper.Map<RoleEntity>(dto);

        entity.Should().BeEquivalentTo(dto, options => options.ExcludingMissingMembers());
        entity.RoleID.Should().Be(9, "UpdateRoleDto carries the identity and the profile maps it");
    }
}
