import { Label } from "../../components/ui/label";
import { Checkbox } from "../../components/ui/checkbox";
import type { PermissionGroupDto } from "../../types/user-management";
import { areAllPermissionsSelected, areSomePermissionsSelected } from "./helpers";

interface PermissionSelectorProps {
    permissionGroups: PermissionGroupDto[] | undefined;
    selectedPermissionIds: number[];
    onPermissionToggle: (permissionId: number, checked: boolean) => void;
    onCategorySelectAll: (categoryPermissions: number[], checked: boolean) => void;
}

export function PermissionSelector({
    permissionGroups,
    selectedPermissionIds,
    onPermissionToggle,
    onCategorySelectAll,
}: Readonly<PermissionSelectorProps>) {
    if (!permissionGroups) return null;

    return (
        <div className="space-y-4">
            <Label className="text-sm font-medium">Permissions</Label>
            <div className="border rounded-lg p-4 max-h-64 overflow-y-auto space-y-4">
                {permissionGroups.map((group) => {
                    const groupPermissionIds = group.permissions.map((p) => p.permissionId);
                    const allSelected = areAllPermissionsSelected(groupPermissionIds, selectedPermissionIds);
                    const someSelected = areSomePermissionsSelected(groupPermissionIds, selectedPermissionIds);

                    return (
                        <div key={group.category} className="space-y-2">
                            <div className="flex items-center space-x-2 font-medium">
                                <Checkbox
                                    id={`cat-${group.category}`}
                                    checked={allSelected}
                                    onCheckedChange={(checked) =>
                                        onCategorySelectAll(groupPermissionIds, checked as boolean)
                                    }
                                />
                                <Label htmlFor={`cat-${group.category}`} className="font-semibold">
                                    {group.category} {someSelected && !allSelected && "(partial)"}
                                </Label>
                            </div>
                            <div className="ml-6 space-y-1">
                                {group.permissions.map((permission) => (
                                    <div key={permission.permissionId} className="flex items-center space-x-2">
                                        <Checkbox
                                            id={`perm-${permission.permissionId}`}
                                            checked={selectedPermissionIds.includes(permission.permissionId)}
                                            onCheckedChange={(checked) =>
                                                onPermissionToggle(permission.permissionId, checked as boolean)
                                            }
                                        />
                                        <Label
                                            htmlFor={`perm-${permission.permissionId}`}
                                            className="text-sm font-normal cursor-pointer"
                                        >
                                            <span className="font-medium">{permission.permissionKey}</span>
                                            {permission.description && (
                                                <span className="text-muted-foreground"> - {permission.description}</span>
                                            )}
                                        </Label>
                                    </div>
                                ))}
                            </div>
                        </div>
                    );
                })}
            </div>
            <p className="text-sm text-muted-foreground">
                Selected: {selectedPermissionIds.length} permission(s)
            </p>
        </div>
    );
}
