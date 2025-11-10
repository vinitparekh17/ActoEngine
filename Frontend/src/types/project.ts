/**
 * Project domain types - centralized shared types for Project related models.
 * This file collects project-specific API types and smaller shared types
 * used across components/pages (ProjectOption, ProjectFormData, ProjectUser, etc.).
 */

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

// Small shared UI / local types
export type ProjectOption = { id: string; name: string };

export interface ProjectFormData {
  projectName: string;
  description: string;
  databaseName: string;
  isActive: boolean;
}

export interface ProjectUser {
  userId: number;
  fullName?: string;
  username: string;
  email: string;
  role?: string;
}
