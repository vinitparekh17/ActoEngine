import { useState } from "react";
import { useApi, useApiMutation, queryKeys } from "../hooks/useApi";
import { useConfirm } from "../hooks/useConfirm";
import { Button } from "../components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../components/ui/table";
import { Input } from "../components/ui/input";
import { Label } from "../components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "../components/ui/dialog";
import {
  LoadingContainer,
  TableSkeleton,
  PageHeaderSkeleton,
} from "../components/ui/skeletons";
import { Pencil, Trash2, Plus } from "lucide-react";
import type {
  UserDto,
  CreateUserRequest,
  UpdateUserRequest,
  Role,
} from "../types/user-management";

export default function UserManagementPage() {
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [formData, setFormData] = useState<CreateUserRequest>({
    username: "",
    password: "",
    fullName: "",
    roleId: 0,
  });
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

  // Mutations
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

  // Handlers
  const handleCreate = () => {
    if (!formData.username.trim()) {
      alert("Username is required");
      return;
    }
    if (!formData.password || formData.password.length < 8) {
      alert("Password must be at least 8 characters");
      return;
    }
    if (!formData.roleId) {
      alert("Please select a role");
      return;
    }

    createMutation.mutate(formData, {
      onSuccess: () => {
        setIsModalOpen(false);
        setFormData({ username: "", password: "", fullName: "", roleId: 0 });
      },
    });
  };

  const handleUpdate = () => {
    if (!editingUser || !editingUser.userId) return;
    const updateData: UpdateUserRequest = {
      userId: editingUser.userId,
      fullName: formData.fullName,
      roleId: formData.roleId,
      isActive: editingUser.isActive,
    };

    updateMutation.mutate(updateData, {
      onSuccess: () => {
        setIsModalOpen(false);
        setEditingUser(null);
        setFormData({ username: "", password: "", fullName: "", roleId: 0 });
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
    setFormData({
      username: user.username,
      password: "", // Password not shown in edit
      fullName: user.fullName || "",
      roleId: user.roleId || 0,
    });
    setIsModalOpen(true);
  };

  const openCreateModal = () => {
    setIsEditing(false);
    setEditingUser(null);
    setFormData({ username: "", password: "", fullName: "", roleId: 0 });
    setIsModalOpen(true);
  };

  const handleModalClose = (open: boolean) => {
    if (!open) {
      setIsModalOpen(false);
      setEditingUser(null);
      setIsEditing(false);
      setFormData({ username: "", password: "", fullName: "", roleId: 0 });
    }
  };

  const users = usersResponse?.users || [];

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
                  <TableRow key={user.userId}>
                    <TableCell>{user.userId}</TableCell>
                    <TableCell>{user.username}</TableCell>
                    <TableCell>{user.fullName || "-"}</TableCell>
                    <TableCell>{user.roleName || "-"}</TableCell>
                    <TableCell>
                      <span
                        className={`px-2 py-1 rounded-full text-xs ${
                          user.isActive
                            ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                            : "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
                        }`}
                      >
                        {user.isActive ? "Active" : "Inactive"}
                      </span>
                    </TableCell>
                    <TableCell>
                      {new Date(user.createdAt).toLocaleDateString()}
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => openEditModal(user)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDelete(user)}
                          className="text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
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
            <DialogContent className="sm:max-w-[425px]">
              <DialogHeader>
                <DialogTitle>
                  {isEditing ? "Edit User" : "Create New User"}
                </DialogTitle>
                <DialogDescription>
                  {isEditing
                    ? 'Update the user details below and click "Update User" to save changes.'
                    : 'Enter the details below and click "Create User" to add a new user.'}
                </DialogDescription>
              </DialogHeader>

              <div className="grid gap-4 py-4">
                <div className="grid grid-cols-4 items-center gap-4">
                  <Label htmlFor="username" className="text-right">
                    Username
                  </Label>
                  <Input
                    id="username"
                    value={formData.username}
                    onChange={(e) =>
                      setFormData({ ...formData, username: e.target.value })
                    }
                    placeholder="Enter username"
                    className="col-span-3"
                    disabled={isEditing}
                  />
                </div>

                {!isEditing && (
                  <div className="grid grid-cols-4 items-center gap-4">
                    <Label htmlFor="password" className="text-right">
                      Password
                    </Label>
                    <Input
                      id="password"
                      type="password"
                      value={formData.password}
                      onChange={(e) =>
                        setFormData({ ...formData, password: e.target.value })
                      }
                      placeholder="Min 8 characters"
                      className="col-span-3"
                    />
                  </div>
                )}

                <div className="grid grid-cols-4 items-center gap-4">
                  <Label htmlFor="fullName" className="text-right">
                    Full Name
                  </Label>
                  <Input
                    id="fullName"
                    value={formData.fullName}
                    onChange={(e) =>
                      setFormData({ ...formData, fullName: e.target.value })
                    }
                    placeholder="Enter full name"
                    className="col-span-3"
                  />
                </div>

                <div className="grid grid-cols-4 items-center gap-4">
                  <Label htmlFor="role" className="text-right">
                    Role
                  </Label>
                  <Select
                    value={formData.roleId.toString()}
                    onValueChange={(value) =>
                      setFormData({ ...formData, roleId: parseInt(value) })
                    }
                  >
                    <SelectTrigger className="col-span-3">
                      <SelectValue placeholder="Select a role" />
                    </SelectTrigger>
                    <SelectContent>
                      {roles?.map((role) => (
                        <SelectItem
                          key={role.roleId}
                          value={role.roleId.toString()}
                        >
                          {role.roleName}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <DialogFooter>
                <Button
                  onClick={isEditing ? handleUpdate : handleCreate}
                  disabled={
                    isEditing
                      ? updateMutation.isPending
                      : createMutation.isPending
                  }
                >
                  {isEditing
                    ? updateMutation.isPending
                      ? "Updating..."
                      : "Update User"
                    : createMutation.isPending
                      ? "Creating..."
                      : "Create User"}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
      )}
    </LoadingContainer>
  );
}
