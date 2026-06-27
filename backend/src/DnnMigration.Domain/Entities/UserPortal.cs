namespace DnnMigration.Domain.Entities;

// MIGRATION: [QA-FINAL Issue #1/#2 — CRITICAL] Command-model entity for the EXISTING legacy [UserPortals] table
// (DotNetNuke.Schema.SqlDataProvider: UserId, PortalId, UserPortalId IDENTITY PK, CreatedDate, Authorised). Schema
// NOT altered. This is the WRITE side of the user<->portal membership that the read view vw_Users surfaces as its
// PortalId/Authorised columns (vw_Users = Users LEFT JOIN UserPortals). The User entity maps to BOTH [Users] (write)
// and vw_Users (read); because the physical [Users] table has NO PortalId column, a user's portal membership is
// persisted here instead. UserRepository.AddAsync stages a UserPortal alongside each new User so the (real SQL Server)
// INSERT fans out to [Users] + [UserPortals], and the next read via vw_Users reassembles PortalId from this row.
// This entity is intentionally minimal (no business behavior) — Clean/Onion persistence-ignorant POCO (AAP §0.3.3/§0.7.3).
public sealed class UserPortal
{
    // MIGRATION: [UserPortals].[UserPortalId] int IDENTITY(1,1) PRIMARY KEY (store-generated on insert).
    public int UserPortalId { get; set; }

    // MIGRATION: [UserPortals].[UserId] int NOT NULL — FK to [Users].[UserID]. Set via the User navigation so EF
    // fixes it up from the store-generated User.UserId within the single SaveChanges commit boundary.
    public int UserId { get; set; }

    // MIGRATION: [UserPortals].[PortalId] int NOT NULL — the tenant the user belongs to (multi-tenant isolation, AAP §0.7.1).
    public int PortalId { get; set; }

    // MIGRATION: [UserPortals].[CreatedDate] datetime NOT NULL (legacy DEFAULT getdate()).
    public DateTime CreatedDate { get; set; }

    // MIGRATION: [UserPortals].[Authorised] bit NOT NULL (legacy DEFAULT 1) — the user's authorization within the
    // portal. Mirrors the User.IsApproved flag at creation time.
    public bool Authorised { get; set; }

    // MIGRATION: Navigation to the owning User (FK = UserId). Optional reference; lets AddAsync rely on EF FK fixup
    // for the store-generated UserId. No inverse collection is added to User (minimal blast radius).
    public User? User { get; set; }
}
