// User types
export interface UserDto {
  userId: number;
  username: string;
  fullName?: string;
  isActive: boolean;
  roleName?: string;
  roleId?: number;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  fullName?: string;
  roleId: number;
}

export interface UpdateUserRequest {
  userId: number;
  fullName?: string;
  roleId: number;
  isActive: boolean;
}

// Role types
export interface Role {
  roleId: number;
  roleName: string;
  description?: string;
  isSystem: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface Permission {
  permissionId: number;
  permissionKey: string;
  resource: string;
  action: string;
  description?: string;
  category?: string;
  isActive: boolean;
  createdAt: string;
}

export interface PermissionGroupDto {
  category: string;
  permissions: Permission[];
}

export interface RoleWithPermissions {
  role: Role;
  permissions: Permission[];
}

export interface CreateRoleRequest {
  roleName: string;
  description?: string;
  permissionIds: number[];
}

export interface UpdateRoleRequest {
  roleName: string;
  description?: string;
  isActive: boolean;
  permissionIds: number[];
}

export interface UserDetailResponse {
  user: UserDto;
  permissions: string[];
}
