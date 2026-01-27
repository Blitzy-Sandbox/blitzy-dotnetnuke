/**
 * Module Feature TypeScript Model Definitions
 * 
 * MIGRATION: VB.NET ModuleInfo.vb, ModuleDefinitionInfo.vb, DesktopModuleInfo.vb entities 
 * converted to TypeScript interfaces for Angular 19 frontend.
 * 
 * @description Contains Module interface (maps to backend ModuleDto, derived from ModuleInfo.vb),
 * ModuleDefinition interface (from ModuleDefinitionInfo.vb), DesktopModule interface 
 * (from DesktopModuleInfo.vb), and related request/response interfaces.
 * 
 * @source Library/Components/Modules/ModuleInfo.vb
 * @source Library/Components/Modules/ModuleDefinitionInfo.vb
 * @source Library/Components/Modules/DesktopModuleInfo.vb
 * @source Website/admin/Modules/ModuleSettings.ascx.vb
 * @source Website/admin/Modules/Export.ascx.vb
 * @source Website/admin/Modules/Import.ascx.vb
 */

/**
 * Module visibility state enumeration
 * 
 * MIGRATION: Direct port from VB.NET Enum VisibilityState (ModuleInfo.vb lines 30-34)
 * Controls how a module is displayed on the page.
 * 
 * @example
 * const state: VisibilityState = VisibilityState.Maximized;
 */
export enum VisibilityState {
  /**
   * Module is fully visible and expanded
   */
  Maximized = 0,

  /**
   * Module is collapsed/minimized showing only header
   */
  Minimized = 1,

  /**
   * Module is hidden from view
   */
  None = 2
}

/**
 * Desktop module supported features flags enumeration
 * 
 * MIGRATION: Direct port from VB.NET Enum DesktopModuleSupportedFeature 
 * (DesktopModuleInfo.vb lines 30-34)
 * 
 * Bit flags enum - values can be combined using bitwise OR for multiple features.
 * 
 * @example
 * // Module that supports both portable and searchable features
 * const features = SupportedFeatures.IsPortable | SupportedFeatures.IsSearchable;
 * 
 * // Check if a feature is supported
 * const isPortable = (features & SupportedFeatures.IsPortable) === SupportedFeatures.IsPortable;
 */
export enum SupportedFeatures {
  /**
   * No special features supported
   */
  None = 0,

  /**
   * Module can export/import content (implements IPortable interface)
   */
  IsPortable = 1,

  /**
   * Module content is searchable (implements ISearchable interface)
   */
  IsSearchable = 2,

  /**
   * Module supports upgrade scripts (implements IUpgradeable interface)
   */
  IsUpgradeable = 4
}

/**
 * Module interface representing a module instance on a page
 * 
 * MIGRATION: Converted from VB.NET ModuleInfo class (ModuleInfo.vb lines 36-637)
 * Maps to backend ModuleDto. Contains all properties for a module instance
 * including display settings, permissions, and container configuration.
 * 
 * Key property mappings:
 * - VB.NET Integer → TypeScript number
 * - VB.NET String → TypeScript string
 * - VB.NET Boolean → TypeScript boolean
 * - VB.NET Date → TypeScript string (ISO format) or Date
 * - VB.NET Enum VisibilityState → TypeScript VisibilityState enum
 * - camelCase naming convention applied (TypeScript standard)
 * - Optional modifiers (?) for nullable fields
 */
export interface Module {
  /**
   * Primary key of the module instance
   * MIGRATION: From _ModuleID (line 46)
   */
  moduleId: number;

  /**
   * Tab/page where this module instance is displayed
   * MIGRATION: From _TabID (line 44)
   */
  tabId: number;

  /**
   * Unique identifier for this module on this specific tab
   * MIGRATION: From _TabModuleID (line 45)
   */
  tabModuleId: number;

  /**
   * Module definition ID that this module is based on
   * MIGRATION: From _ModuleDefID (line 47)
   */
  moduleDefId: number;

  /**
   * Portal/site that this module belongs to
   * MIGRATION: From _PortalID (line 43)
   */
  portalId: number;

  /**
   * Display order of the module within its pane
   * MIGRATION: From _ModuleOrder (line 48)
   */
  moduleOrder: number;

  /**
   * Name of the content pane where module is placed (e.g., 'ContentPane', 'LeftPane', 'RightPane')
   * MIGRATION: From _PaneName (line 49)
   */
  paneName: string;

  /**
   * Display title of the module shown in the header
   * MIGRATION: From _ModuleTitle (line 50)
   */
  moduleTitle: string;

  /**
   * Output cache duration in seconds
   * MIGRATION: From _CacheTime (line 52)
   */
  cacheTime: number;

  /**
   * Horizontal alignment of the module ('Left', 'Center', 'Right')
   * MIGRATION: From _Alignment (line 54)
   */
  alignment?: string;

  /**
   * Background color of the module
   * MIGRATION: From _Color (line 55)
   */
  color?: string;

  /**
   * Border style of the module
   * MIGRATION: From _Border (line 56)
   */
  border?: string;

  /**
   * Path to the module's icon file
   * MIGRATION: From _IconFile (line 57)
   */
  iconFile?: string;

  /**
   * Whether the module appears on all tabs/pages
   * MIGRATION: From _AllTabs (line 58)
   */
  allTabs: boolean;

  /**
   * Current visibility state of the module
   * MIGRATION: From _Visibility (line 59)
   */
  visibility: VisibilityState;

  /**
   * Whether the module is marked as deleted (soft delete)
   * MIGRATION: From _IsDeleted (line 61)
   */
  isDeleted: boolean;

  /**
   * HTML content to display above the module content
   * MIGRATION: From _Header (line 62)
   */
  header?: string;

  /**
   * HTML content to display below the module content
   * MIGRATION: From _Footer (line 63)
   */
  footer?: string;

  /**
   * Date when the module becomes visible (ISO format string)
   * MIGRATION: From _StartDate (line 64)
   */
  startDate?: string;

  /**
   * Date when the module is no longer visible (ISO format string)
   * MIGRATION: From _EndDate (line 65)
   */
  endDate?: string;

  /**
   * Path to the container skin file
   * MIGRATION: From _ContainerSrc (line 66)
   */
  containerSrc?: string;

  /**
   * Whether to display the module title in the header
   * MIGRATION: From _DisplayTitle (line 67)
   */
  displayTitle: boolean;

  /**
   * Whether to display the print icon
   * MIGRATION: From _DisplayPrint (line 68)
   */
  displayPrint: boolean;

  /**
   * Whether to display the RSS/syndication icon
   * MIGRATION: From _DisplaySyndicate (line 69)
   */
  displaySyndicate: boolean;

  /**
   * Whether view permissions are inherited from the parent tab
   * MIGRATION: From _InheritViewPermissions (line 70)
   */
  inheritViewPermissions: boolean;

  /**
   * ID of the desktop module definition
   * MIGRATION: From _DesktopModuleID (line 72)
   */
  desktopModuleId: number;

  /**
   * Folder name containing the module files
   * MIGRATION: From _FolderName (line 73)
   */
  folderName?: string;

  /**
   * User-friendly display name of the module
   * MIGRATION: From _FriendlyName (line 74)
   */
  friendlyName?: string;

  /**
   * Description of the module's purpose
   * MIGRATION: From _Description (line 75)
   */
  description?: string;

  /**
   * Version number of the module
   * MIGRATION: From _Version (line 76)
   */
  version?: string;

  /**
   * Whether the module requires a premium license
   * MIGRATION: From _IsPremium (line 77)
   */
  isPremium: boolean;

  /**
   * Whether this is an admin-only module
   * MIGRATION: From _IsAdmin (line 78)
   */
  isAdmin: boolean;

  /**
   * Fully qualified class name of the business controller
   * MIGRATION: From _BusinessControllerClass (line 79)
   */
  businessControllerClass?: string;

  /**
   * Internal module name/identifier
   * MIGRATION: From _ModuleName (line 80)
   */
  moduleName?: string;

  /**
   * Bit flags indicating supported features (IPortable, ISearchable, IUpgradeable)
   * MIGRATION: From _SupportedFeatures (line 81)
   */
  supportedFeatures: number;

  /**
   * Compatible DNN versions for this module
   * MIGRATION: From _CompatibleVersions (line 82)
   */
  compatibleVersions?: string;

  /**
   * Dependencies required by this module
   * MIGRATION: From _Dependencies (line 83)
   */
  dependencies?: string;

  /**
   * Permission settings string
   * MIGRATION: From _Permissions (line 84)
   */
  permissions?: string;

  /**
   * Default cache time for the module definition
   * MIGRATION: From _DefaultCacheTime (line 85)
   */
  defaultCacheTime?: number;

  /**
   * ID of the module control being rendered
   * MIGRATION: From _ModuleControlId (line 86)
   */
  moduleControlId?: number;

  /**
   * Source path of the module control
   * MIGRATION: From _ControlSrc (line 87)
   */
  controlSrc?: string;

  /**
   * Title of the module control
   * MIGRATION: From _ControlTitle (line 89)
   */
  controlTitle?: string;

  /**
   * URL to the module's help documentation
   * MIGRATION: From _HelpUrl (line 90)
   */
  helpUrl?: string;

  /**
   * Whether the module supports partial/AJAX rendering
   * MIGRATION: From _SupportsPartialRendering (line 91)
   */
  supportsPartialRendering?: boolean;

  /**
   * Semi-colon separated list of roles authorized to edit
   * MIGRATION: From _AuthorizedEditRoles (line 51)
   */
  authorizedEditRoles?: string;

  /**
   * Semi-colon separated list of roles authorized to view
   * MIGRATION: From _AuthorizedViewRoles (line 53)
   */
  authorizedViewRoles?: string;

  /**
   * Path to the container directory
   * MIGRATION: From _ContainerPath (line 92)
   */
  containerPath?: string;

  /**
   * Index position of this module within its pane
   * MIGRATION: From _PaneModuleIndex (line 93)
   */
  paneModuleIndex?: number;

  /**
   * Total count of modules in the same pane
   * MIGRATION: From _PaneModuleCount (line 94)
   */
  paneModuleCount?: number;

  /**
   * Whether this is the default module for the tab
   * MIGRATION: From _IsDefaultModule (line 95)
   */
  isDefaultModule?: boolean;

  /**
   * Whether this applies to all modules
   * MIGRATION: From _AllModules (line 96)
   */
  allModules?: boolean;

  /**
   * Legacy authorized roles string (deprecated)
   * MIGRATION: From _AuthorizedRoles (line 60)
   */
  authorizedRoles?: string;

  /**
   * Computed property: Whether the module supports import/export
   * MIGRATION: From IsPortable read-only property (lines 608-612)
   * Computed from (supportedFeatures & SupportedFeatures.IsPortable) === SupportedFeatures.IsPortable
   */
  readonly isPortable?: boolean;

  /**
   * Computed property: Whether the module content is searchable
   * MIGRATION: From IsSearchable read-only property (lines 614-618)
   * Computed from (supportedFeatures & SupportedFeatures.IsSearchable) === SupportedFeatures.IsSearchable
   */
  readonly isSearchable?: boolean;

  /**
   * Computed property: Whether the module supports upgrade scripts
   * MIGRATION: From IsUpgradeable read-only property (lines 620-624)
   * Computed from (supportedFeatures & SupportedFeatures.IsUpgradeable) === SupportedFeatures.IsUpgradeable
   */
  readonly isUpgradeable?: boolean;
}

/**
 * Module definition interface representing a module type/template
 * 
 * MIGRATION: Converted from VB.NET ModuleDefinitionInfo class 
 * (ModuleDefinitionInfo.vb lines 30-84)
 * Represents a specific definition/variant of a desktop module.
 */
export interface ModuleDefinition {
  /**
   * Primary key of the module definition
   * MIGRATION: From _ModuleDefID (line 32)
   */
  moduleDefId: number;

  /**
   * User-friendly display name
   * MIGRATION: From _FriendlyName (line 33)
   */
  friendlyName: string;

  /**
   * Parent desktop module ID
   * MIGRATION: From _DesktopModuleID (line 34)
   */
  desktopModuleId: number;

  /**
   * Temporary module ID used during installation
   * MIGRATION: From _TempModuleID (line 35)
   */
  tempModuleId?: number;

  /**
   * Default cache time in seconds for modules of this type
   * MIGRATION: From _DefaultCacheTime (line 36)
   */
  defaultCacheTime: number;
}

/**
 * Desktop module interface representing a module package/type
 * 
 * MIGRATION: Converted from VB.NET DesktopModuleInfo class 
 * (DesktopModuleInfo.vb lines 36-260)
 * Represents the top-level module package that contains module definitions.
 */
export interface DesktopModule {
  /**
   * Primary key of the desktop module
   * MIGRATION: From _DesktopModuleID (line 40)
   */
  desktopModuleId: number;

  /**
   * Internal module name/identifier
   * MIGRATION: From _ModuleName (line 41)
   */
  moduleName: string;

  /**
   * Folder name containing the module files
   * MIGRATION: From _FolderName (line 42)
   */
  folderName: string;

  /**
   * User-friendly display name
   * MIGRATION: From _FriendlyName (line 43)
   */
  friendlyName: string;

  /**
   * Description of the module's purpose
   * MIGRATION: From _Description (line 44)
   */
  description?: string;

  /**
   * Version number of the module
   * MIGRATION: From _Version (line 45)
   */
  version?: string;

  /**
   * Whether the module requires a premium license
   * MIGRATION: From _IsPremium (line 46)
   */
  isPremium: boolean;

  /**
   * Whether this is an admin-only module
   * MIGRATION: From _IsAdmin (line 47)
   */
  isAdmin: boolean;

  /**
   * Fully qualified class name of the business controller
   * MIGRATION: From _BusinessControllerClass (line 49)
   */
  businessControllerClass?: string;

  /**
   * Bit flags indicating supported features
   * MIGRATION: From _SupportedFeatures (line 48)
   */
  supportedFeatures: number;

  /**
   * Compatible DNN versions for this module
   * MIGRATION: From _CompatibleVersions (line 50)
   */
  compatibleVersions?: string;

  /**
   * Dependencies required by this module
   * MIGRATION: From _Dependencies (line 51)
   */
  dependencies?: string;

  /**
   * Permission settings string
   * MIGRATION: From _Permissions (line 52)
   */
  permissions?: string;

  /**
   * Computed property: Whether the module supports import/export
   * MIGRATION: From IsPortable property (lines 164-171)
   */
  readonly isPortable?: boolean;

  /**
   * Computed property: Whether the module content is searchable
   * MIGRATION: From IsSearchable property (lines 173-180)
   */
  readonly isSearchable?: boolean;

  /**
   * Computed property: Whether the module supports upgrade scripts
   * MIGRATION: From IsUpgradeable property (lines 155-162)
   */
  readonly isUpgradeable?: boolean;
}

/**
 * Request interface for creating a new module instance
 * 
 * Used with POST /api/modules endpoint.
 */
export interface CreateModuleRequest {
  /**
   * Module definition ID that this module should be based on
   * Required - specifies which type of module to create
   */
  moduleDefId: number;

  /**
   * Tab/page ID where the module should be added
   * Required - specifies which page to add module to
   */
  tabId: number;

  /**
   * Name of the content pane where module should be placed
   * Required - e.g., 'ContentPane', 'LeftPane', 'RightPane'
   */
  paneName: string;

  /**
   * Display title of the module shown in the header
   * Required - the user-visible title
   */
  moduleTitle: string;

  /**
   * Position/order within the pane
   * Optional - defaults to bottom of pane
   */
  position?: number;

  /**
   * Horizontal alignment of the module
   * Optional - 'Left', 'Center', or 'Right'
   */
  alignment?: string;

  /**
   * Whether the module should appear on all tabs/pages
   * Optional - defaults to false
   */
  allTabs?: boolean;

  /**
   * Initial visibility state of the module
   * Optional - defaults to Maximized
   */
  visibility?: VisibilityState;
}

/**
 * Request interface for updating an existing module instance
 * 
 * MIGRATION: Derived from ModuleSettings.ascx.vb BindData method (lines 85-166)
 * Form field mappings from the original ASPX control bindings.
 * 
 * Used with PUT /api/modules/{id} endpoint.
 * All fields are optional - only provided fields will be updated.
 */
export interface UpdateModuleRequest {
  /**
   * Display title of the module
   * MIGRATION: From txtTitle.Text (line 126)
   */
  moduleTitle?: string;

  /**
   * Path to the module's icon file
   * MIGRATION: From ctlIcon.Url (line 127)
   */
  iconFile?: string;

  /**
   * Move module to a different tab
   * MIGRATION: From cboTab selection (line 129)
   */
  tabId?: number;

  /**
   * Whether the module appears on all tabs
   * MIGRATION: From chkAllTabs.Checked (line 133)
   */
  allTabs?: boolean;

  /**
   * Visibility state of the module
   * MIGRATION: From cboVisibility.SelectedIndex (line 134)
   */
  visibility?: VisibilityState;

  /**
   * Output cache duration in seconds
   * MIGRATION: From txtCacheTime.Text (line 141)
   */
  cacheTime?: number;

  /**
   * Horizontal alignment of the module
   * MIGRATION: From cboAlign selection (line 144)
   */
  alignment?: string;

  /**
   * Background color of the module
   * MIGRATION: From txtColor.Text (line 146)
   */
  color?: string;

  /**
   * Border style of the module
   * MIGRATION: From txtBorder.Text (line 147)
   */
  border?: string;

  /**
   * HTML content to display above the module content
   * MIGRATION: From txtHeader.Text (line 149)
   */
  header?: string;

  /**
   * HTML content to display below the module content
   * MIGRATION: From txtFooter.Text (line 150)
   */
  footer?: string;

  /**
   * Date when the module becomes visible (ISO format string)
   * MIGRATION: From txtStartDate.Text (line 153)
   */
  startDate?: string;

  /**
   * Date when the module is no longer visible (ISO format string)
   * MIGRATION: From txtEndDate.Text (line 156)
   */
  endDate?: string;

  /**
   * Path to the container skin file
   * MIGRATION: From ctlModuleContainer.SkinSrc (line 161)
   */
  containerSrc?: string;

  /**
   * Whether to display the module title in the header
   * MIGRATION: From chkDisplayTitle.Checked (line 163)
   */
  displayTitle?: boolean;

  /**
   * Whether to display the print icon
   * MIGRATION: From chkDisplayPrint.Checked (line 164)
   */
  displayPrint?: boolean;

  /**
   * Whether to display the RSS/syndication icon
   * MIGRATION: From chkDisplaySyndicate.Checked (line 165)
   */
  displaySyndicate?: boolean;

  /**
   * Whether view permissions are inherited from the parent tab
   * MIGRATION: From chkInheritPermissions.Checked (line 122)
   */
  inheritViewPermissions?: boolean;
}

/**
 * Request interface for updating module-specific custom settings
 * 
 * Module-specific settings are stored as key-value pairs
 * separate from the core module properties.
 */
export interface ModuleSettingsRequest {
  /**
   * ID of the module to update settings for
   * Required
   */
  moduleId: number;

  /**
   * Key-value pairs of custom settings
   * Keys and values are both strings
   */
  settings: Record<string, string>;
}

/**
 * Request interface for exporting module content
 * 
 * MIGRATION: Derived from Export.ascx.vb cmdExport_Click handler (lines 119-137)
 * Used to export module content to an XML file.
 */
export interface ExportModuleRequest {
  /**
   * ID of the module to export
   * MIGRATION: From ModuleId parameter
   */
  moduleId: number;

  /**
   * Destination folder path for the export file
   * MIGRATION: From cboFolders.SelectedItem.Value (line 125)
   */
  folder: string;

  /**
   * Name of the export file (without extension)
   * MIGRATION: From txtFile.Text (line 124)
   */
  fileName: string;
}

/**
 * Request interface for importing module content
 * 
 * MIGRATION: Derived from Import.ascx.vb cmdImport_Click handler (lines 143-150)
 * Used to import module content from an XML file.
 */
export interface ImportModuleRequest {
  /**
   * ID of the module to import content into
   * MIGRATION: From ModuleId parameter
   */
  moduleId: number;

  /**
   * Source folder path containing the import file
   * MIGRATION: From cboFolders.SelectedItem.Value (line 149)
   */
  folder: string;

  /**
   * Name of the file to import
   * MIGRATION: From cboFiles.SelectedItem.Value (line 149)
   */
  fileName: string;
}

/**
 * Folder interface for file/folder selection dropdowns
 * 
 * Used in export/import functionality for folder selection.
 */
export interface Folder {
  /**
   * Unique identifier of the folder
   */
  folderId: number;

  /**
   * Relative path of the folder
   */
  folderPath: string;

  /**
   * Display name for the folder in dropdowns
   */
  displayName: string;
}

/**
 * Import file interface for file selection dropdowns
 * 
 * Represents a file available for import in the selected folder.
 */
export interface ImportFile {
  /**
   * Actual file name on disk
   */
  fileName: string;

  /**
   * Cleaned display name for dropdowns (without content prefix)
   */
  displayName: string;
}

/**
 * Response interface for module export operation
 */
export interface ExportModuleResponse {
  /**
   * Whether the export was successful
   */
  success: boolean;

  /**
   * Path to the exported file if successful
   */
  filePath?: string;

  /**
   * Status or error message
   */
  message?: string;
}

/**
 * Request interface for moving a module to a different tab
 */
export interface MoveModuleRequest {
  /**
   * Source tab ID where the module currently exists
   */
  fromTabId: number;

  /**
   * Destination tab ID where the module should be moved
   */
  toTabId: number;

  /**
   * Target pane name on the destination tab
   */
  paneName: string;
}

/**
 * Request interface for copying a module to another tab
 */
export interface CopyModuleRequest {
  /**
   * Source tab ID where the module exists
   */
  fromTabId: number;

  /**
   * Destination tab ID where the module should be copied
   */
  toTabId: number;

  /**
   * Whether to include module-specific settings in the copy
   */
  includeSettings: boolean;
}

/**
 * Request interface for adding an existing module to a tab
 * 
 * Used when a module with AllTabs=false is added to additional pages.
 */
export interface AddModuleToTabRequest {
  /**
   * ID of the module to add
   */
  moduleId: number;

  /**
   * Target pane name on the tab
   */
  paneName: string;
}

/**
 * Request interface for updating module order within a pane
 */
export interface UpdateModuleOrderRequest {
  /**
   * New order/position value for the module
   */
  order: number;

  /**
   * Pane name where the module is located
   */
  paneName: string;
}

/**
 * Tab interface for tab/page selection in module forms
 * 
 * Simplified tab representation for dropdown selections.
 */
export interface Tab {
  /**
   * Unique identifier of the tab
   */
  tabId: number;

  /**
   * Display name of the tab/page
   */
  tabName: string;

  /**
   * Parent tab ID for hierarchical display (null for root tabs)
   */
  parentId: number | null;

  /**
   * Nesting level in the tab hierarchy (0 = root)
   */
  level: number;

  /**
   * Whether the tab is visible in navigation
   */
  isVisible: boolean;

  /**
   * Whether the tab link is disabled
   */
  disableLink: boolean;
}

/**
 * Container interface for container/skin selection
 * 
 * Represents a module container skin option.
 */
export interface Container {
  /**
   * Source path of the container file
   */
  containerSrc: string;

  /**
   * Display name of the container
   */
  containerName: string;
}

/**
 * Module permission interface for managing module-level permissions
 * 
 * MIGRATION: Based on ModulePermissionInfo from 
 * Library/Components/Security/Permissions/ModulePermissionInfo.vb
 */
export interface ModulePermission {
  /**
   * Primary key of the module permission record
   */
  modulePermissionId: number;

  /**
   * ID of the module this permission applies to
   */
  moduleId: number;

  /**
   * ID of the permission definition
   */
  permissionId: number;

  /**
   * Permission key (e.g., 'VIEW', 'EDIT')
   */
  permissionKey: string;

  /**
   * Human-readable permission name
   */
  permissionName: string;

  /**
   * Role ID this permission is granted to (null if user-specific)
   */
  roleId: number | null;

  /**
   * Role name for display purposes
   */
  roleName?: string;

  /**
   * Whether this is a grant (true) or deny (false) permission
   */
  allowAccess: boolean;

  /**
   * User ID this permission is granted to (null if role-based)
   */
  userId: number | null;

  /**
   * Username for display purposes
   */
  username?: string;

  /**
   * User display name for UI purposes
   */
  displayName?: string;
}

/**
 * Helper function to check if a module supports a specific feature
 * 
 * @param supportedFeatures - The supportedFeatures bit flags from the module
 * @param feature - The feature to check for
 * @returns true if the feature is supported
 * 
 * @example
 * const isPortable = hasFeature(module.supportedFeatures, SupportedFeatures.IsPortable);
 */
export function hasFeature(supportedFeatures: number, feature: SupportedFeatures): boolean {
  return (supportedFeatures & feature) === feature;
}

/**
 * Helper function to combine multiple features into a single bit flags value
 * 
 * @param features - Array of features to combine
 * @returns Combined bit flags value
 * 
 * @example
 * const features = combineFeatures([SupportedFeatures.IsPortable, SupportedFeatures.IsSearchable]);
 */
export function combineFeatures(features: SupportedFeatures[]): number {
  return features.reduce((acc, feature) => acc | feature, 0);
}

/**
 * Helper function to create a Module object with computed properties
 * 
 * @param moduleData - Raw module data from API
 * @returns Module with computed isPortable, isSearchable, isUpgradeable properties
 */
export function createModuleWithComputedProperties(moduleData: Omit<Module, 'isPortable' | 'isSearchable' | 'isUpgradeable'>): Module {
  return {
    ...moduleData,
    get isPortable(): boolean {
      return hasFeature(moduleData.supportedFeatures, SupportedFeatures.IsPortable);
    },
    get isSearchable(): boolean {
      return hasFeature(moduleData.supportedFeatures, SupportedFeatures.IsSearchable);
    },
    get isUpgradeable(): boolean {
      return hasFeature(moduleData.supportedFeatures, SupportedFeatures.IsUpgradeable);
    }
  };
}

/**
 * Helper function to create a DesktopModule object with computed properties
 * 
 * @param moduleData - Raw desktop module data from API
 * @returns DesktopModule with computed isPortable, isSearchable, isUpgradeable properties
 */
export function createDesktopModuleWithComputedProperties(
  moduleData: Omit<DesktopModule, 'isPortable' | 'isSearchable' | 'isUpgradeable'>
): DesktopModule {
  return {
    ...moduleData,
    get isPortable(): boolean {
      return hasFeature(moduleData.supportedFeatures, SupportedFeatures.IsPortable);
    },
    get isSearchable(): boolean {
      return hasFeature(moduleData.supportedFeatures, SupportedFeatures.IsSearchable);
    },
    get isUpgradeable(): boolean {
      return hasFeature(moduleData.supportedFeatures, SupportedFeatures.IsUpgradeable);
    }
  };
}
