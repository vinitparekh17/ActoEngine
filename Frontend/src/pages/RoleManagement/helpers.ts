import type { PermissionGroupDto } from "../../types/user-management";

/**
 * Group permissions that belong to a role by their category
 */
export function filterPermissionGroupsByRole(
  permissionGroups: PermissionGroupDto[] | undefined,
  rolePermissionIds: number[],
): PermissionGroupDto[] {
  if (!permissionGroups) return [];

  return permissionGroups
    .filter((group) =>
      group.permissions.some((p) => rolePermissionIds.includes(p.permissionId)),
    )
    .map((group) => ({
      ...group,
      permissions: group.permissions.filter((p) =>
        rolePermissionIds.includes(p.permissionId),
      ),
    }));
}

/**
 * Check if all permissions in a category are selected
 */
export function areAllPermissionsSelected(
  permissionIds: number[],
  selectedIds: number[],
): boolean {
  return permissionIds.every((id) => selectedIds.includes(id));
}

/**
 * Check if some (but not all) permissions in a category are selected
 */
export function areSomePermissionsSelected(
  permissionIds: number[],
  selectedIds: number[],
): boolean {
  const allSelected = areAllPermissionsSelected(permissionIds, selectedIds);
  const someSelected = permissionIds.some((id) => selectedIds.includes(id));
  return someSelected && !allSelected;
}
