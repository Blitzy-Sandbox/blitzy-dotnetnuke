namespace DnnMigration.Domain.Entities;

// MIGRATION (Finding CP5 MAJOR — membership-password sourcing): NEW relational entity introduced to restore
// schema fidelity (ADR-002) for credential verification. The user's password hash lives in the physical
// dbo.aspnet_Membership table (created by aspnet_regsql.exe and consumed by InstallMembership.sql), keyed by
// the membership UserId (a uniqueidentifier) shared with dbo.aspnet_Users. The legacy DNN authentication path
// resolves a row here via "u.UserId = m.UserId" after matching the lowered username on aspnet_Users
// (InstallMembership.sql). Mapping this physical table — rather than adding a Password column to the preserved
// [Users] schema — is why CP2 UserConfiguration correctly Ignore()s User.Password.
//
// Only the columns the credential lookup requires are modelled (UserId key + Password). The aspnet_Membership
// table carries many other columns (PasswordFormat, PasswordSalt, IsApproved, IsLockedOut, ...); they are not
// mapped because they are out of scope for the Phase-1 BCrypt verification path, and EF Core maps only the
// declared properties (the unmapped physical columns are simply ignored, never altered — ADR-002). EF Core
// column mapping is configured separately via Fluent API (IEntityTypeConfiguration<AspNetMembership> in
// AspNetMembershipConfiguration) in the Infrastructure layer; this Domain type carries zero framework deps.

/// <summary>
/// Pure POCO entity for the ASP.NET 2.0 Membership <c>aspnet_Membership</c> table, exposing the credential
/// hash (<see cref="Password"/>) keyed by the membership <see cref="UserId"/>.
/// </summary>
/// <remarks>
/// The table's primary key is the <see cref="UserId"/> <c>uniqueidentifier</c>, shared 1:1 with
/// <see cref="AspNetUser.UserId"/>. Within the current scope this entity is read-only — it is queried (joined
/// from <see cref="AspNetUser"/>) solely to source the password hash for <c>AuthService.LoginAsync</c>'s
/// BCrypt verification. It carries data only and has no framework dependencies.
/// </remarks>
public class AspNetMembership
{
    /// <summary>
    /// Gets or sets the membership identifier (primary key; physical column <c>UserId</c>,
    /// <c>uniqueidentifier</c>). Shared with <see cref="AspNetUser.UserId"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the stored password hash (physical column <c>Password</c>). Under the migrated security
    /// model this column holds a BCrypt hash (DEV-033 — DES replaced by BCrypt) that
    /// <c>AuthService.LoginAsync</c> verifies via <c>IPasswordHasher.Verify</c>.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
