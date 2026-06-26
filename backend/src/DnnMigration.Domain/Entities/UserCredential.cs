namespace DnnMigration.Domain.Entities;

// MIGRATION (CP2 review — DependencyInjection #1 / Program.cs #4): credential-store entity backing the
// Application ICredentialStore port (Application/Interfaces/ICredentialStore.cs). It REPLACES the legacy
// ASP.NET 2.0 membership credential table (aspnet_Membership, written through AspNetSqlMembershipProvider),
// which AAP §0.5.2 explicitly lists as REMOVED in favour of "ASP.NET Core JWT auth + BCrypt". In the legacy
// system the password material lived OUTSIDE the [Users] row (the migrated User entity therefore carries no
// credential fields, AAP §0.7.6); this dedicated table preserves that separation of concerns while storing a
// one-way BCrypt hash (produced by IPasswordHasher) instead of the legacy GUID-keyed, salted aspnet_Membership
// password.
//
// SCHEMA NOTE (MIGRATION_NOTES.md): this is a documented schema-COMPATIBILITY addition, NOT an alteration of any
// existing legacy table and NOT an EF migration (the project runs no Database.Migrate / EnsureCreated / schema
// SQL — Rules item #2). It models the migrated BCrypt identity store that supersedes the GUID-keyed
// aspnet_Membership chain (Users.Username -> aspnet_Users.UserName -> aspnet_Membership.UserId); the credential
// is keyed directly on the DNN integer UserId for a clean 1:1 relationship with the migrated [Users] row.
public class UserCredential
{
    // MIGRATION: keyed on the DNN integer UserId (== User.UserId / [Users].[UserID]). One credential row per user;
    // referential integrity to the migrated [Users] table is by-convention (no FK navigation is modelled so the
    // credential lifecycle stays decoupled from the User aggregate and from any cascade path).
    public int UserId { get; set; }

    // MIGRATION: the one-way BCrypt hash (BCrypt.Net-Next) produced by IPasswordHasher.Hash. NEVER a plaintext
    // password and NEVER logged. Replaces the legacy reversible aspnet_Membership Password/PasswordSalt pair.
    public string PasswordHash { get; set; } = string.Empty;

    // MIGRATION: audit timestamps (UTC). Provide credential-lifecycle traceability without exposing any secret
    // material. CreatedDate is stamped when the credential is first persisted; LastModifiedDate is stamped on each
    // subsequent password replacement.
    public DateTime CreatedDate { get; set; }

    public DateTime? LastModifiedDate { get; set; }
}
