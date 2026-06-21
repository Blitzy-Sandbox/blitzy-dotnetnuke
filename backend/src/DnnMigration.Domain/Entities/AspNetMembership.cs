namespace DnnMigration.Domain.Entities;

// MIGRATION (Finding CP5 MAJOR — membership-password sourcing): NEW relational entity introduced to restore
// schema fidelity (ADR-002) for credential verification. The user's password hash lives in the physical
// dbo.aspnet_Membership table (created by aspnet_regsql.exe and consumed by InstallMembership.sql), keyed by
// the membership UserId (a uniqueidentifier) shared with dbo.aspnet_Users. The legacy DNN authentication path
// resolves a row here via "u.UserId = m.UserId" after matching the lowered username on aspnet_Users
// (InstallMembership.sql). Mapping this physical table — rather than adding a Password column to the preserved
// [Users] schema — is why CP2 UserConfiguration correctly Ignore()s User.Password.
//
// The credential hash (UserId key + Password) plus the membership-state columns the legacy
// Website/admin/Users/Membership.ascx.vb workflow manages are modelled here: IsApproved, IsLockedOut,
// FailedPasswordAttemptCount, and LastLockoutDate (Finding CP-FINAL-6 / DEV-067 — restores authorize /
// unauthorize / unlock parity). These are EXISTING physical [aspnet_Membership] columns
// (InstallMembership.sql L90-96), so mapping them changes NO schema: ADR-002 forbids schema CHANGES, not
// mapping more of the preserved schema (no migration, no generation, no data migration). The table still
// carries further columns (PasswordFormat, PasswordSalt, PasswordQuestion, ...) that remain unmapped; EF Core
// maps only the declared properties and leaves the unmapped physical columns untouched (ADR-002). EF Core
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

    /// <summary>
    /// Gets or sets whether the membership is approved (physical column <c>IsApproved</c>, <c>bit NOT NULL</c>).
    /// MIGRATION (DEV-067): the target of the legacy <c>cmdAuthorize</c>/<c>cmdUnAuthorize</c> admin actions
    /// (Membership.ascx.vb). An unapproved account cannot authenticate (legacy <c>LOGIN_USERNOTAPPROVED</c>).
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// Gets or sets whether the membership is locked out after too many failed attempts (physical column
    /// <c>IsLockedOut</c>, <c>bit NOT NULL</c>). MIGRATION (DEV-067): cleared by the legacy <c>cmdUnLock</c>
    /// admin action (Membership.ascx.vb), which also resets <see cref="FailedPasswordAttemptCount"/>.
    /// </summary>
    public bool IsLockedOut { get; set; }

    /// <summary>
    /// Gets or sets the count of consecutive failed password attempts (physical column
    /// <c>FailedPasswordAttemptCount</c>, <c>int NOT NULL</c>). MIGRATION (DEV-067): reset to zero by the
    /// unlock transition, mirroring the legacy <c>aspnet_Membership_UnlockUser</c> stored procedure.
    /// </summary>
    public int FailedPasswordAttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent lockout (physical column <c>LastLockoutDate</c>,
    /// <c>datetime NOT NULL</c>). When the account has never been locked out the legacy schema stores the
    /// sentinel <see cref="NeverLockedOutDate"/> (<c>CONVERT(datetime,'17540101',112)</c> per
    /// InstallMembership.sql); the unlock transition resets this column to that sentinel.
    /// </summary>
    public DateTime LastLockoutDate { get; set; } = NeverLockedOutDate;

    /// <summary>
    /// The legacy ASP.NET 2.0 Membership "never locked out" sentinel date
    /// (<c>CONVERT(datetime,'17540101',112)</c> = 1754-01-01) used by <c>aspnet_Membership_CreateUser</c> and
    /// <c>aspnet_Membership_UnlockUser</c> (InstallMembership.sql L169, L629, L1057). It is SQL-Server
    /// <c>datetime</c>-range-safe (the type's minimum is 1753-01-03) and signals an account that has never been
    /// locked out; the read projection treats this (and any earlier value) as "no lockout".
    /// </summary>
    public static readonly DateTime NeverLockedOutDate = new(1754, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
