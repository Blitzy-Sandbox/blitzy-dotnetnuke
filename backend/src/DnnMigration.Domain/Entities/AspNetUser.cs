namespace DnnMigration.Domain.Entities;

// MIGRATION: persistence-mapping POCO for the EXISTING legacy ASP.NET 2.0 membership table [aspnet_Users]
// (installed by Website/Providers/DataProviders/SqlDataProvider/InstallCommon.sql). Like AspNetApplication this is
// a persistence detail used ONLY by the Infrastructure CredentialStore adapter - NOT a domain aggregate. It is the
// GUID-keyed membership identity that the legacy provider keyed credentials on; the DNN integer [Users].UserID is
// bridged to it by Username ([Users].Username -> aspnet_Users.UserName) so the migrated BCrypt hash can be stored
// in the existing aspnet_Membership row WITHOUT adding any new table (AAP 0.7.1).
//
// UserId is a GUID (physical DEFAULT NEWID()) and is generated in application code here so EF inserts the value we
// choose (ValueGeneratedNever in AspNetUserConfiguration). It is DISTINCT from the DNN integer User.UserId.
public class AspNetUser
{
    // MIGRATION: [aspnet_Users].[ApplicationId] uniqueidentifier NOT NULL (FK -> aspnet_Applications.ApplicationId,
    // enforced by the physical schema). Scopes the user to its application.
    public Guid ApplicationId { get; set; }

    // MIGRATION: [aspnet_Users].[UserId] uniqueidentifier PRIMARY KEY (physical DEFAULT NEWID()). The membership
    // identity that aspnet_Membership.UserId references 1:1.
    public Guid UserId { get; set; }

    // MIGRATION: [aspnet_Users].[UserName] nvarchar(256) NOT NULL. Equals the DNN [Users].Username for the bridge.
    public string UserName { get; set; } = string.Empty;

    // MIGRATION: [aspnet_Users].[LoweredUserName] nvarchar(256) NOT NULL (part of the UNIQUE CLUSTERED index with
    // ApplicationId). The lower-cased UserName the provider matches on; populated via ToLowerInvariant().
    public string LoweredUserName { get; set; } = string.Empty;

    // MIGRATION: [aspnet_Users].[MobileAlias] nvarchar(16) NULL (physical DEFAULT NULL).
    public string? MobileAlias { get; set; }

    // MIGRATION: [aspnet_Users].[IsAnonymous] bit NOT NULL (physical DEFAULT 0). Migrated users are never anonymous.
    public bool IsAnonymous { get; set; }

    // MIGRATION: [aspnet_Users].[LastActivityDate] datetime NOT NULL. Stamped on credential creation.
    public DateTime LastActivityDate { get; set; }
}
