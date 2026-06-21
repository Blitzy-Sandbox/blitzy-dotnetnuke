namespace DnnMigration.Domain.Entities;

// MIGRATION (Finding CP5 MAJOR — membership-password sourcing): NEW relational entity introduced to restore
// schema fidelity (ADR-002) for credential verification. In legacy DotNetNuke 4.9.0.85 a user's password is
// NOT stored on the application [Users] table; authentication is delegated to the ASP.NET 2.0 Membership
// provider, whose credentials live in the dbo.aspnet_Membership table keyed by the membership UserId (a
// uniqueidentifier). The bridge from a DNN username to that membership row is the dbo.aspnet_Users table,
// whose LoweredUserName column the legacy GetUserByUsername / GetUserMembership stored procedures match on
// (see Website/Providers/DataProviders/SqlDataProvider/InstallMembership.sql — the canonical JOIN is
// "LOWER(@UserName) = u.LoweredUserName AND u.UserId = m.UserId", u = aspnet_Users, m = aspnet_Membership).
//
// This POCO maps the real dbo.aspnet_Users table so UserRepository can resolve the membership UserId from a
// lowered username and then JOIN to aspnet_Membership to source the BCrypt password hash. Mapping these
// physical tables (rather than mutating the preserved [Users] schema) is why CP2 UserConfiguration correctly
// Ignore()s User.Password — Password is not a physical [Users] column. The legacy copyright header and
// Imports are dropped; EF Core column mapping is configured separately via Fluent API
// (IEntityTypeConfiguration<AspNetUser> in AspNetUserConfiguration) in the Infrastructure layer, keeping this
// Domain type free of any framework dependency.

/// <summary>
/// Pure POCO entity for the ASP.NET 2.0 Membership <c>aspnet_Users</c> table, which bridges a DotNetNuke
/// username to the membership <see cref="UserId"/> (a <c>uniqueidentifier</c>) under which the user's
/// credentials are stored in <c>aspnet_Membership</c>.
/// </summary>
/// <remarks>
/// The table's primary key is the <see cref="UserId"/> <c>uniqueidentifier</c>. Within the current scope this
/// entity is read-only — it is queried (matching on <see cref="LoweredUserName"/>) solely to resolve the
/// membership key for a credential lookup, so it carries data only and has no framework dependencies, keeping
/// the Domain layer the dependency-free inner ring.
/// </remarks>
public class AspNetUser
{
    /// <summary>
    /// Gets or sets the membership identifier of the user (primary key; physical column <c>UserId</c>,
    /// <c>uniqueidentifier</c>). This is the key shared with <see cref="AspNetMembership.UserId"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the lower-cased user name used as the canonical match key (physical column
    /// <c>LoweredUserName</c>). The legacy membership stored procedures match on this column with
    /// <c>LOWER(@UserName) = u.LoweredUserName</c>, so the repository compares against an already-lowered
    /// username.
    /// </summary>
    public string LoweredUserName { get; set; } = string.Empty;
}
