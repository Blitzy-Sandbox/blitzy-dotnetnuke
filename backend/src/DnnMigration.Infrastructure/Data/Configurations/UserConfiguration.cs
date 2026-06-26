using DnnMigration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DnnMigration.Infrastructure.Data.Configurations;

// MIGRATION: Fluent API mapping for User (legacy UserInfo, Library/Components/Users/UserInfo.vb,
// namespace DotNetNuke.Entities.Users).
//
// FLATTENING + READ-MODEL NOTE (QA-4 #3, CRITICAL): the C# User entity is a flattened read model that merges the
// legacy UserInfo + UserMembership + UserPortals shape. The physical legacy [Users] table has ONLY 9 columns
// (UserID, Username, FirstName, LastName, IsSuperUser, AffiliateId, Email, DisplayName, UpdatePassword) â€”
// FirstName/LastName ARE real [Users] columns. The remaining User properties are NOT columns of [Users]:
//   - PortalId            -> physically lives in [UserPortals] (the user<->portal membership table).
//   - IsApproved, CreatedDate, LastLoginDate, LastLockoutDate, LockedOut -> [aspnet_Membership].
//   - LastActivityDate    -> [aspnet_Users].
//   - FullName            -> computed (FirstName + ' ' + LastName), never stored.
// Mapping these by EF convention to [Users] emitted phantom columns; against the real SQL Server schema every
// User SELECT/INSERT/UPDATE raised "Invalid column name" -> HTTP 500. PortalId in particular is used by five
// repository LINQ WHERE clauses (tenant scoping), so it CANNOT simply be Ignore()d (that breaks SQL translation).
//
// FIX (two parts): map User to the read VIEW vw_Users instead of the [Users] table â€” the SAME blessed read-model
// pattern the team already applies to Module -> vw_Modules (MIGRATION_NOTES.md Â§13.2/Â§14.2/Â§15.2). The legacy
// vw_Users view (defined in DotNetNuke.Schema.SqlDataProvider) is:
//     CREATE VIEW vw_Users AS
//       SELECT U.UserId, UP.PortalId, U.Username, U.FirstName, U.LastName, U.DisplayName, U.IsSuperUser,
//              U.Email, U.AffiliateId, U.UpdatePassword, UP.Authorised
//       FROM Users U LEFT OUTER JOIN UserPortals UP ON U.UserId = UP.UserId
// i.e. it projects exactly 11 columns and joins ONLY [UserPortals] â€” it does NOT join [aspnet_Membership] or
// [aspnet_Users]. Crucially it DOES expose PortalId (from the [UserPortals] join), so PortalId stays a first-class,
// filterable view column and the five tenant-scoping repository LINQ WHERE clauses translate to valid SQL with zero
// repository/service churn. The nine User properties projected by vw_Users (UserId, PortalId, Username, FirstName,
// LastName, DisplayName, IsSuperUser, Email, AffiliateId) therefore remain EF-mapped.
//
// (2) The SEVEN User properties NOT projected by vw_Users â€” FullName (computed), IsApproved, CreatedDate,
// LastLoginDate, LastLockoutDate, LockedOut (legacy [aspnet_Membership]) and LastActivityDate (legacy
// [aspnet_Users]) â€” are explicitly builder.Ignore()d below. Ignore() drops ONLY the EF column mapping (so EF never
// emits [vw_Users].[IsApproved] etc., which the view does not project); the CLR properties REMAIN on the entity for
// AutoMapper (UserResponse/CurrentUserDto), the AuthService approval/lockout/last-login logic (which operates on
// already-materialized entities), and Identity-layer hydration. These account-status/membership fields are not used
// in any repository LINQ query predicate (verified), so Ignore()ing them is SQL-translation-safe. Persisting them on
// the real DB (the underlying aspnet_* / UserPortals tables) is the same documented read/write split as Module
// (MIGRATION_NOTES.md Â§14.2/Â§15.2): writes target the base tables via the Identity layer, not this read view.
//
// GenerateCreateScript() emits no CREATE TABLE for a ToView entity, so the phantom [Users] columns can never be
// demanded of the physical table; the SchemaFidelityTests metadata guard additionally asserts every EF-mapped User
// column is one the real vw_Users actually projects. The EF Core InMemory gates ignore view/table mapping AND the
// Ignored properties are not asserted by any User integration test, so Gate-5 CRUD (201/200/200/204) is unaffected â€”
// identical to Module. Credentials/passwords remain excluded entirely (Identity layer: JWT + BCrypt).
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // MIGRATION (QA-4 #3): map to the read view, NOT the physical [Users] table (see the class note above).
        builder.ToView("vw_Users");

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasColumnName("UserID");

        // MIGRATION: User 1..N UserRole (preserves the user->role->permission model, AAP 0.7.1).
        // This relationship is OWNED here (configured exactly once); the FK is UserRole.UserId.
        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // MIGRATION (QA-4 #3, CRITICAL â€” completes the vw_Users read-model fix): Ignore the SEVEN properties the
        // real vw_Users view does NOT project (it joins only [UserPortals], not [aspnet_Membership]/[aspnet_Users];
        // see the class note above). Without these Ignore()s, EF maps them by convention and emits
        // [vw_Users].[FullName]/[IsApproved]/... on every User read -> "Invalid column name" -> HTTP 500 against the
        // real SQL Server. Ignore() drops ONLY the EF column mapping; the CLR properties remain for AutoMapper, the
        // AuthService approval/lockout/last-login logic (on materialized entities), and Identity-layer hydration.
        // None of the seven is referenced in a repository LINQ predicate, so this is SQL-translation-safe.
        builder.Ignore(u => u.FullName);          // computed (FirstName + ' ' + LastName); never stored
        builder.Ignore(u => u.IsApproved);        // legacy [aspnet_Membership].IsApproved
        builder.Ignore(u => u.CreatedDate);       // legacy [aspnet_Membership].CreateDate
        builder.Ignore(u => u.LastLoginDate);     // legacy [aspnet_Membership].LastLoginDate
        builder.Ignore(u => u.LastActivityDate);  // legacy [aspnet_Users].LastActivityDate
        builder.Ignore(u => u.LastLockoutDate);   // legacy [aspnet_Membership].LastLockoutDate
        builder.Ignore(u => u.LockedOut);         // legacy [aspnet_Membership].IsLockedOut

        // NOTE: the nine vw_Users-projected scalar columns (Username, DisplayName, Email, IsSuperUser, AffiliateId,
        // FirstName, LastName, and the multi-tenant discriminator PortalId â€” all real columns of vw_Users) rely on
        // EF convention (case-insensitive SQL Server collation). Only the key gets an explicit HasColumnName here;
        // the FK column UserRole.UserId -> "UserID" is mapped in UserRoleConfiguration.
    }
}
