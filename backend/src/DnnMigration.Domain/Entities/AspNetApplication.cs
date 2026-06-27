namespace DnnMigration.Domain.Entities;

// MIGRATION: persistence-mapping POCO for the EXISTING legacy ASP.NET 2.0 membership table [aspnet_Applications]
// (installed by Website/Providers/DataProviders/SqlDataProvider/InstallCommon.sql). It is NOT a core domain
// aggregate; it exists ONLY so the Infrastructure CredentialStore adapter can map BCrypt credential material onto
// the membership schema that already ships with the DNN database. The legacy AspNetSqlMembershipProvider COMPONENT
// is removed (AAP 0.5.2) and the reversible DES/SHA password is replaced by a one-way BCrypt hash (AAP 0.7.6), but
// the underlying aspnet_* TABLES are part of the existing schema this phase maps to (AAP 0.7.1 - no schema
// alteration). The DNN integer [Users].UserID is bridged to the GUID-keyed membership chain by Username:
// [Users].Username -> aspnet_Users.UserName -> aspnet_Membership.UserId.
//
// The legacy install script scopes every membership row to an application identified by ApplicationName; DNN
// configures this as "DotNetNuke" (Website/release.config L246). ApplicationId is a GUID (DEFAULT NEWID() in the
// physical schema) and is generated in application code here so EF inserts the value we choose (ValueGeneratedNever
// in AspNetApplicationConfiguration).
public class AspNetApplication
{
    // MIGRATION: [aspnet_Applications].[ApplicationId] uniqueidentifier PRIMARY KEY (physical DEFAULT NEWID()).
    public Guid ApplicationId { get; set; }

    // MIGRATION: [aspnet_Applications].[ApplicationName] nvarchar(256) NOT NULL (UNIQUE). DNN value: "DotNetNuke".
    public string ApplicationName { get; set; } = string.Empty;

    // MIGRATION: [aspnet_Applications].[LoweredApplicationName] nvarchar(256) NOT NULL (UNIQUE, CLUSTERED). The
    // lower-cased ApplicationName the provider matches on; populated by the adapter via ToLowerInvariant().
    public string LoweredApplicationName { get; set; } = string.Empty;

    // MIGRATION: [aspnet_Applications].[Description] nvarchar(256) NULL.
    public string? Description { get; set; }
}
