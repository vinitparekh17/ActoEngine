// Centralized, static fallback permissions mapping by role

export const rolePermissions: Record<string, string[]> = {
  admin: ['all'],
  user: ['read', 'write'],
  guest: ['read'],
};

export type Permission = string;
