// types/context.ts

/**
 * TypeScript type definitions for Context Service and Database Metadata
 */

// Database Metadata DTOs

// Lightweight list DTOs (used by list endpoints for minimal bandwidth)
export interface TableMetadataDto {
  tableId: number;
  tableName: string;
  schemaName?: string;
}

export interface StoredProcedureMetadataDto {
  spId: number;
  procedureName: string;
  schemaName?: string;
}

// Full detail DTOs (used by detail endpoints)
export interface TableDetailDto {
  tableId: number;
  tableName: string;
  schemaName?: string;
  description?: string;
  rowCount?: number;
  createdDate?: string;
  modifiedDate?: string;
}

export interface StoredProcedureDetailDto {
  spId: number;
  procedureName: string;
  schemaName?: string;
  description?: string;
  definition?: string;
  createdDate?: string;
  modifiedDate?: string;
}

export interface ColumnMetadataDto {
  columnId: number;
  columnName: string;
  dataType: string;
  tableName?: string;
  schemaName?: string;
  isNullable?: boolean;
  isPrimaryKey?: boolean;
  isForeignKey?: boolean;
  description?: string;
}

// Context Service Types

export type EntityType = 'TABLE' | 'COLUMN' | 'SP' | 'FUNCTION' | 'VIEW';

export type ExpertiseLevel = 'OWNER' | 'EXPERT' | 'FAMILIAR' | 'CONTRIBUTOR';

export type SensitivityLevel = 'PUBLIC' | 'INTERNAL' | 'PII' | 'FINANCIAL' | 'SENSITIVE';

export type FrequencyLevel = 'REALTIME' | 'HOURLY' | 'DAILY' | 'BATCH' | 'ADHOC';

export type BusinessDomain = 
  | 'ORDERS' 
  | 'FINANCE' 
  | 'USERS' 
  | 'INVENTORY' 
  | 'REPORTING' 
  | 'INTEGRATION' 
  | 'GENERAL';

export type CriticalityLevel = 1 | 2 | 3 | 4 | 5;

export interface EntityContext {
  contextId: number;
  projectId: number;
  entityType: EntityType;
  entityId: number;
  entityName: string;
  
  // Core context fields
  purpose?: string;
  businessImpact?: string;
  dataOwner?: string;
  criticalityLevel: CriticalityLevel;
  businessDomain?: BusinessDomain;
  
  // Column-specific
  sensitivity?: SensitivityLevel;
  dataSource?: string;
  validationRules?: string; // JSON format
  
  // Table-specific
  retentionPolicy?: string;
  
  // SP-specific
  dataFlow?: string;
  frequency?: FrequencyLevel;
  isDeprecated: boolean;
  deprecationReason?: string;
  replacedBy?: string;
  
  // Metadata
  isContextStale: boolean;
  lastReviewedAt?: string;
  reviewedBy?: number;
  lastContextUpdate?: string;
  contextUpdatedBy?: number;
  createdAt: string;
}

export interface EntityExpert {
  expertId: number;
  projectId: number;
  entityType: EntityType;
  entityId: number;
  userId: number;
  expertiseLevel: ExpertiseLevel;
  notes?: string;
  addedAt: string;
  addedBy?: number;
  
  // Navigation
  user?: User;
}

export interface User {
  userId: number;
  username: string;
  fullName?: string;
  email: string;
}

export interface ContextHistory {
  historyId: number;
  entityType: EntityType;
  entityId: number;
  fieldName: string;
  oldValue?: string;
  newValue?: string;
  changedBy: number;
  changedAt: string;
  changeReason?: string;
}

export interface ContextReviewRequest {
  requestId: number;
  entityType: EntityType;
  entityId: number;
  requestedBy: number;
  assignedTo?: number;
  status: 'PENDING' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED';
  reason?: string;
  createdAt: string;
  completedAt?: string;
}

// Request/Response DTOs

export interface SaveContextRequest {
  purpose?: string;
  businessImpact?: string;
  dataOwner?: string;
  criticalityLevel?: CriticalityLevel;
  businessDomain?: BusinessDomain;
  sensitivity?: SensitivityLevel;
  dataSource?: string;
  validationRules?: string;
  retentionPolicy?: string;
  dataFlow?: string;
  frequency?: FrequencyLevel;
  isDeprecated?: boolean;
  deprecationReason?: string;
  replacedBy?: string;
  expertUserIds?: number[];
}

export interface ContextResponse {
  context: EntityContext;
  experts: EntityExpert[];
  suggestions?: ContextSuggestions;
  completenessScore: number;
  isStale: boolean;
  dependencyCount: number;
}

export interface ContextSuggestions {
  purpose?: string;
  businessDomain?: BusinessDomain;
  sensitivity?: SensitivityLevel;
  validationRules?: string;
  potentialOwners: UserSuggestion[];
  potentialExperts: UserSuggestion[];
}

export interface UserSuggestion {
  userId: number;
  name: string;
  email: string;
  reason?: string;
}

export interface AddExpertRequest {
  userId: number;
  expertiseLevel: ExpertiseLevel;
  notes?: string;
}

export interface BulkContextEntry {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  context: SaveContextRequest;
}

export interface BulkImportResult {
  entityName: string;
  success: boolean;
  error?: string;
}

export interface ContextCoverageStats {
  entityType: string;
  total: number;
  documented: number;
  coveragePercentage: number;
  avgCompleteness?: number;
}

export interface DashboardData {
  coverage: ContextCoverageStats[];
  staleCount: number;
  staleEntities: StaleEntity[];
  topDocumented: DocumentedEntity[];
  criticalUndocumented: UndocumentedEntity[];
  lastUpdated: string;
}

export interface StaleEntity {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  lastContextUpdate: string;
  lastReviewedAt?: string;
  daysSinceUpdate: number;
}

export interface DocumentedEntity {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  purpose?: string;
  businessDomain?: string;
  dataOwner?: string;
  criticalityLevel: CriticalityLevel;
  completenessScore: number;
  expertCount: number;
}

export interface UndocumentedEntity {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  reason: string;
  referenceCount?: number;
  versionCount?: number;
}

// UI Component Props

export interface ContextEditorPanelProps {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  onSave?: (context: EntityContext) => void;
}

export interface ExpertManagementProps {
  entityType: EntityType;
  entityId: number;
  entityName: string;
}

export interface InlineContextBadgeProps {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  variant?: 'minimal' | 'detailed';
}

// Utility types


export interface ValidationRule {
  required?: boolean;
  min?: number;
  max?: number;
  pattern?: string;
  custom?: string;
}

export const CRITICALITY_COLORS: Record<CriticalityLevel, string> = {
  1: 'gray',
  2: 'blue',
  3: 'yellow',
  4: 'orange',
  5: 'red'
};

export const EXPERTISE_ICONS: Record<ExpertiseLevel, string> = {
  OWNER: '👑',
  EXPERT: '⭐',
  FAMILIAR: '👤',
  CONTRIBUTOR: '🔧'
};

export const SENSITIVITY_COLORS: Record<SensitivityLevel, string> = {
  PUBLIC: 'green',
  INTERNAL: 'blue',
  PII: 'red',
  FINANCIAL: 'red',
  SENSITIVE: 'orange'
};

// Helper functions (can be in a separate utils file)

export function getCompletenessColor(score: number): string {
  if (score >= 80) return 'green';
  if (score >= 50) return 'yellow';
  return 'red';
}

export function getCriticalityLabel(level: CriticalityLevel): string {
  switch (level) {
    case 5:
      return 'Critical';
    case 4:
      return 'High';
    case 3:
      return 'Medium';
    case 2:
      return 'Low';
    case 1:
      return 'Minimal';
    default:
      return 'Unknown';
  }
}

export function getExpertiseLabel(level: ExpertiseLevel): string {
  switch (level) {
    case 'OWNER':
      return 'Owner - Built it, maintains it';
    case 'EXPERT':
      return 'Expert - Deep knowledge';
    case 'FAMILIAR':
      return 'Familiar - Can answer questions';
    case 'CONTRIBUTOR':
      return 'Contributor - Has made changes';
  }
}

export function formatRelativeTime(dateString: string): string {
  const past = new Date(dateString);
  const time = past.getTime();

  if (isNaN(time)) {
    return 'invalid date';
  }

  const now = new Date();
  const diffInMs = now.getTime() - time;

  if (diffInMs < 0) {
    return 'in the future';
  }

  const diffInDays = Math.floor(diffInMs / (1000 * 60 * 60 * 24));

  if (diffInDays === 0) return 'today';
  if (diffInDays === 1) return 'yesterday';
  if (diffInDays < 7) return `${diffInDays} days ago`;
  if (diffInDays < 30) return `${Math.floor(diffInDays / 7)} weeks ago`;
  if (diffInDays < 365) return `${Math.floor(diffInDays / 30)} months ago`;
  return `${Math.floor(diffInDays / 365)} years ago`;
}


export function parseValidationRules(jsonString?: string): ValidationRule | null {
  if (!jsonString) return null;
  try {
    return JSON.parse(jsonString);
  } catch {
    return null;
  }
}

export function stringifyValidationRules(rules: ValidationRule): string {
  return JSON.stringify(rules);
}