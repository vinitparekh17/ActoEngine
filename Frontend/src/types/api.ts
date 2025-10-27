/**
 * API Types - Single Source of Truth
 * 
 * This file contains all TypeScript type definitions that correspond to the backend API models.
 * These types should match the C# models defined in WebApi/Models/.
 * 
 * Benefits of centralizing types:
 * 1. Single source of truth - all API types defined in one place
 * 2. Easy maintenance - update types in one location
 * 3. Consistency - prevents duplicate/conflicting type definitions
 * 4. Type safety - ensures frontend matches backend contracts
 * 
 * Guidelines:
 * - Keep types synchronized with backend models (C# classes in WebApi/Models/)
 * - Use optional properties (?) for nullable backend properties
 * - Use Date for date/time properties that will be parsed
 * - Use string for date/time properties that remain as strings
 * - Document complex types with JSDoc comments
 * 
 * Related Backend Files:
 * - WebApi/Models/Project.cs
 * - WebApi/Models/Client.cs
 * - WebApi/Models/Common.cs
 */

// ============================================
// Common API Response Types
// ============================================

export interface ApiResponse<T> {
  status: boolean;
  data: T | null;
  message: string | null;
  errors: string[];
  timestamp: string;
}

export interface MessageResponse {
  message: string;
}

export interface ErrorResponse {
  error: string;
  message: string;
  timestamp: string;
  path: string;
}

// ============================================
// Project Types
// ============================================

export interface Project {
  projectId: number;
  projectName: string;
  description?: string;
  databaseName?: string;
  connectionString?: string;
  databaseType?: string;
  serverName?: string;
  isActive?: boolean;
  syncStatus?: string;
  syncProgress?: number;
  lastSyncAttempt?: Date;
  createdAt?: Date;
  updatedAt?: Date;
  createdBy?: number;
  updatedBy?: number;
}

export interface VerifyConnectionRequest {
  server: string;
  databaseName: string;
  username: string;
  password: string;
  port: number;
  databaseType: string;
}

export interface ConnectionResponse {
  isValid: boolean;
  message: string;
  serverVersion?: string;
  testedAt?: string;
  errors?: string[];
}

export interface CreateProjectRequest {
  projectName: string;
  description: string;
  databaseName: string;
  connectionString: string;
  databaseType: string;
}

export interface LinkProjectRequest {
  projectId: number;
  projectName: string;
  description: string;
  databaseName: string;
  connectionString: string;
  databaseType: string;
}

export interface ProjectResponse {
  projectId: number;
  message: string;
  syncJobId: number;
}

export interface UpdateProjectRequest {
  projectName: string;
  description: string;
  isActive: boolean;
  databaseName?: string;
  connectionString?: string;
}

export interface UpdateProjectResponse {
  success: boolean;
  message: string;
  data: Project;
}

export interface DeleteProjectResponse {
  success: boolean;
  message: string;
}

export interface SyncProjectResponse {
  success: boolean;
  message: string;
}

export interface SyncStatusResponse {
  projectId: number;
  status: string;
  syncProgress: number;
  lastSyncAttempt?: Date;
}

export interface ProjectStatsResponse {
  tableCount: number;
  spCount: number;
  lastSync?: Date;
}

export interface ActivityItem {
  type: string;
  description: string;
  timestamp: Date;
  user: string;
}

// ============================================
// Client Types
// ============================================

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
