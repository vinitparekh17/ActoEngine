import { useState } from "react";
import { useApi, useApiMutation, queryKeys } from "../hooks/useApi";
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
  UserDto,
  CreateUserRequest,
  UpdateUserRequest,
  Role,
  UserDetailResponse,
} from "../types/user-management";
import { UserTableRow } from "./UserManagement/UserTableRow";
import { UserFormModal } from "./UserManagement/UserFormModal";
import { UserDetailModal } from "./UserManagement/UserDetailModal";
import { PasswordChangeModal } from "./UserManagement/PasswordChangeModal";



export default function UserManagementPage() {
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [viewingUser, setViewingUser] = useState<UserDto | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);

  const { confirm } = useConfirm();

  // Fetch users with pagination
  const {
    data: usersResponse,
    isLoading,
    error,
  } = useApi<{ users: UserDto[]; totalCount: number }>(
    "/UserManagement?page=1&pageSize=100",
    { queryKey: Array.from(queryKeys.users.all()) },
  );

  // Fetch roles for dropdown
  const { data: roles } = useApi<Role[]>("/Role", {
    queryKey: Array.from(queryKeys.roles.all()),
  });

  // Mutations (useApiMutation calls remain same...)
  const createMutation = useApiMutation<UserDto, CreateUserRequest>(
    "/UserManagement",
    "POST",
    {
      successMessage: "User created successfully",
      invalidateKeys: [Array.from(queryKeys.users.all())],
    },
  );

  const updateMutation = useApiMutation<void, UpdateUserRequest>(
    "/UserManagement/:userId",
    "PUT",
    {
      successMessage: "User updated successfully",
      invalidateKeys: [Array.from(queryKeys.users.all())],
    },
  );

  const deleteMutation = useApiMutation<void, { userId: number }>(
    "/UserManagement/:userId",
    "DELETE",
    {
      successMessage: "User deleted successfully",
      invalidateKeys: [Array.from(queryKeys.users.all())],
    },
  );

  // Fetch user detail
  const { data: userDetail } = useApi<UserDetailResponse>(
    viewingUser ? `/UserManagement/${viewingUser.userId}` : "",
    {
      queryKey: viewingUser
        ? Array.from(queryKeys.users.detail(viewingUser.userId))
        : [],
      enabled: !!viewingUser && isDetailModalOpen,
    },
  );

  // Password change mutation
  const changePasswordMutation = useApiMutation<
    void,
    { userId: number; newPassword: string }
  >("/UserManagement/:userId/change-password", "POST", {
    successMessage: "Password changed successfully",
    invalidateKeys: [Array.from(queryKeys.users.all())],
  });

  // Handlers
  const handleCreate = (data: any) => {
    // validation is now handled by Zod in the modal
    createMutation.mutate(
      {
        username: data.username,
        password: data.password,
        fullName: data.fullName,
        roleId: data.roleId,
      },
      {
        onSuccess: () => {
          setIsModalOpen(false);
        },
      }
    );
  };

  const handleUpdate = (data: any) => {
    if (!editingUser?.userId) return;
    const updateData: UpdateUserRequest = {
      userId: editingUser.userId,
      fullName: data.fullName,
      roleId: data.roleId,
      isActive: editingUser.isActive,
    };

    updateMutation.mutate(updateData, {
      onSuccess: () => {
        setIsModalOpen(false);
        setEditingUser(null);
      },
    });
  };

  const handleDelete = async (user: UserDto) => {
    const confirmed = await confirm({
      title: "Delete User",
      description: `Are you sure you want to delete "${user.username}"? This action cannot be undone.`,
      confirmText: "Delete",
      cancelText: "Cancel",
      variant: "destructive",
    });

    if (confirmed) {
      deleteMutation.mutate({ userId: user.userId });
    }
  };

  const openEditModal = (user: UserDto) => {
    setIsEditing(true);
    setEditingUser(user);
    setIsModalOpen(true);
  };

  const openCreateModal = () => {
    setIsEditing(false);
    setEditingUser(null);
    setIsModalOpen(true);
  };

  const openDetailModal = (user: UserDto) => {
    setViewingUser(user);
    setIsDetailModalOpen(true);
  };

  const openPasswordModal = (user: UserDto) => {
    setViewingUser(user);
    setIsPasswordModalOpen(true);
  };

  const handlePasswordChange = (data: any) => {
    if (!viewingUser) return;

    changePasswordMutation.mutate(
      { userId: viewingUser.userId, newPassword: data.newPassword },
      {
        onSuccess: () => {
          setIsPasswordModalOpen(false);
          setViewingUser(null);
        },
      },
    );
  };

  const handleModalClose = (open: boolean) => {
    if (!open) {
      setIsModalOpen(false);
      setEditingUser(null);
      setIsEditing(false);
    }
  };

  const handleDetailModalClose = (open: boolean) => {
    setIsDetailModalOpen(open);
    if (!open) {
      setViewingUser(null);
    }
  };

  const handlePasswordModalClose = (open: boolean) => {
    setIsPasswordModalOpen(open);
    if (!open) {
      setViewingUser(null);
    }
  };

  const handlePasswordModalChangePassword = (user: UserDto) => {
    setIsDetailModalOpen(false);
    openPasswordModal(user);
  };

  const users = usersResponse?.users || [];
  const isPending = isEditing ? updateMutation.isPending : createMutation.isPending;

  return (
    <LoadingContainer
      isLoading={isLoading}
      skeleton={
        <div className="space-y-6">
          <PageHeaderSkeleton />
          <TableSkeleton rows={5} columns={7} />
        </div>
      }
    >
      {error ? (
        <div className="text-center text-red-600">
          Error loading users: {error.message}
        </div>
      ) : (
        <div className="space-y-6">
          {/* Header */}
          <div className="flex justify-between items-center">
            <div>
              <h1 className="text-3xl font-bold text-foreground">
                User Management
              </h1>
              <p className="text-muted-foreground">
                Manage system users and their roles
              </p>
            </div>

            {/* Create User Button */}
            <Button onClick={openCreateModal}>
              <Plus className="h-4 w-4 mr-2" />
              Add User
            </Button>
          </div>

          {/* Users Table */}
          <div className="rounded-2xl border bg-background dark:bg-dark-500 shadow-sm overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User ID</TableHead>
                  <TableHead>Username</TableHead>
                  <TableHead>Full Name</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created At</TableHead>
                  <TableHead>Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.map((user) => (
                  <UserTableRow
                    key={user.userId}
                    user={user}
                    onEdit={openEditModal}
                    onDelete={handleDelete}
                    onViewDetail={openDetailModal}
                    onChangePassword={openPasswordModal}
                  />
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Unified Create/Edit Modal */}
          <UserFormModal
            isOpen={isModalOpen}
            isEditing={isEditing}
            defaultValues={
              editingUser
                ? {
                  username: editingUser.username,
                  fullName: editingUser.fullName || "",
                  roleId: editingUser.roleId || 0,
                }
                : undefined
            }
            roles={roles}
            isPending={isPending}
            onClose={handleModalClose}
            onSubmit={isEditing ? handleUpdate : handleCreate}
          />

          {/* User Detail Modal */}
          <UserDetailModal
            isOpen={isDetailModalOpen}
            userDetail={userDetail}
            onClose={handleDetailModalClose}
            onChangePassword={handlePasswordModalChangePassword}
          />

          {/* Password Change Modal */}
          <PasswordChangeModal
            isOpen={isPasswordModalOpen}
            user={viewingUser}
            isPending={changePasswordMutation.isPending}
            onClose={handlePasswordModalClose}
            onSubmit={handlePasswordChange}
          />
        </div>
      )}
    </LoadingContainer>
  );
}
