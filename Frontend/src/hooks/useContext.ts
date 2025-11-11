// hooks/useContext.ts
import { useCallback } from 'react';
import { useApi, useApiPost, useApiPut, useApiDelete } from './useApi';
import { useProject } from './useProject';
import { toast } from 'sonner';
import { EntityContext, SaveContextRequest } from '../types/context';

// Types
export interface ContextData {
  purpose?: string;
  businessImpact?: string;
  dataOwner?: string;
  criticalityLevel?: number;
  businessDomain?: string;
  sensitivity?: string;
  dataSource?: string;
  validationRules?: string;
  retentionPolicy?: string;
  dataFlow?: string;
  frequency?: string;
  isDeprecated?: boolean;
  deprecationReason?: string;
  replacedBy?: string;
  expertUserIds?: number[];
}

export interface Expert {
  userId: number;
  name: string;
  email: string;
  expertiseLevel: string;
  notes?: string;
}

export interface ContextResponse {
  context: ContextData;
  experts: Expert[];
  suggestions: {
    purpose?: string;
    businessImpact?: string;
    dataOwner?: string;
  };
  completenessScore: number;
  isStale: boolean;
  dependencyCount: number;
  lastReviewed?: string;
}

export interface CoverageStats {
  breakdown: Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    total: number;
    documented: number;
    coveragePercentage: number;
    avgCompleteness?: number;
  }>;
  lastUpdated: string;
  trends?: {
    change: number;
    previousPeriod: number;
  };
}

export interface DashboardData {
  coverage: Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    total: number;
    documented: number;
    coveragePercentage: number;
    avgCompleteness?: number;
  }>;
  staleEntities: Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    entityId: number;
    entityName: string;
    lastContextUpdate: string;
    daysSinceUpdate: number;
    schemaChanged: boolean;
  }>;
  topDocumented: Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    entityId: number;
    entityName: string;
    businessDomain?: string;
    dataOwner?: string;
    completenessScore: number;
    expertCount: number;
  }>;
  criticalUndocumented: Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    entityId: number;
    entityName: string;
    reason: string;
    priority: 'HIGH' | 'MEDIUM' | 'LOW';
    lastSchemaChange?: string;
  }>;
  staleCount: number;
  lastUpdated: string;
  trends?: {
    coverageChange: number;
    period: string;
  };
}

export interface BulkImportResult {
  success: boolean;
  entityType: string;
  entityId: number;
  entityName: string;
  error?: string;
}

export interface SuggestionsResponse {
  suggestions: {
    purpose?: string;
    businessImpact?: string;
    dataOwner?: string;
  };
  // optional metadata
  confidence?: number;
  source?: string;
}

/**
 * Hook to fetch context for an entity
 */
export function useEntityContext(
  entityType: string,
  entityId: number,
  options?: { enabled?: boolean }
) {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<ContextResponse>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}`,
    {
      enabled: hasProject && !!selectedProjectId && !!entityId && (options?.enabled !== false),
      staleTime: 5 * 60 * 1000, // 5 minutes
      retry: 2,
    }
  );
}

/**
 * Hook to save/update context
 */
export function useSaveContext(
  entityType: string,
  entityId: number,
  onSuccess?: (data: EntityContext) => void // ← Fixed type
) {
  const { selectedProjectId } = useProject();

  const handleSuccess = useCallback((data: EntityContext) => { // ← Fixed type
    toast.success('Context saved', {
      description: 'Documentation updated successfully'
    });
    onSuccess?.(data);
  }, [onSuccess]);

  return useApiPut<EntityContext, SaveContextRequest>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}`,
    {
      showSuccessToast: false,
      showErrorToast: true,
      onSuccess: handleSuccess,
      invalidateKeys:
        selectedProjectId != null
          ? [
              ['projects', String(selectedProjectId), 'context', entityType, String(entityId)],
              ['projects', String(selectedProjectId), 'context', 'dashboard'],
              ['projects', String(selectedProjectId), 'context', 'statistics', 'coverage'],
            ]
          : [],
    }
  );
}

export function useQuickSaveContext(
  onSuccess?: (data: EntityContext) => void
) {
  const { selectedProjectId } = useProject();

  const handleSuccess = useCallback((data: EntityContext) => {
    const completeness = calculateCompleteness(data);
    toast.success('Context saved', {
      description: `Documentation is ${completeness}% complete`,
    });
    onSuccess?.(data);
  }, [onSuccess]);

  return useApiPost<EntityContext, {
    entityType: string;
    entityId: number;
    purpose: string;
    criticalityLevel?: number;
  }>(
    `/projects/${selectedProjectId}/context/quick-save`, // ← New endpoint
    {
      showSuccessToast: false,
      showErrorToast: true,
      onSuccess: handleSuccess,
      invalidateKeys:
        selectedProjectId != null
          ? [
              ['projects', String(selectedProjectId), 'context', 'dashboard'],
              ['projects', String(selectedProjectId), 'context', 'statistics', 'coverage'],
            ]
          : [],
    }
  );
}

// Helper function
function calculateCompleteness(context: EntityContext): number {
  const fields = [
    context.purpose,
    context.businessImpact,
    context.dataOwner,
    context.businessDomain,
  ];
  const filled = fields.filter(f => f && f.trim().length > 0).length;
  return Math.round((filled / fields.length) * 100);
}

/**
 * Hook to fetch context suggestions
 */
export function useContextSuggestions(
  entityType: string,
  entityId: number,
  enabled: boolean = true
) {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<SuggestionsResponse>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}/suggestions`,
    {
      enabled: hasProject && !!selectedProjectId && !!entityId && enabled,
      staleTime: 10 * 60 * 1000, // 10 minutes - suggestions don't change often
      retry: 1,
      showErrorToast: false, // Silent failure for suggestions
    }
  );
}

/**
 * Hook to fetch context coverage stats
 */
export function useContextCoverage() {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<CoverageStats>(
    `/projects/${selectedProjectId}/context/statistics/coverage`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 30 * 1000, // 30 seconds
      refetchInterval: 60 * 1000, // Refresh every minute
      retry: 2,
    }
  );
}

/**
 * Hook to fetch context dashboard data
 */
export function useContextDashboard() {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<DashboardData>(
    `/projects/${selectedProjectId}/context/dashboard`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 30 * 1000, // 30 seconds
      refetchInterval: 30 * 1000, // Refresh every 30 seconds
      retry: 2,
    }
  );
}

/**
 * Hook to fetch stale entities
 */
export function useStaleEntities() {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    entityId: number;
    entityName: string;
    lastContextUpdate: string;
    daysSinceUpdate: number;
    schemaChanged: boolean;
  }>>(
    `/projects/${selectedProjectId}/context/statistics/stale`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 2 * 60 * 1000, // 2 minutes
      retry: 2,
    }
  );
}

/**
 * Hook to fetch critical undocumented entities
 */
export function useCriticalUndocumented() {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    entityId: number;
    entityName: string;
    reason: string;
    priority: 'HIGH' | 'MEDIUM' | 'LOW';
  }>>(
    `/projects/${selectedProjectId}/context/statistics/critical-undocumented`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 5 * 60 * 1000, // 5 minutes
      retry: 2,
    }
  );
}

/**
 * Hook to add expert to entity
 */
export function useAddExpert(
  entityType: string,
  entityId: number,
  onSuccess?: () => void
) {
  const { selectedProjectId } = useProject();

  const handleSuccess = useCallback(() => {
    toast.success('Expert added', {
      description: 'Expert has been added successfully'
    });
    onSuccess?.();
  }, [onSuccess]);

  return useApiPost<any, {
    userId: number;
    expertiseLevel: string;
    notes?: string;
  }>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}/experts`,
    {
      showSuccessToast: false, // We handle it manually
      showErrorToast: true,
      onSuccess: handleSuccess,
      invalidateKeys:
        selectedProjectId != null
          ? [[
              'projects',
              String(selectedProjectId),
              'context',
              entityType,
              String(entityId),
            ]]
          : [],
    }
  );
}

/**
 * Hook to remove expert from entity
 */
export function useRemoveExpert(
  entityType: string,
  entityId: number,
  userId: number,
  onSuccess?: () => void
) {
  const { selectedProjectId } = useProject();

  const handleSuccess = useCallback(() => {
    toast.success('Expert removed', {
      description: 'Expert has been removed successfully'
    });
    onSuccess?.();
  }, [onSuccess]);

  return useApiDelete<void>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}/experts/${userId}`,
    {
      showSuccessToast: false, // We handle it manually
      showErrorToast: true,
      onSuccess: handleSuccess,
      invalidateKeys:
        selectedProjectId != null
          ? [[
              'projects',
              String(selectedProjectId),
              'context',
              entityType,
              String(entityId),
            ]]
          : [],
    }
  );
}

/**
 * Hook to mark context as reviewed
 */
export function useMarkContextReviewed(
  entityType: string,
  entityId: number,
  onSuccess?: () => void
) {
  const { selectedProjectId } = useProject();

  const handleSuccess = useCallback(() => {
    toast.success('Marked as reviewed', {
      description: 'Context has been marked as up to date'
    });
    onSuccess?.();
  }, [onSuccess]);

  return useApiPost<void, void>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}/mark-reviewed`,
    {
      showSuccessToast: false, // We handle it manually
      showErrorToast: true,
      onSuccess: handleSuccess,
      invalidateKeys:
        selectedProjectId != null
          ? [
              ['projects', String(selectedProjectId), 'context', entityType, String(entityId)],
              ['projects', String(selectedProjectId), 'context', 'statistics', 'stale'],
              ['projects', String(selectedProjectId), 'context', 'dashboard'],
            ]
          : [],
    }
  );
}

/**
 * Hook to bulk import context
 */
export function useBulkImportContext(onSuccess?: (results: BulkImportResult[]) => void) {
  const { selectedProjectId } = useProject();

  const handleSuccess = useCallback((results: BulkImportResult[]) => {
    const successCount = results.filter(r => r.success).length;
    const failCount = results.length - successCount;
    
    toast.success('Bulk import completed', {
      description: `${successCount} succeeded, ${failCount} failed`
    });
    
    onSuccess?.(results);
  }, [onSuccess]);

  return useApiPost<BulkImportResult[], Array<{
    entityType: string;
    entityId: number;
    context: ContextData;
  }>>(
    `/projects/${selectedProjectId}/context/bulk-import`,
    {
      showSuccessToast: false, // We handle it manually
      showErrorToast: true,
      onSuccess: handleSuccess,
      invalidateKeys:
        selectedProjectId != null
          ? [
              ['projects', String(selectedProjectId), 'context', 'dashboard'],
              ['projects', String(selectedProjectId), 'context', 'statistics', 'coverage'],
            ]
          : [],
    }
  );
}

/**
 * Hook to export context data
 */
export function useExportContext() {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<{
    format: 'json' | 'csv' | 'xlsx';
    data: any[];
    downloadUrl?: string;
  }>(
    `/projects/${selectedProjectId}/context/export`,
    {
      enabled: false, // Only fetch when explicitly triggered
      staleTime: 0, // Always fresh for exports
      retry: 1,
    }
  );
}

/**
 * Hook to get context statistics summary
 */
export function useContextStats() {
  const { selectedProjectId, hasProject } = useProject();

  return useApi<{
    totalEntities: number;
    documentedEntities: number;
    overallCoverage: number;
    staleCount: number;
    criticalUndocumentedCount: number;
    expertCount: number;
    lastUpdated: string;
  }>(
    `/projects/${selectedProjectId}/context/statistics/summary`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 60 * 1000, // 1 minute
      retry: 2,
    }
  );
}

/**
 * Hook to search entities by context content
 */
export function useContextSearch(query: string, options?: {
  entityTypes?: ('TABLE' | 'COLUMN' | 'SP')[];
  domains?: string[];
  minCompleteness?: number;
}) {
  const { selectedProjectId, hasProject } = useProject();

  const searchParams = new URLSearchParams();
  if (query) searchParams.append('q', query);
  if (options?.entityTypes?.length) {
    options.entityTypes.forEach(type => searchParams.append('type', type));
  }
  if (options?.domains?.length) {
    options.domains.forEach(domain => searchParams.append('domain', domain));
  }
  if (options?.minCompleteness !== undefined) {
    searchParams.append('minCompleteness', options.minCompleteness.toString());
  }

  const endpoint = `/projects/${selectedProjectId}/context/search${
    searchParams.toString() ? `?${searchParams.toString()}` : ''
  }`;

  return useApi<Array<{
    entityType: 'TABLE' | 'COLUMN' | 'SP';
    entityId: number;
    entityName: string;
    context: ContextData;
    completenessScore: number;
    matchScore: number;
    highlights: string[];
  }>>(
    endpoint,
    {
      enabled: hasProject && !!selectedProjectId && !!query && query.length >= 2,
      staleTime: 30 * 1000, // 30 seconds
      retry: 1,
    }
  );
}

/**
 * Utility hook to get context completeness for multiple entities
 */
export function useEntitiesContextStatus(entities: Array<{
  entityType: string;
  entityId: number;
}>) {
  const { selectedProjectId, hasProject } = useProject();

  return useApiPost<Array<{
    entityType: string;
    entityId: number;
    hasContext: boolean;
    completenessScore: number;
    isStale: boolean;
    expertCount: number;
  }>, { entities: typeof entities }>(
    `/projects/${selectedProjectId}/context/batch-status`,
    {
      showErrorToast: false,
      showSuccessToast: false,
    }
  );
}