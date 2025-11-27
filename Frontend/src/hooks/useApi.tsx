import { useQuery, useMutation, useQueryClient, type UseQueryOptions, type UseMutationOptions } from '@tanstack/react-query';
import { toast } from 'sonner';
import { api } from '@/lib/api';

// NOTE: Types are sourced from '@/types/api' to stay in sync with backend Models/Common.cs
// NOTE: API client is sourced from '@/lib/api' for centralized HTTP communication

// ============================================
// useApi - Query Hook
// ============================================
interface UseApiOptions<T> extends Omit<UseQueryOptions<T, Error>, 'queryKey' | 'queryFn'> {
  showErrorToast?: boolean;
  queryKey?: any[]; // Allow custom query keys
}

export function useApi<T>(
  endpoint: string,
  options?: UseApiOptions<T>
) {
  const { showErrorToast = true, queryKey, ...queryOptions } = options || {};
  
  // Generate query key from endpoint if not provided
  const finalQueryKey = queryKey || (endpoint ? generateQueryKey(endpoint) : ['api', 'invalid']);

  return useQuery<T, Error>({
    queryKey: finalQueryKey,
    queryFn: () => {
      if (!endpoint || endpoint === '') {
        return Promise.reject(new Error('Invalid endpoint'));
      }
      return api.get<T>(endpoint);
    },
    enabled: !!endpoint && endpoint !== '',
    staleTime: 60 * 1000, // 1 minute default
    ...queryOptions,
  } as any);
}

// ============================================
// Query Key Generator (Smart key generation)
// ============================================
function generateQueryKey(endpoint: string): string[] {
  // Remove leading slash and query params
  const cleanEndpoint = endpoint.replace(/^\//, '').split('?')[0];

  // Split by / to create hierarchical key
  const parts = cleanEndpoint.split('/').filter(Boolean);

  return parts;
}

// Example:
// '/CodeGen/schema/tables/5' → ['CodeGen', 'schema', 'tables', '5']
// '/Project' → ['Project']

// ============================================
// useApiMutation - Mutation Hook
// ============================================
type HttpMethod = 'POST' | 'PUT' | 'DELETE' | 'PATCH';

interface UseApiMutationOptions<TData, TVariables> extends Omit<UseMutationOptions<TData, Error, TVariables>, 'mutationFn'> {
  showSuccessToast?: boolean;
  showErrorToast?: boolean;
  successMessage?: string;
  invalidateKeys?: string[][];
}

export function useApiMutation<TData = unknown, TVariables = void>(
  endpoint: string,
  method: HttpMethod = 'POST',
  options?: UseApiMutationOptions<TData, TVariables>
) {
  const queryClient = useQueryClient();
  const {
    showSuccessToast = true,
    showErrorToast = true,
    successMessage,
    invalidateKeys = [],
    ...mutationOptions
  } = options || {};

  return useMutation<TData, Error, TVariables>({
    mutationFn: (variables: TVariables) => {
      const paramMatches = Array.from(endpoint.matchAll(/:([a-zA-Z0-9_]+)/g));
      const paramKeys = paramMatches.map((match) => match[1]);

      let url = endpoint.replace(/:([a-zA-Z0-9_]+)/g, (_, key) => {
        const value = (variables as Record<string, unknown>)?.[key];
        if (value === undefined || value === null) {
          throw new Error(`Missing required URL parameter: ${key}`);
        }
        return encodeURIComponent(String(value));
      });

      const bodyVariables = variables ? { ...(variables as Record<string, unknown>) } : {};
      for (const key of paramKeys) {
        delete bodyVariables[key];
      }

      switch (method) {
        case 'POST':
          return api.post<TData>(url, bodyVariables);
        case 'PUT':
          return api.put<TData>(url, bodyVariables);
        case 'PATCH':
          return api.patch<TData>(url, bodyVariables);
        case 'DELETE':
          return api.delete<TData>(url);
        default:
          throw new Error(`Unsupported method: ${method}`);
      }
    },
    onSuccess: (data, variables, context) => {
      if (showSuccessToast) {
        toast.success(successMessage || 'Operation successful');
      }

      // Invalidate specified query keys
      invalidateKeys.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: key });
      });

      mutationOptions.onSuccess?.(data, variables, context, null as any);
    },
    onError: (error, variables, context) => {
      if (showErrorToast) {
        toast.error(error.message || 'Operation failed');
      }
      mutationOptions.onError?.(error, variables, context, null as any);
    },
    ...mutationOptions,
  });
}

// ============================================
// Specialized Hooks for Common Patterns
// ============================================

/**
 * useApiQuery - Alternative name for useApi (for clarity)
 */
export const useApiQuery = useApi;

/**
 * useApiPost - POST mutation shorthand
 */
export function useApiPost<TData = unknown, TVariables = void>(
  endpoint: string,
  options?: UseApiMutationOptions<TData, TVariables>
) {
  return useApiMutation<TData, TVariables>(endpoint, 'POST', options);
}

/**
 * useApiPut - PUT mutation shorthand
 */
export function useApiPut<TData = unknown, TVariables = void>(
  endpoint: string,
  options?: UseApiMutationOptions<TData, TVariables>
) {
  return useApiMutation<TData, TVariables>(endpoint, 'PUT', options);
}

/**
 * useApiDelete - DELETE mutation shorthand
 */
export function useApiDelete<TData = unknown, TVariables = void>(
  endpoint: string,
  options?: UseApiMutationOptions<TData, TVariables>
) {
  return useApiMutation<TData, TVariables>(endpoint, 'DELETE', options);
}

// ============================================
// Query Key Helpers
// ============================================
export const queryKeys = {
  projects: {
    all: () => ['projects'] as const,
    detail: (id: number) => ['projects', id] as const,
  },
  clients: {
    all: () => ['clients'] as const,
    detail: (id: number) => ['clients', id] as const,
  },
  tables: {
    all: (projectId: number) => ['tables', projectId] as const,
    schema: (projectId: number, tableName: string) => ['tables', projectId, tableName, 'schema'] as const,
  },
  codeGen: {
    history: () => ['codeGen', 'history'] as const,
  },
  users: {
    all: () => ['users'] as const,
    detail: (id: number) => ['users', id] as const,
  },
  roles: {
    all: () => ['roles'] as const,
    detail: (id: number) => ['roles', id] as const,
    permissions: (id: number) => ['roles', id, 'permissions'] as const,
  },
  permissions: {
    all: () => ['permissions'] as const,
    grouped: () => ['permissions', 'grouped'] as const,
  },
};

// ============================================
// Re-export API client for convenience
// ============================================
export { api } from '@/lib/api';