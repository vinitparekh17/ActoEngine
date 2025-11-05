/**
 * API Client - Centralized HTTP client for backend communication
 *
 * This module provides a type-safe wrapper around the Fetch API.
 * All API requests should go through this client for consistency.
 *
 * Response types match backend models in:
 * - Backend/Models/Common.cs (ApiResponse, ErrorResponse, MessageResponse)
 *
 * Features:
 * - Automatic authentication header injection
 * - Standardized error handling
 * - Type-safe response parsing
 * - Session management (401 handling)
 */

import { getAuthHeaders, useAuthStore } from '@/hooks/useAuth';
import type { ApiResponse, ErrorResponse } from '@/types/api';

// ============================================
// Type Guards for Backend Response Shapes
// ============================================

/**
 * Type guard to check if response matches ApiResponse<T> shape from Common.cs
 */
export function isApiResponse<T = unknown>(value: any): value is ApiResponse<T> {
  return value && typeof value === 'object'
    && 'status' in value
    && 'message' in value
    && 'data' in value
    && 'timestamp' in value;
}

/**
 * Type guard to check if response matches ErrorResponse shape from Common.cs
 */
export function isErrorResponse(value: any): value is ErrorResponse {
  return value && typeof value === 'object'
    && 'error' in value
    && 'message' in value
    && 'timestamp' in value
    && 'path' in value;
}

// ============================================
// API Client Class
// ============================================

/**
 * HTTP client for making type-safe API requests
 *
 * Handles:
 * - Request headers (auth, content-type)
 * - Response parsing (JSON, ApiResponse unwrapping)
 * - Error handling (401, 403, validation errors)
 * - Session management
 */
class ApiClient {
  private baseUrl: string;

  constructor(baseUrl = '/api') {
    this.baseUrl = baseUrl;
  }

  /**
   * Core request method - handles all HTTP operations
   *
   * Response handling priority:
   * 1. Check HTTP status (401, 403)
   * 2. Parse JSON if content-type is application/json
   * 3. Unwrap ApiResponse<T> if present
   * 4. Return raw response for non-JSON (file downloads, etc.)
   */
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
    const contentType = response.headers.get('content-type') ?? '';
    const isJson = contentType.includes('application/json');

    // Try to parse JSON body once when present
    const body = isJson ? await response.json().catch(() => null) : null;

    if (!response.ok) {
      // Prefer ApiResponse shape from backend when available
      if (body && isApiResponse<any>(body)) {
        const err = new Error(body.message || 'Request failed');
        (err as any).status = response.status;
        (err as any).errors = body.errors;
        throw err;
      }

      // Handle ErrorResponse shape (from middleware)
      if (body && isErrorResponse(body)) {
        const err = new Error(body.message || body.error || 'Request failed');
        (err as any).status = response.status;
        throw err;
      }

      const err = new Error(`Request failed: ${response.status} ${response.statusText}`);
      (err as any).status = response.status;
      throw err;
    }

    // Unwrap ApiResponse<T> to return just the data
    if (body && isApiResponse<T>(body)) {
      if (!body.status) {
        const err = new Error(body.message || 'Operation failed');
        (err as any).status = response.status;
        (err as any).errors = body.errors;
        throw err;
      }
      return body.data as T;
    }

    // Non-JSON response (file downloads, etc.)
    return response as any;
  }

  /**
   * GET request
   */
  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'GET' });
  }

  /**
   * POST request
   */
  async post<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  /**
   * PUT request
   */
  async put<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PUT',
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  /**
   * DELETE request
   */
  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: 'DELETE' });
  }

  /**
   * PATCH request
   */
  async patch<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: 'PATCH',
      body: body ? JSON.stringify(body) : undefined,
    });
  }
}

// ============================================
// Export Singleton Instance
// ============================================

/**
 * Singleton API client instance
 *
 * Base URL can be configured via VITE_API_BASE_URL environment variable.
 * Defaults to '/api' for same-origin requests.
 */
export const api = new ApiClient(import.meta.env.VITE_API_BASE_URL || '/api');

/**
 * Alternative export name for clarity
 */
export const apiClient = api;
