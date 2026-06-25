namespace DnnMigration.Application.DTOs.Portal;

// MIGRATION: Read/response DTO for the Portal resource (GET /api/portals/{id}); placed in the "data" field of the
// success envelope { data, meta }. Projected from the Domain entity DnnMigration.Domain.Entities.Portal by the
// sibling Mapping/PortalProfile (AutoMapper). This file is a plain shape only — no logic, no entity import.
// MIGRATION: Canonical name is PortalDto; "PortalResponse" is an accepted alias for this same read model.
// MIGRATION (SECURITY, AAP 0.7.6/0.7.5): ProcessorPassword (payment-processor secret) is intentionally OMITTED
// from this response contract so it is never serialized to clients or written to logs.
public record PortalDto
{
    public int PortalId { get; init; }

    public string PortalName { get; init; } = string.Empty;

    public string? LogoFile { get; init; }

    public string? FooterText { get; init; }

    public DateTime? ExpiryDate { get; init; }

    public int UserRegistration { get; init; }

    public int BannerAdvertising { get; init; }

    public int AdministratorId { get; init; }

    public string? Currency { get; init; }

    public float HostFee { get; init; }

    public int HostSpace { get; init; }

    public int PageQuota { get; init; }

    public int UserQuota { get; init; }

    public int AdministratorRoleId { get; init; }

    public string? AdministratorRoleName { get; init; }

    public int RegisteredRoleId { get; init; }

    public string? RegisteredRoleName { get; init; }

    public string? Description { get; init; }

    public string? KeyWords { get; init; }

    public string? BackgroundFile { get; init; }

    public Guid Guid { get; init; }

    public string? PaymentProcessor { get; init; }

    // MIGRATION: ProcessorPassword intentionally excluded (see header). ProcessorUserId is not a secret and is retained.
    public string? ProcessorUserId { get; init; }

    public int SiteLogHistory { get; init; }

    public string? Email { get; init; }

    public int AdminTabId { get; init; }

    public int SuperTabId { get; init; }

    // MIGRATION: Legacy PortalInfo.Users lazy getter (UserController.GetUserCountByPortal) dropped on the entity;
    // this nullable count is populated by the Application/service layer.
    public int? Users { get; init; }

    // MIGRATION: Legacy PortalInfo.Pages lazy getter (TabController.GetTabCount) dropped on the entity;
    // this nullable count is populated by the Application/service layer.
    public int? Pages { get; init; }

    public int SplashTabId { get; init; }

    public int HomeTabId { get; init; }

    public int LoginTabId { get; init; }

    public int UserTabId { get; init; }

    public string? DefaultLanguage { get; init; }

    public int TimeZoneOffset { get; init; }

    public string? HomeDirectory { get; init; }

    public string? Version { get; init; }
}
