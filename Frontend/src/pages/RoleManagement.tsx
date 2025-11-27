import { useState } from 'react';
import { useApi, useApiMutation, queryKeys } from '../hooks/useApi';
import { useConfirm } from '../hooks/useConfirm';
import { Button } from '../components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Textarea } from '../components/ui/textarea';
import { Checkbox } from '../components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { LoadingContainer, TableSkeleton, PageHeaderSkeleton } from '../components/ui/skeletons';
import { Pencil, Trash2, Plus, Shield } from 'lucide-react';
import type { Role, RoleWithPermissions, CreateRoleRequest, UpdateRoleRequest, PermissionGroupDto } from '../types/user-management';

export default function RoleManagementPage() {
    const [editingRole, setEditingRole] = useState<Role | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [formData, setFormData] = useState<CreateRoleRequest>({
        roleName: '',
        description: '',
        permissionIds: [],
    });
    const { confirm } = useConfirm();

    // Fetch roles
    const { data: roles, isLoading, error } = useApi<Role[]>('/Role', {
        queryKey: Array.from(queryKeys.roles.all())
    });

    // Fetch permissions grouped by category
    const { data: permissionGroups } = useApi<PermissionGroupDto[]>('/Permission/grouped', {
        queryKey: Array.from(queryKeys.permissions.grouped())
    });

    // Mutations
    const createMutation = useApiMutation<Role, CreateRoleRequest>(
        '/Role',
        'POST',
        {
            successMessage: 'Role created successfully',
            invalidateKeys: [Array.from(queryKeys.roles.all())],
        }
    );

    const updateMutation = useApiMutation<void, UpdateRoleRequest>(
        '/Role/:roleId',
        'PUT',
        {
            successMessage: 'Role updated successfully',
            invalidateKeys: [Array.from(queryKeys.roles.all())],
        }
    );

    const deleteMutation = useApiMutation<void, { roleId: number }>(
        '/Role/:roleId',
        'DELETE',
        {
            successMessage: 'Role deleted successfully',
            invalidateKeys: [Array.from(queryKeys.roles.all())],
        }
    );

    // Handler for permission checkbox toggle
    const handlePermissionToggle = (permissionId: number, checked: boolean) => {
        if (checked) {
            setFormData({
                ...formData,
                permissionIds: [...formData.permissionIds, permissionId]
            });
        } else {
            setFormData({
                ...formData,
                permissionIds: formData.permissionIds.filter(id => id !== permissionId)
            });
        }
    };

    // Handler for "Select All" in a category
    const handleCategorySelectAll = (categoryPermissions: number[], checked: boolean) => {
        if (checked) {
            // Add all permissions from this category
            const newPermissions = new Set([...formData.permissionIds, ...categoryPermissions]);
            setFormData({
                ...formData,
                permissionIds: Array.from(newPermissions)
            });
        } else {
            // Remove all permissions from this category
            setFormData({
                ...formData,
                permissionIds: formData.permissionIds.filter(id => !categoryPermissions.includes(id))
            });
        }
    };

    // Handlers
    const handleCreate = () => {
        if (!formData.roleName.trim()) {
            alert('Role name is required');
            return;
        }

        createMutation.mutate(formData, {
            onSuccess: () => {
                setIsModalOpen(false);
                setFormData({ roleName: '', description: '', permissionIds: [] });
            },
        });
    };

    const handleUpdate = () => {
        if (!editingRole) return;

        const updateData: UpdateRoleRequest = {
            roleId: editingRole.roleId,
            roleName: formData.roleName,
            description: formData.description,
            isActive: editingRole.isActive,
            permissionIds: formData.permissionIds,
        };

        updateMutation.mutate(updateData, {
            onSuccess: () => {
                setIsModalOpen(false);
                setEditingRole(null);
                setFormData({ roleName: '', description: '', permissionIds: [] });
            },
        });
    };

    const handleDelete = async (role: Role) => {
        if (role.isSystem) {
            alert('Cannot delete system roles');
            return;
        }

        const confirmed = await confirm({
            title: 'Delete Role',
            description: `Are you sure you want to delete "${role.roleName}"? This action cannot be undone.`,
            confirmText: 'Delete',
            cancelText: 'Cancel',
            variant: 'destructive',
        });

        if (confirmed) {
            deleteMutation.mutate({ roleId: role.roleId });
        }
    };

    const openEditModal = async (role: Role) => {
        setIsEditing(true);
        setEditingRole(role);

        // Fetch role with permissions
        try {
            const roleWithPermissions = await useApi<RoleWithPermissions>(`/Role/${role.roleId}`, {
                queryKey: Array.from(queryKeys.roles.detail(role.roleId))
            }).refetch?.();

            const permissionIds = roleWithPermissions?.data?.permissions.map(p => p.permissionId) || [];

            setFormData({
                roleName: role.roleName,
                description: role.description || '',
                permissionIds: permissionIds,
            });
        } catch (err) {
            console.error('Error loading role permissions:', err);
            setFormData({
                roleName: role.roleName,
                description: role.description || '',
                permissionIds: [],
            });
        }

        setIsModalOpen(true);
    };

    const openCreateModal = () => {
        setIsEditing(false);
        setEditingRole(null);
        setFormData({ roleName: '', description: '', permissionIds: [] });
        setIsModalOpen(true);
    };

    const handleModalClose = (open: boolean) => {
        if (!open) {
            setIsModalOpen(false);
            setEditingRole(null);
            setIsEditing(false);
            setFormData({ roleName: '', description: '', permissionIds: [] });
        }
    };

    // Calculate permission count for each role
    const getPermissionCount = (role: Role) => {
        // This would ideally come from the API, but for now we'll show a placeholder
        return '-';
    };

    return (
        <LoadingContainer
            isLoading={isLoading}
            skeleton={
                <div className="space-y-6">
                    <PageHeaderSkeleton />
                    <TableSkeleton rows={4} columns={6} />
                </div>
            }
        >
            {error ? (
                <div className="text-center text-red-600">
                    Error loading roles: {error.message}
                </div>
            ) : (
                <div className="space-y-6">
                    {/* Header */}
                    <div className="flex justify-between items-center">
                        <div>
                            <h1 className="text-3xl font-bold text-foreground">Role Management</h1>
                            <p className="text-muted-foreground">Manage roles and their permissions</p>
                        </div>

                        {/* Create Role Button */}
                        <Button onClick={openCreateModal}>
                            <Plus className="h-4 w-4 mr-2" />
                            Add Role
                        </Button>
                    </div>

                    {/* Roles Table */}
                    <div className="rounded-2xl border bg-background dark:bg-dark-500 shadow-sm overflow-hidden">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Role ID</TableHead>
                                    <TableHead>Role Name</TableHead>
                                    <TableHead>Description</TableHead>
                                    <TableHead>Permissions</TableHead>
                                    <TableHead>Status</TableHead>
                                    <TableHead>Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {roles?.map((role) => (
                                    <TableRow key={role.roleId}>
                                        <TableCell>{role.roleId}</TableCell>
                                        <TableCell>
                                            <div className="flex items-center gap-2">
                                                {role.roleName}
                                                {role.isSystem && (
                                                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200">
                                                        <Shield className="h-3 w-3 mr-1" />
                                                        System
                                                    </span>
                                                )}
                                            </div>
                                        </TableCell>
                                        <TableCell>{role.description || '-'}</TableCell>
                                        <TableCell>{getPermissionCount(role)}</TableCell>
                                        <TableCell>
                                            <span className={`px-2 py-1 rounded-full text-xs ${
                                                role.isActive
                                                    ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
                                                    : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200'
                                            }`}>
                                                {role.isActive ? 'Active' : 'Inactive'}
                                            </span>
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex gap-2">
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => openEditModal(role)}
                                                    disabled={role.isSystem}
                                                >
                                                    <Pencil className="h-4 w-4" />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => handleDelete(role)}
                                                    className="text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                                                    disabled={role.isSystem}
                                                >
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </div>

                    {/* Unified Create/Edit Modal */}
                    <Dialog open={isModalOpen} onOpenChange={handleModalClose}>
                        <DialogContent className="sm:max-w-[600px] max-h-[80vh] overflow-y-auto">
                            <DialogHeader>
                                <DialogTitle>{isEditing ? 'Edit Role' : 'Create New Role'}</DialogTitle>
                                <DialogDescription>
                                    {isEditing
                                        ? 'Update the role details and permissions below.'
                                        : 'Enter the role details and select permissions.'
                                    }
                                </DialogDescription>
                            </DialogHeader>

                            <div className="grid gap-4 py-4">
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="roleName" className="text-right">
                                        Role Name
                                    </Label>
                                    <Input
                                        id="roleName"
                                        value={formData.roleName}
                                        onChange={(e) => setFormData({ ...formData, roleName: e.target.value })}
                                        placeholder="Enter role name"
                                        className="col-span-3"
                                    />
                                </div>

                                <div className="grid grid-cols-4 items-start gap-4">
                                    <Label htmlFor="description" className="text-right pt-2">
                                        Description
                                    </Label>
                                    <Textarea
                                        id="description"
                                        value={formData.description}
                                        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                                        placeholder="Enter role description"
                                        className="col-span-3"
                                        rows={3}
                                    />
                                </div>

                                {/* Permission Selector */}
                                <div className="space-y-4">
                                    <Label className="text-sm font-medium">Permissions</Label>
                                    <div className="border rounded-lg p-4 max-h-64 overflow-y-auto space-y-4">
                                        {permissionGroups?.map((group) => {
                                            const groupPermissionIds = group.permissions.map(p => p.permissionId);
                                            const allSelected = groupPermissionIds.every(id => formData.permissionIds.includes(id));
                                            const someSelected = groupPermissionIds.some(id => formData.permissionIds.includes(id));

                                            return (
                                                <div key={group.category} className="space-y-2">
                                                    <div className="flex items-center space-x-2 font-medium">
                                                        <Checkbox
                                                            id={`cat-${group.category}`}
                                                            checked={allSelected}
                                                            onCheckedChange={(checked) =>
                                                                handleCategorySelectAll(groupPermissionIds, checked as boolean)
                                                            }
                                                        />
                                                        <Label htmlFor={`cat-${group.category}`} className="font-semibold">
                                                            {group.category} {someSelected && !allSelected && '(partial)'}
                                                        </Label>
                                                    </div>
                                                    <div className="ml-6 space-y-1">
                                                        {group.permissions.map((permission) => (
                                                            <div key={permission.permissionId} className="flex items-center space-x-2">
                                                                <Checkbox
                                                                    id={`perm-${permission.permissionId}`}
                                                                    checked={formData.permissionIds.includes(permission.permissionId)}
                                                                    onCheckedChange={(checked) =>
                                                                        handlePermissionToggle(permission.permissionId, checked as boolean)
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
                                        Selected: {formData.permissionIds.length} permission(s)
                                    </p>
                                </div>
                            </div>

                            <DialogFooter>
                                <Button
                                    onClick={isEditing ? handleUpdate : handleCreate}
                                    disabled={isEditing ? updateMutation.isPending : createMutation.isPending}
                                >
                                    {isEditing
                                        ? (updateMutation.isPending ? 'Updating...' : 'Update Role')
                                        : (createMutation.isPending ? 'Creating...' : 'Create Role')
                                    }
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                </div>
            )}
        </LoadingContainer>
    );
}
