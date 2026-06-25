import type { User } from './user.model';
import type { Role } from './role.model';

// MIGRATION: Permission.vb (DotNetNuke.Security.Permissions.PermissionInfo) -> Permission (base type).
export interface Permission {
  permissionId: number;
  permissionCode?: string | null;
  moduleDefId: number;
  permissionKey?: string | null;
  permissionName?: string | null;
}

// MIGRATION: ModulePermission.vb (Inherits PermissionInfo) -> ModulePermission extends Permission.
export interface ModulePermission extends Permission {
  modulePermissionId: number;
  moduleId?: number | null;
  roleId: number;
  roleName?: string | null;
  allowAccess: boolean;
  userId?: number | null;
  username?: string | null;
  displayName?: string | null;
}

// MIGRATION: TabPermission.vb (Inherits PermissionInfo) -> TabPermission extends Permission.
export interface TabPermission extends Permission {
  tabPermissionId: number;
  tabId?: number | null;
  roleId: number;
  roleName?: string | null;
  allowAccess: boolean;
  userId?: number | null;
  username?: string | null;
  displayName?: string | null;
}

// MIGRATION: FolderPermission.vb (Inherits PermissionInfo) -> FolderPermission extends Permission.
export interface FolderPermission extends Permission {
  folderPermissionId: number;
  folderId?: number | null;
  portalId?: number | null;
  folderPath?: string | null;
  roleId: number;
  roleName?: string | null;
  allowAccess: boolean;
  userId?: number | null;
  username?: string | null;
  displayName?: string | null;
}

// MIGRATION: UserRoleInfo.vb -> UserRole (clean join; legacy FullName/Email dropped). Does NOT extend Role.
export interface UserRole {
  userRoleId: number;
  userId: number;
  roleId: number;
  effectiveDate?: string | null;
  expiryDate?: string | null;
  isTrialUsed: boolean;
  subscribed: boolean;
  // Optional navigation properties (present only when the API expands them).
  user?: User;
  role?: Role;
}
