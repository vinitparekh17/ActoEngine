import { Button } from "../../components/ui/button";
import { Label } from "../../components/ui/label";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "../../components/ui/dialog";
import { Pencil, Shield } from "lucide-react";
import type {
    Role,
    RoleWithPermissions,
    PermissionGroupDto,
} from "../../types/user-management";
import { filterPermissionGroupsByRole } from "./helpers";
import { format } from "date-fns";

interface RoleDetailModalProps {
    isOpen: boolean;
    roleDetail: RoleWithPermissions | undefined;
    permissionGroups: PermissionGroupDto[] | undefined;
    onClose: (open: boolean) => void;
    onEdit: (role: Role) => void;
}

export function RoleDetailModal({
    isOpen,
    roleDetail,
    permissionGroups,
    onClose,
    onEdit,
}: Readonly<RoleDetailModalProps>) {
    const filteredGroups = roleDetail
        ? filterPermissionGroupsByRole(
            permissionGroups ?? [],
            roleDetail.permissions?.map((p) => p.permissionId) ?? []
        )
        : [];

    const handleEdit = () => {
        if (roleDetail) {
            onClose(false);
            onEdit(roleDetail.role);
        }
    };

    const safeFormatDate = (dateString: string | Date) => {
        try {
            const date = new Date(dateString);
            if (Number.isNaN(date.getTime())) return "-";
            return format(date, "PPpp");
        } catch {
            return "-";
        }
    };

    return (
        <Dialog open={isOpen} onOpenChange={onClose}>
            <DialogContent className="sm:max-w-[600px] max-h-[80vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>Role Details</DialogTitle>
                    <DialogDescription>
                        Detailed information about {roleDetail?.role?.roleName || "this role"}
                    </DialogDescription>
                </DialogHeader>

                {roleDetail && (
                    <div className="grid gap-4 py-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label className="text-muted-foreground">Role ID</Label>
                                <p className="font-medium">{roleDetail.role.roleId}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Role Name</Label>
                                <div className="flex items-center gap-2">
                                    <p className="font-medium">{roleDetail.role.roleName}</p>
                                    {roleDetail.role.isSystem && (
                                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200">
                                            <Shield className="h-3 w-3 mr-1" />
                                            System
                                        </span>
                                    )}
                                </div>
                            </div>
                            <div className="col-span-2">
                                <Label className="text-muted-foreground">Description</Label>
                                <p className="font-medium">
                                    {roleDetail.role.description || "-"}
                                </p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Status</Label>
                                <span
                                    className={`px-2 py-1 rounded-full text-xs inline-block ${roleDetail.role.isActive
                                        ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                                        : "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
                                        }`}
                                >
                                    {roleDetail.role.isActive ? "Active" : "Inactive"}
                                </span>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Created At</Label>
                                <p className="font-medium">
                                    {safeFormatDate(roleDetail.role.createdAt)}
                                </p>
                            </div>
                        </div>

                        {roleDetail.permissions && roleDetail.permissions.length > 0 && (
                            <div>
                                <Label className="text-muted-foreground mb-2 block">
                                    Permissions ({roleDetail.permissions.length})
                                </Label>
                                <div className="border rounded-lg p-4 max-h-64 overflow-y-auto space-y-2">
                                    {filteredGroups.map((group) => (
                                        <div key={group.category} className="space-y-1">
                                            <p className="font-semibold text-sm">{group.category}</p>
                                            <div className="ml-4 space-y-1">
                                                {group.permissions.map((permission) => (
                                                    <div
                                                        key={permission.permissionId}
                                                        className="text-sm p-2 bg-muted rounded"
                                                    >
                                                        <span className="font-medium">
                                                            {permission.permissionKey}
                                                        </span>
                                                        {permission.description && (
                                                            <span className="text-muted-foreground ml-2">
                                                                - {permission.description}
                                                            </span>
                                                        )}
                                                    </div>
                                                ))}
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>
                )}

                <DialogFooter>
                    <Button
                        variant="outline"
                        onClick={() => onClose(false)}
                    >
                        Close
                    </Button>
                    {roleDetail && !roleDetail.role.isSystem && (
                        <Button onClick={handleEdit}>
                            <Pencil className="h-4 w-4 mr-2" />
                            Edit Role
                        </Button>
                    )}
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
