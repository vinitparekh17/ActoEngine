import { useQuery, useMutation, useQueryClient, type UseQueryOptions, type UseMutationOptions } from '@tanstack/react-query';
import { getAuthHeaders, useAuthStore } from './useAuth';
import { toast } from 'sonner';

// ============================================
// Types matching backend ApiResponse<T>
// ============================================
interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

interface ApiErrorResponse {
  success: false;
  message: string;
  errors?: Record<string, string[]>;
}

// ============================================
// API Client (Fetch wrapper)
// ============================================
class ApiClient {
  private baseUrl: string;

  constructor(baseUrl = '/api') {
    this.baseUrl = baseUrl;
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;

    // Merge headers with defaults
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...getAuthHeaders(),
      ...options.headers,
    };

    const response = await fetch(url, {
      ...options,
      headers,
      credentials: 'include', // Include cookies for cross-origin requests
    });

    // Handle 401 - Token expired
    if (response.status === 401) {
      const { clearAuth } = useAuthStore.getState();
      clearAuth();

      // Throw error to be caught by error boundary
      const error = new Error('Session expired. Please login again.');
      (error as any).status = 401;
      throw error;
    }

    // Handle 403 - Forbidden
    if (response.status === 403) {
      const error = new Error('You do not have permission to perform this action');
      (error as any).status = 403;
      throw error;
    }

    // Parse response
    const contentType = response.headers.get('content-type');
    const isJson = contentType?.includes('application/json');

    if (!response.ok) {
      if (isJson) {
        const error: ApiErrorResponse = await response.json();
        const err = new Error(error.message || 'Request failed');
        (err as any).status = response.status;
        (err as any).errors = error.errors; // Validation errors
        throw err;
      }

      const err = new Error(`Request failed: ${response.statusText}`);
      (err as any).status = response.status;
      throw err;
    }

    if (isJson) {
      const data: ApiResponse<T> = await response.json();
      return data.data;
    }

    // Non-JSON response (file downloads, etc.)
    return response as any;
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' });
  }

  async post<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async put<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' });
  }

  async patch<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PATCH',
      body: body ? JSON.stringify(body) : undefined,
    });
  }
}

export const api = new ApiClient(import.meta.env.VITE_API_BASE_URL || '/api');

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
  const finalQueryKey = queryKey || generateQueryKey(endpoint);

  return useQuery<T, Error>({
    queryKey: finalQueryKey,
    queryFn: () => api.get<T>(endpoint),
    staleTime: 60 * 1000, // 1 minute default
    ...queryOptions,
    // Type assertion needed for TanStack Query v5
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
};

// ============================================
// Export API client for direct use
// ============================================
export { api as apiClient }