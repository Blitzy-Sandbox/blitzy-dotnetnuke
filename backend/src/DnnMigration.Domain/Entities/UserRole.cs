// -----------------------------------------------------------------------------
// DnnMigration - Modern DotNetNuke Migration
// Copyright (c) 2024. All rights reserved.
// Licensed under the MIT License.
// -----------------------------------------------------------------------------
// MIGRATION: VB.NET DotNetNuke.Entities.Users.UserRoleInfo → C# 12 UserRole entity
// Source: Library/Components/Users/UserRoleInfo.vb
// Changes:
// - Converted from VB.NET Class to C# 12 class with file-scoped namespace
// - No longer inherits from RoleInfo (composition over inheritance)
// - Removed denormalized FullName and Email (available via User navigation property)
// - Converted VB Date to C# DateTime? for EffectiveDate and ExpiryDate
// - Applied nullable reference types
// - Added navigation properties to User and Role
// - Added XML documentation comments
// -----------------------------------------------------------------------------

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Represents the many-to-many relationship between users and roles.
/// </summary>
/// <remarks>
/// <para>
/// This entity maps to the UserRoles table and serves as the join table between
/// <see cref="User"/> and <see cref="Role"/> entities. It includes additional
/// properties for tracking role assignment validity periods and subscription status.
/// </para>
/// <para>
/// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Users.UserRoleInfo.
/// The original class inherited from RoleInfo; this has been changed to composition
/// using navigation properties instead of inheritance.
/// </para>
/// </remarks>
public class UserRole
{
    /// <summary>
    /// Gets or sets the unique identifier for the user-role assignment.
    /// </summary>
    /// <value>The primary key of the user-role record.</value>
    public int UserRoleId { get; set; }

    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    /// <value>The foreign key to the User entity.</value>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier.
    /// </summary>
    /// <value>The foreign key to the Role entity.</value>
    public int RoleId { get; set; }

    /// <summary>
    /// Gets or sets the effective date when the role assignment becomes active.
    /// </summary>
    /// <value>The date when the role becomes active. May be null for immediate activation.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _EffectiveDate field (Date → DateTime?).
    /// </remarks>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the expiry date when the role assignment expires.
    /// </summary>
    /// <value>The date when the role expires. May be null for no expiration.</value>
    /// <remarks>
    /// MIGRATION: Converted from VB.NET _ExpiryDate field (Date → DateTime?).
    /// </remarks>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the trial period has been used.
    /// </summary>
    /// <value><c>true</c> if the trial has been used; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// Used for roles that have trial periods defined in the role configuration.
    /// </remarks>
    public bool IsTrialUsed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user is subscribed to the role.
    /// </summary>
    /// <value><c>true</c> if the user is subscribed; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// Used for roles that require subscription (with ServiceFee).
    /// </remarks>
    public bool Subscribed { get; set; }

    /// <summary>
    /// Gets or sets the date when this record was created.
    /// </summary>
    /// <value>The creation date of the user-role assignment.</value>
    public DateTime CreatedDate { get; set; }

    // Navigation properties

    /// <summary>
    /// Gets or sets the user that has this role assignment.
    /// </summary>
    /// <value>Navigation property to the <see cref="User"/> entity.</value>
    public virtual User? User { get; set; }

    /// <summary>
    /// Gets or sets the role that is assigned to the user.
    /// </summary>
    /// <value>Navigation property to the <see cref="Role"/> entity.</value>
    public virtual Role? Role { get; set; }

    /// <summary>
    /// Gets a value indicating whether the role assignment is currently active.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current date is between EffectiveDate and ExpiryDate; otherwise, <c>false</c>.
    /// </value>
    public bool IsActive
    {
        get
        {
            var now = DateTime.UtcNow;
            var effectiveOk = !EffectiveDate.HasValue || EffectiveDate.Value <= now;
            var expiryOk = !ExpiryDate.HasValue || ExpiryDate.Value > now;
            return effectiveOk && expiryOk;
        }
    }
}
