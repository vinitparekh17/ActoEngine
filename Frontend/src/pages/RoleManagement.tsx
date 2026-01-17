import { useState } from "react";
import { useApi, useApiMutation, queryKeys, api } from "../hooks/useApi";
import { useConfirm } from "../hooks/useConfirm";
import { Button } from "../components/ui/button";
import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from "../components/ui/table";
import {
  LoadingContainer,
  TableSkeleton,
  PageHeaderSkeleton,
} from "../components/ui/skeletons";
import { Plus } from "lucide-react";
import type {
  Role,
  RoleWithPermissions,
  CreateRoleRequest,
  UpdateRoleRequest,
  PermissionGroupDto,
} from "../types/user-management";
import { RoleTableRow } from "./RoleManagement/RoleTableRow";
import { RoleFormModal } from "./RoleManagement/RoleFormModal";
import { RoleDetailModal } from "./RoleManagement/RoleDetailModal";

export default function RoleManagementPage() {
  const [editingRole, setEditingRole] = useState<Role | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [viewingRoleId, setViewingRoleId] = useState<number | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [editingRolePermissions, setEditingRolePermissions] = useState<number[]>([]);

  const { confirm } = useConfirm();

  // Fetch roles
  const {
    data: roles,
    isLoading,
    error,
  } = useApi<Role[]>("/Role", {
    queryKey: Array.from(queryKeys.roles.all()),
  });

  // Fetch permissions grouped by category
  const { data: permissionGroups } = useApi<PermissionGroupDto[]>(
    "/Permission/grouped",
    {
      queryKey: Array.from(queryKeys.permissions.grouped()),
    },
  );

  // Mutations
  const createMutation = useApiMutation<Role, CreateRoleRequest>(
    "/Role",
    "POST",
    {
      successMessage: "Role created successfully",
      invalidateKeys: [Array.from(queryKeys.roles.all())],
    },
  );

  const updateMutation = useApiMutation<void, UpdateRoleRequest & { roleId: number }>(
    "/Role/:roleId",
    "PUT",
    {
      successMessage: "Role updated successfully",
      invalidateKeys: [Array.from(queryKeys.roles.all())],
    },
  );

  const deleteMutation = useApiMutation<void, { roleId: number }>(
    "/Role/:roleId",
    "DELETE",
    {
      successMessage: "Role deleted successfully",
      invalidateKeys: [Array.from(queryKeys.roles.all())],
    },
  );

  // Fetch role detail
  const { data: roleDetail } = useApi<RoleWithPermissions>(
    viewingRoleId ? `/Role/${viewingRoleId}` : "",
    {
      queryKey: viewingRoleId
        ? Array.from(queryKeys.roles.detail(viewingRoleId))
        : [],
      enabled: !!viewingRoleId && isDetailModalOpen,
    },
  );

  // Handlers
  const handleCreate = (data: any) => {
    // validation handled in modal
    createMutation.mutate(
      {
        roleName: data.roleName,
        description: data.description,
        permissionIds: data.permissionIds,
      },
      {
        onSuccess: () => {
          setIsModalOpen(false);
        },
      }
    );
  };

  const handleUpdate = (data: any) => {
    if (!editingRole) return;

    const updateData = {
      roleId: editingRole.roleId, // Used for URL parameter only
      roleName: data.roleName,
      description: data.description,
      isActive: editingRole.isActive,
      permissionIds: data.permissionIds,
    };

    updateMutation.mutate(updateData, {
      onSuccess: () => {
        setIsModalOpen(false);
        setEditingRole(null);
        setEditingRolePermissions([]);
      },
    });
  };

  const handleDelete = async (role: Role) => {
    if (role.isSystem) {
      alert("Cannot delete system roles");
      return;
    }

    const confirmed = await confirm({
      title: "Delete Role",
      description: `Are you sure you want to delete "${role.roleName}"? This action cannot be undone.`,
      confirmText: "Delete",
      cancelText: "Cancel",
      variant: "destructive",
    });

    if (confirmed) {
      deleteMutation.mutate({ roleId: role.roleId });
    }
  };

  const openEditModal = async (role: Role) => {
    setIsEditing(true);
    setEditingRole(role);

    // Fetch role with permissions to pre-fill
    try {
      const response = await api.get<RoleWithPermissions>(`/Role/${role.roleId}`);
      const permissionIds = response?.permissions?.map((p) => p.permissionId) || [];
      setEditingRolePermissions(permissionIds);
    } catch (err) {
      console.error("Error loading role permissions:", err);
      setEditingRolePermissions([]);
    }

    setIsModalOpen(true);
  };

  const openDetailModal = (role: Role) => {
    setViewingRoleId(role.roleId);
    setIsDetailModalOpen(true);
  };

  const openCreateModal = () => {
    setIsEditing(false);
    setEditingRole(null);
    setEditingRolePermissions([]);
    setIsModalOpen(true);
  };

  const handleModalClose = (open: boolean) => {
    if (!open) {
      setIsModalOpen(false);
      setEditingRole(null);
      setIsEditing(false);
      setEditingRolePermissions([]);
    }
  };

  const handleDetailModalClose = (open: boolean) => {
    if (!open) {
      setIsDetailModalOpen(false);
      setViewingRoleId(null);
    }
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
              <h1 className="text-3xl font-bold text-foreground">
                Role Management
              </h1>
              <p className="text-muted-foreground">
                Manage roles and their permissions
              </p>
            </div>
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
                  <RoleTableRow
                    key={role.roleId}
                    role={role}
                    onView={openDetailModal}
                    onEdit={openEditModal}
                    onDelete={handleDelete}
                  />
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Unified Create/Edit Modal */}
          <RoleFormModal
            isOpen={isModalOpen}
            isEditing={isEditing}
            defaultValues={
              editingRole
                ? {
                  roleName: editingRole.roleName,
                  description: editingRole.description || "",
                  permissionIds: editingRolePermissions,
                }
                : undefined
            }
            permissionGroups={permissionGroups}
            isPending={isEditing ? updateMutation.isPending : createMutation.isPending}
            onClose={handleModalClose}
            onSubmit={isEditing ? handleUpdate : handleCreate}
          />

          {/* Role Detail Modal */}
          <RoleDetailModal
            isOpen={isDetailModalOpen}
            roleDetail={roleDetail}
            permissionGroups={permissionGroups}
            onClose={handleDetailModalClose}
            onEdit={openEditModal}
          />
        </div>
      )}
    </LoadingContainer>
  );
}
