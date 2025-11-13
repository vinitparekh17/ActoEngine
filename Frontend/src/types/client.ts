/**
 * Client domain types - centralized shared types for Client related models.
 */

export interface Client {
  clientId: number;
  clientName: string;
  projectId: number;
  email?: string;
  phone?: string;
  address?: string;
  isActive: boolean;
  createdAt: string;
  createdBy: number;
  updatedAt: string | null;
  updatedBy: number | null;
}

export interface CreateClientRequest {
  clientName: string;
  projectId: number;
  email?: string;
  phone?: string;
  address?: string;
}

export interface UpdateClientRequest {
  clientName: string;
  projectId?: number;
  email?: string;
  phone?: string;
  address?: string;
  isActive: boolean;
}
