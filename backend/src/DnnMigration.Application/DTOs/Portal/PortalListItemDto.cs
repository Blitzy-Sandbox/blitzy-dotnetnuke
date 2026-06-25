namespace DnnMigration.Application.DTOs.Portal;

// MIGRATION: Lightweight list/grid projection for the collection endpoint GET /api/portals; the service wraps a
// sequence of these in PagedResult<PortalListItemDto>. Column set derived from the legacy host portals grid
// (Website/admin/Portal/Portals.ascx.vb) to avoid over-fetching (AAP 0.7.7). Plain shape only — no logic, no entity import.
public record PortalListItemDto
{
    public int PortalId { get; init; }

    public string PortalName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Currency { get; init; }

    public DateTime? ExpiryDate { get; init; }

    // MIGRATION: Service-populated count (legacy PortalInfo.Users lazy getter dropped on the entity).
    public int? Users { get; init; }

    // MIGRATION: Service-populated count (legacy PortalInfo.Pages lazy getter dropped on the entity).
    public int? Pages { get; init; }

    public float HostFee { get; init; }
}
