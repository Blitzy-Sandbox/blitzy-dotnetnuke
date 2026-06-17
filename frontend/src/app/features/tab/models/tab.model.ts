/**
 * Tab / Page feature — client-side TypeScript model contracts.
 *
 * Pure type declarations (interfaces + a single numeric enum) for the Angular 19 SPA. `Tab`,
 * `CreateTab`, and `UpdateTab` mirror the backend `TabDto` / `CreateTabDto` / `UpdateTabDto` wire
 * shapes; `TabType` mirrors the verbatim backend `DnnMigration.Domain.Entities.TabType` enum.
 *
 * In DotNetNuke a "Tab" IS a site Page. The field set is derived from the legacy DotNetNuke VB.NET
 * source (`Library/Components/Tabs/TabInfo.vb`) read as reference, and the backend Tab DTOs (the wire
 * shape that this SPA actually consumes). Legacy XML serialization attributes
 * (`<XmlRoot>` / `<XmlElement>` / `<XmlIgnore>`) and the `IPropertyAccess` concern are intentionally
 * NOT ported.
 */

// MIGRATION: JSON CASING CONTRACT. The backend serializes with the DEFAULT System.Text.Json
// `JsonNamingPolicy.CamelCase` (no override; the DTOs carry no `[JsonPropertyName]`, and the API does
// NOT register `JsonStringEnumConverter`/`AddJsonOptions`). That policy lowercases the LEADING run of
// an acronym up to the next word boundary, then keeps the capital that starts the next word.
// Consequently the acronym-prefixed properties serialize as:
//   TabID    -> tabID     (leading run is just "T"; "ID" trails unchanged)
//   PortalID -> portalID
//   ParentId -> parentId  (C# property is `ParentId`, not `ParentID`)
// Every other property is ordinary camelCase (tabOrder, tabName, isVisible, level, iconFile, title,
// description, keyWords, url, skinSrc, containerSrc, tabPath, startDate, endDate, hasChildren,
// refreshInterval, isSecure, tabType). The interface field names below MUST MATCH this wire format
// EXACTLY — a mismatch yields silently `undefined` fields at runtime. (The sibling
// `features/role/models/role.model.ts` and `core/models/user.model.ts` follow the same rule.)
//
// C#->TS type mapping: value types -> `number` / `boolean`; `int?` -> `number | null`;
// `string?` -> `string | null`; `DateTime?` -> ISO-8601 `string | null`. Because the API does not
// register a string-enum converter, the `TabType` enum serializes as its NUMERIC ordinal, so `tabType`
// is a `number` on the wire and `TabType` below is a NUMERIC enum whose members match the backend
// ordinals verbatim.

/**
 * Tab classification. VERBATIM ordinals from the backend `DnnMigration.Domain.Entities.TabType`
 * (ported from `TabInfo.vb` L32-L38: File=0, Normal=1, Tab=2, Url=3, Member=4). Serialized by the API
 * as the numeric value (no `JsonStringEnumConverter` is registered), so the wire value is a `number`
 * and these members give it a readable name for display. This is the only runtime (value) export in
 * this module; the remaining exports are type-only.
 */
export enum TabType {
  File = 0,
  Normal = 1,
  Tab = 2,
  Url = 3,
  Member = 4,
}

/**
 * `Tab` mirrors the backend `TabDto` (full read projection; field order preserved from the DTO).
 * Computed/hierarchy/derived fields (`level`, `hasChildren`, `tabPath`, `tabType`) are read-only on
 * this model and are NOT part of the create/update contracts below.
 */
export interface Tab {
  tabID: number; // TabID (int) — PK
  tabOrder: number; // TabOrder (int) — ordering among siblings
  portalID: number; // PortalID (int) — owning portal (tenant scope)
  tabName: string | null; // TabName (string?)
  isVisible: boolean; // IsVisible (bool)
  parentId: number | null; // ParentId (int?) — null = root/top-level tab
  level: number; // Level (int) — depth in hierarchy (computed; read-only)
  iconFile: string | null; // IconFile (string?)
  title: string | null; // Title (string?) — browser title override
  description: string | null; // Description (string?) — meta description
  keyWords: string | null; // KeyWords (string?) — meta keywords
  url: string | null; // Url (string?) — external/redirect url
  skinSrc: string | null; // SkinSrc (string?)
  containerSrc: string | null; // ContainerSrc (string?)
  tabPath: string | null; // TabPath (string?) — materialized path (computed; read-only)
  startDate: string | null; // StartDate (DateTime?) — ISO-8601; null = unbounded
  endDate: string | null; // EndDate (DateTime?) — ISO-8601; null = unbounded
  hasChildren: boolean; // HasChildren (bool) — computed; read-only
  refreshInterval: number | null; // RefreshInterval (int?) — auto-refresh seconds; null = not set
  isSecure: boolean; // IsSecure (bool) — requires HTTPS
  tabType: TabType; // TabType (enum -> numeric ordinal on the wire); computed; read-only
}

// `CreateTab` mirrors the backend `CreateTabDto`: the client-writable creation subset. It excludes the
// server-generated identity (`tabID`) and the computed/hierarchy/derived fields
// (`level`, `hasChildren`, `tabPath`, `tabType`). POST /api/v1/tabs binds this shape.
export type CreateTab = Omit<Tab, 'tabID' | 'level' | 'hasChildren' | 'tabPath' | 'tabType'>;

// `UpdateTab` mirrors the backend `UpdateTabDto`: the client-writable mutable subset PLUS the `tabID`
// identity. PUT /api/v1/tabs/{id} requires `tabID` populated; the backend guards path id == body TabID.
// It excludes the computed/hierarchy/derived fields (`level`, `hasChildren`, `tabPath`, `tabType`).
export type UpdateTab = Omit<Tab, 'level' | 'hasChildren' | 'tabPath' | 'tabType'>;
