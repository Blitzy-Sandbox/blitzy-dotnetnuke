namespace DnnMigration.Domain.Entities;

// MIGRATION: persistence-mapping POCO for the EXISTING legacy ASP.NET 2.0 membership table [aspnet_Membership]
// (installed by Website/Providers/DataProviders/SqlDataProvider/InstallMembership.sql). It is the credential row the
// legacy AspNetSqlMembershipProvider wrote/read; this migration REUSES the existing table (AAP 0.7.1 - no schema
// alteration) but stores a one-way BCrypt hash in [Password] in place of the legacy reversible/SHA value
// (AAP 0.7.6). The provider COMPONENT is removed (AAP 0.5.2); the TABLE is part of the existing schema.
//
// Used ONLY by the Infrastructure CredentialStore adapter (not a domain aggregate). Keyed 1:1 to aspnet_Users by
// the GUID UserId. The full legacy column set (21 columns, 14 NOT NULL) is modelled so the adapter can insert a
// faithful, schema-valid row; CredentialStore populates every NOT NULL column on insert.
public class AspNetMembership
{
    // MIGRATION: [aspnet_Membership].[ApplicationId] uniqueidentifier NOT NULL (FK -> aspnet_Applications).
    public Guid ApplicationId { get; set; }

    // MIGRATION: [aspnet_Membership].[UserId] uniqueidentifier NOT NULL PRIMARY KEY (FK -> aspnet_Users.UserId, 1:1).
    public Guid UserId { get; set; }

    // MIGRATION: [aspnet_Membership].[Password] nvarchar(128) NOT NULL. Stores the one-way BCrypt hash (60 chars,
    // comfortably within 128). Replaces the legacy reversible/SHA password (AAP 0.7.6). NEVER plaintext, NEVER logged.
    public string Password { get; set; } = string.Empty;

    // MIGRATION: [aspnet_Membership].[PasswordFormat] int NOT NULL (legacy DEFAULT 0). Set to 1 (Hashed) because the
    // stored value is a one-way BCrypt hash, not clear (0) or the legacy reversible (2) format.
    public int PasswordFormat { get; set; }

    // MIGRATION: [aspnet_Membership].[PasswordSalt] nvarchar(128) NOT NULL. BCrypt embeds its own per-hash salt, so
    // this legacy column is stored as an empty string (the salt is part of the [Password] hash, not separate).
    public string PasswordSalt { get; set; } = string.Empty;

    // MIGRATION: [aspnet_Membership].[MobilePIN] nvarchar(16) NULL.
    public string? MobilePIN { get; set; }

    // MIGRATION: [aspnet_Membership].[Email] nvarchar(256) NULL.
    public string? Email { get; set; }

    // MIGRATION: [aspnet_Membership].[LoweredEmail] nvarchar(256) NULL.
    public string? LoweredEmail { get; set; }

    // MIGRATION: [aspnet_Membership].[PasswordQuestion] nvarchar(256) NULL.
    public string? PasswordQuestion { get; set; }

    // MIGRATION: [aspnet_Membership].[PasswordAnswer] nvarchar(128) NULL.
    public string? PasswordAnswer { get; set; }

    // MIGRATION: [aspnet_Membership].[IsApproved] bit NOT NULL. The persisted approval state surfaced on the migrated
    // User.IsApproved (which is Ignore()d on the User mapping because it physically lives here). AuthService writes it
    // via ICredentialStore.SetApprovedAsync so the verification-code approval persists to SQL Server.
    public bool IsApproved { get; set; }

    // MIGRATION: [aspnet_Membership].[IsLockedOut] bit NOT NULL. False for migrated/created accounts.
    public bool IsLockedOut { get; set; }

    // MIGRATION: [aspnet_Membership].[CreateDate] datetime NOT NULL. Stamped (UTC) when the credential is created.
    public DateTime CreateDate { get; set; }

    // MIGRATION: [aspnet_Membership].[LastLoginDate] datetime NOT NULL. Updated via ICredentialStore.RecordLoginAsync
    // on successful authentication so the migrated User.LastLoginDate (Ignore()d on User) persists to SQL Server.
    public DateTime LastLoginDate { get; set; }

    // MIGRATION: [aspnet_Membership].[LastPasswordChangedDate] datetime NOT NULL. Stamped on create and on each
    // password replacement.
    public DateTime LastPasswordChangedDate { get; set; }

    // MIGRATION: [aspnet_Membership].[LastLockoutDate] datetime NOT NULL. Set to the SQL sentinel (1754-01-01) when
    // the account has never been locked out (DateTime.MinValue is OUT OF RANGE for SQL Server datetime).
    public DateTime LastLockoutDate { get; set; }

    // MIGRATION: [aspnet_Membership].[FailedPasswordAttemptCount] int NOT NULL. 0 on create.
    public int FailedPasswordAttemptCount { get; set; }

    // MIGRATION: [aspnet_Membership].[FailedPasswordAttemptWindowStart] datetime NOT NULL. SQL sentinel on create.
    public DateTime FailedPasswordAttemptWindowStart { get; set; }

    // MIGRATION: [aspnet_Membership].[FailedPasswordAnswerAttemptCount] int NOT NULL. 0 on create.
    public int FailedPasswordAnswerAttemptCount { get; set; }

    // MIGRATION: [aspnet_Membership].[FailedPasswordAnswerAttemptWindowStart] datetime NOT NULL. SQL sentinel on create.
    public DateTime FailedPasswordAnswerAttemptWindowStart { get; set; }

    // MIGRATION: [aspnet_Membership].[Comment] ntext NULL.
    public string? Comment { get; set; }
}
