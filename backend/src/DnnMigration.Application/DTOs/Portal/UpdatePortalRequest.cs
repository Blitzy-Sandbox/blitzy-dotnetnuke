// -----------------------------------------------------------------------------
// DnnMigration - Portal Update Request DTO
// MIGRATION: Derived from DotNetNuke 4.x SiteSettings.ascx.vb cmdUpdate_Click handler
// MIGRATION: UpdatePortalInfo method parameters (lines 772-781) converted to C# 12 record
// -----------------------------------------------------------------------------

namespace DnnMigration.Application.DTOs.Portal;

/// <summary>
/// Data Transfer Object representing a request to update an existing portal's settings.
/// Contains all fields required to update portal configuration including branding, 
/// navigation, quotas, billing, and administration settings.
/// </summary>
/// <remarks>
/// MIGRATION: This DTO is derived from the SiteSettings.ascx.vb cmdUpdate_Click handler
/// which calls UpdatePortalInfo with the following parameters:
/// (intPortalId, txtPortalName.Text, strLogo, txtFooterText.Text, datExpiryDate,
/// optUserRegistration.SelectedIndex, optBanners.SelectedIndex, cboCurrency.SelectedItem.Value,
/// cboAdministratorId.SelectedItem.Value, dblHostFee, dblHostSpace, intPageQuota, intUserQuota,
/// cboProcessor.SelectedItem.Text, txtUserId.Text, txtPassword.Text, txtDescription.Text,
/// txtKeyWords.Text, strBackground, intSiteLogHistory, intSplashTabId, intHomeTabId,
/// intLoginTabId, intUserTabId, cboDefaultLanguage.SelectedValue, cboTimeZone.SelectedValue,
/// txtHomeDirectory.Text)
/// </remarks>
public record UpdatePortalRequest
{
    #region Branding Properties

    /// <summary>
    /// Gets or sets the portal display name.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtPortalName.Text in SiteSettings.ascx.vb
    /// </remarks>
    public required string PortalName { get; init; }

    /// <summary>
    /// Gets or sets the relative path to the portal logo image file.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From strLogo (urlLogo.Url) in SiteSettings.ascx.vb
    /// </remarks>
    public string? LogoFile { get; init; }

    /// <summary>
    /// Gets or sets the footer text displayed at the bottom of portal pages.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtFooterText.Text in SiteSettings.ascx.vb
    /// </remarks>
    public string? FooterText { get; init; }

    /// <summary>
    /// Gets or sets the relative path to the portal background image file.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From strBackground (urlBackground.Url) in SiteSettings.ascx.vb
    /// </remarks>
    public string? BackgroundFile { get; init; }

    #endregion

    #region SEO Properties

    /// <summary>
    /// Gets or sets the portal description used for SEO meta tags.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtDescription.Text in SiteSettings.ascx.vb
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the portal keywords used for SEO meta tags.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtKeyWords.Text in SiteSettings.ascx.vb
    /// </remarks>
    public string? KeyWords { get; init; }

    #endregion

    #region Configuration Properties

    /// <summary>
    /// Gets or sets the user registration mode for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From optUserRegistration.SelectedIndex in SiteSettings.ascx.vb
    /// Values: 0 = None, 1 = Private, 2 = Public, 3 = Verified
    /// </remarks>
    public int UserRegistration { get; init; }

    /// <summary>
    /// Gets or sets the banner advertising mode for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From optBanners.SelectedIndex in SiteSettings.ascx.vb
    /// Values: 0 = None, 1 = Site, 2 = Host
    /// </remarks>
    public int BannerAdvertising { get; init; }

    /// <summary>
    /// Gets or sets the default language code for the portal (e.g., "en-US").
    /// </summary>
    /// <remarks>
    /// MIGRATION: From cboDefaultLanguage.SelectedValue in SiteSettings.ascx.vb
    /// </remarks>
    public string? DefaultLanguage { get; init; }

    /// <summary>
    /// Gets or sets the time zone offset in minutes from UTC.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From Convert.ToInt32(cboTimeZone.SelectedValue) in SiteSettings.ascx.vb
    /// </remarks>
    public int TimeZoneOffset { get; init; }

    /// <summary>
    /// Gets or sets the home directory path for portal files.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtHomeDirectory.Text in SiteSettings.ascx.vb
    /// Typically in format "Portals/{PortalId}"
    /// </remarks>
    public string? HomeDirectory { get; init; }

    #endregion

    #region Navigation Tab Properties

    /// <summary>
    /// Gets or sets the Tab ID for the portal splash page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intSplashTabId (cboSplashTabId.SelectedItem.Value) in SiteSettings.ascx.vb
    /// Null or -1 indicates no splash page.
    /// </remarks>
    public int? SplashTabId { get; init; }

    /// <summary>
    /// Gets or sets the Tab ID for the portal home page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intHomeTabId (cboHomeTabId.SelectedItem.Value) in SiteSettings.ascx.vb
    /// Null or -1 indicates no home page specified.
    /// </remarks>
    public int? HomeTabId { get; init; }

    /// <summary>
    /// Gets or sets the Tab ID for the portal login page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intLoginTabId (cboLoginTabId.SelectedItem.Value) in SiteSettings.ascx.vb
    /// Null or -1 indicates default login behavior.
    /// </remarks>
    public int? LoginTabId { get; init; }

    /// <summary>
    /// Gets or sets the Tab ID for the user profile/account page.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intUserTabId (cboUserTabId.SelectedItem.Value) in SiteSettings.ascx.vb
    /// Null or -1 indicates default user profile behavior.
    /// </remarks>
    public int? UserTabId { get; init; }

    #endregion

    #region Quota Properties

    /// <summary>
    /// Gets or sets the maximum number of pages allowed for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intPageQuota (txtPageQuota.Text) in SiteSettings.ascx.vb
    /// Value of 0 indicates unlimited pages.
    /// </remarks>
    public int PageQuota { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of users allowed for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intUserQuota (txtUserQuota.Text) in SiteSettings.ascx.vb
    /// Value of 0 indicates unlimited users.
    /// </remarks>
    public int UserQuota { get; init; }

    /// <summary>
    /// Gets or sets the disk space quota in megabytes allocated to the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From dblHostSpace (txtHostSpace.Text) in SiteSettings.ascx.vb
    /// Value of 0 indicates unlimited disk space.
    /// </remarks>
    public int HostSpace { get; init; }

    #endregion

    #region Billing Properties

    /// <summary>
    /// Gets or sets the monthly hosting fee for the portal.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From dblHostFee (txtHostFee.Text) in SiteSettings.ascx.vb
    /// VB.NET used Single (float), migrated to decimal for monetary precision.
    /// </remarks>
    public decimal HostFee { get; init; }

    /// <summary>
    /// Gets or sets the currency code for billing (e.g., "USD", "EUR").
    /// </summary>
    /// <remarks>
    /// MIGRATION: From cboCurrency.SelectedItem.Value in SiteSettings.ascx.vb
    /// </remarks>
    public string? Currency { get; init; }

    /// <summary>
    /// Gets or sets the payment processor name (e.g., "PayPal").
    /// </summary>
    /// <remarks>
    /// MIGRATION: From IIf(cboProcessor.SelectedValue = "", "", cboProcessor.SelectedItem.Text) in SiteSettings.ascx.vb
    /// </remarks>
    public string? PaymentProcessor { get; init; }

    /// <summary>
    /// Gets or sets the payment processor user/merchant ID.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtUserId.Text in SiteSettings.ascx.vb
    /// </remarks>
    public string? ProcessorUserId { get; init; }

    /// <summary>
    /// Gets or sets the payment processor password or API key.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From txtPassword.Text in SiteSettings.ascx.vb
    /// WARNING: This is sensitive data and should be handled securely.
    /// Consider encrypting in transit and at rest.
    /// </remarks>
    public string? ProcessorPassword { get; init; }

    #endregion

    #region Administration Properties

    /// <summary>
    /// Gets or sets the User ID of the portal administrator.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From Convert.ToInt32(cboAdministratorId.SelectedItem.Value) in SiteSettings.ascx.vb
    /// </remarks>
    public int AdministratorId { get; init; }

    /// <summary>
    /// Gets or sets the portal expiration date.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From datExpiryDate in SiteSettings.ascx.vb
    /// Null indicates the portal never expires.
    /// </remarks>
    public DateTime? ExpiryDate { get; init; }

    /// <summary>
    /// Gets or sets the number of days to retain site log history.
    /// </summary>
    /// <remarks>
    /// MIGRATION: From intSiteLogHistory (txtSiteLogHistory.Text) in SiteSettings.ascx.vb
    /// Value of 0 indicates logs are kept indefinitely.
    /// </remarks>
    public int SiteLogHistory { get; init; }

    #endregion
}
