/**
 * API Client - Centralized HTTP client for backend communication
 *
 * This module provides a type-safe wrapper around Axios with interceptors.
 * All API requests should go through this client for consistency.
 *
 * Response types match backend models in:
 * - Backend/Models/Common.cs (ApiResponse, ErrorResponse, MessageResponse)
 *
 * Features:
 * - Automatic authentication header injection via request interceptor
 * - Standardized error handling via response interceptor
 * - Type-safe response parsing
 * - Session management (401 handling with request queueing)
 * - Request/response interceptors for global behavior
 */

import axios, { type AxiosInstance, type AxiosRequestConfig, type AxiosResponse } from 'axios';
import { getAuthHeaders } from '@/hooks/useAuth';
import { ApiError, type ApiResponse, type ErrorResponse } from '@/types/api';
import { requestQueue, type QueuedRequestOptions } from './requestQueue';

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

export function isErrorResponse(value: any): value is ErrorResponse {
  return value && typeof value === 'object'
    && 'error' in value
    && 'message' in value
    && 'timestamp' in value
    && 'path' in value;
}

// ============================================
// API Client Class with Axios
// ============================================

/**
 * HTTP client for making type-safe API requests using Axios
 *
 * Handles:
 * - Request headers (auth, content-type) via request interceptor
 * - Response parsing (JSON, ApiResponse unwrapping) via response interceptor
 * - Error handling (401, 403, validation errors) via response interceptor
 * - Session management with request queueing
 */
type OnUnauthorized = () => void;

class ApiClient {
  private axiosInstance: AxiosInstance;
  private onUnauthorized?: OnUnauthorized;
  private queueingSuspendCount = 0; // Counter instead of boolean for concurrency safety

  constructor(baseURL = '/api') {
    // Create axios instance with default config
    this.axiosInstance = axios.create({
      baseURL,
      withCredentials: true,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.setupInterceptors();
  }

  /**
   * Set callback for 401 responses (dependency injection)
   */
  setUnauthorizedHandler(handler: OnUnauthorized) {
    this.onUnauthorized = handler;
  }

  /**
   * Enable/disable request queueing for 401 errors
   */
  setQueueing(enabled: boolean) {
    const MAX_QUEUEING_SUSPEND_COUNT = 1000;

    if (enabled) {
      this.queueingSuspendCount = Math.max(0, this.queueingSuspendCount - 1);
    } else {
      // Warn if hitting upper bound (possible leak)
      if (this.queueingSuspendCount >= MAX_QUEUEING_SUSPEND_COUNT) {
        console.warn('[API] queueingSuspendCount at maximum, possible leak');
      }
      // Clamp to maximum to prevent unbounded growth
      this.queueingSuspendCount = Math.min(
        MAX_QUEUEING_SUSPEND_COUNT,
        this.queueingSuspendCount + 1
      );
    }
  }

  /**
   * Setup request and response interceptors
   */
  private setupInterceptors() {
    // ============================================
    // Request Interceptor - Add auth headers
    // ============================================
    this.axiosInstance.interceptors.request.use(
      (config) => {
        // Add authentication headers to every request
        const authHeaders = getAuthHeaders();
        Object.assign(config.headers, authHeaders);
        return config;
      },
      (error) => {
        return Promise.reject(error);
      }
    );

    // ============================================
    // Response Interceptor - Handle errors and unwrap responses
    // ============================================
    this.axiosInstance.interceptors.response.use(
      (response: AxiosResponse) => {
        // Unwrap ApiResponse<T> format from backend
        const data = response.data;

        if (isApiResponse<any>(data)) {
          // Check if backend marked response as failed
          if (!data.status) {
            throw new ApiError(
              data.message || 'Operation failed',
              response.status,
              data.errors
            );
          }
          // Return response with unwrapped data
          response.data = data.data;
          return response;
        }

        // Return raw response if not ApiResponse format
        return response;
      },
      async (error) => {
        // ============================================
        // Handle 401 - Token expired
        // ============================================
        if (error.response?.status === 401) {
          const endpoint = error.config?.url || '';
          const isAuthEndpoint = endpoint.includes('/Auth/login') || endpoint.includes('/Auth/refresh');

          // Queue non-auth requests for retry after re-authentication
          if (this.queueingSuspendCount === 0 && !isAuthEndpoint) {
            console.log(`[API] 401 detected, queueing request: ${endpoint}`);

            // Atomic check-and-set to trigger re-login only once (prevents race conditions)
            // Fixed: Removed non-atomic size check, rely solely on atomic compareAndSetWaitingForAuth
            const shouldTriggerReLogin = requestQueue.compareAndSetWaitingForAuth();
            if (shouldTriggerReLogin) {
              this.onUnauthorized?.();
            }

            // Queue the request and return a promise that resolves when retried
            // Fixed: Guard error.config access with optional chaining
            return requestQueue.enqueue(endpoint, {
              method: error.config?.method,
              headers: error.config?.headers,
              data: error.config?.data,
            });
          }

          // For auth endpoints or when queueing is disabled
          this.onUnauthorized?.();
          throw new ApiError('Session expired. Please login again.', 401);
        }

        // ============================================
        // Handle 403 - Forbidden
        // ============================================
        if (error.response?.status === 403) {
          throw new ApiError('You do not have permission to perform this action', 403);
        }

        // ============================================
        // Handle other errors
        // ============================================
        const responseData = error.response?.data;

        // Check for ApiResponse error format
        if (responseData && isApiResponse<any>(responseData)) {
          throw new ApiError(
            responseData.message || 'Request failed',
            error.response?.status || 500,
            responseData.errors
          );
        }

        // Check for ErrorResponse format
        if (responseData && isErrorResponse(responseData)) {
          throw new ApiError(
            responseData.message || responseData.error || 'Request failed',
            error.response?.status || 500
          );
        }

        // Generic error
        const status = error.response?.status || 500;
        const message = error.message || `Request failed: ${status}`;
        throw new ApiError(message, status);
      }
    );
  }

  /**
   * Make a request using the axios instance
   * Note: Response interceptor unwraps AxiosResponse<T> and returns T directly
   */
  private async request<T>(config: AxiosRequestConfig): Promise<T> {
    // The response interceptor unwraps ApiResponse<T> to T in response.data
    const response = await this.axiosInstance.request<T>(config);
    return response.data;
  }

  /**
   * GET request
   */
  async get<T>(endpoint: string, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({ ...config, method: 'GET', url: endpoint });
  }

  /**
   * POST request
   */
  async post<T>(endpoint: string, body?: any, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({ ...config, method: 'POST', url: endpoint, data: body });
  }

  /**
   * PUT request
   */
  async put<T>(endpoint: string, body?: any, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({ ...config, method: 'PUT', url: endpoint, data: body });
  }

  /**
   * DELETE request
   */
  async delete<T>(endpoint: string, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({ ...config, method: 'DELETE', url: endpoint });
  }

  /**
   * PATCH request
   */
  async patch<T>(endpoint: string, body?: any, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({ ...config, method: 'PATCH', url: endpoint, data: body });
  }

  /**
   * Process all queued requests after successful re-authentication
   * Fixed: Added error handling, removed unsafe type casts
   */
  async processQueuedRequests(): Promise<void> {
    await requestQueue.processQueue(async (endpoint: string, options: QueuedRequestOptions) => {
      // Temporarily disable queueing to avoid infinite loops
      this.queueingSuspendCount++;
      try {
        return await this.request({
          url: endpoint,
          method: options.method || 'GET',
          headers: options.headers || {},
          data: options.data,
        });
      } catch (error) {
        // Log retry failure with context
        console.error(`[API] Failed to retry queued request: ${endpoint}`, error);
        // We don't re-throw here to allow other requests in the queue to be processed
        // But we could if we wanted to stop processing on first error
      } finally {
        this.queueingSuspendCount--;
      }
    });
  }

  /**
   * Clear all queued requests (called on user cancellation)
   */
  clearQueue(): void {
    requestQueue.clear();
  }

  /**
   * Get the number of queued requests
   */
  get queueSize(): number {
    return requestQueue.size;
  }

  /**
   * Get the axios instance for advanced usage
   */
  get instance(): AxiosInstance {
    return this.axiosInstance;
  }
}

// ============================================
// Initialize unauthorized handler
// ============================================

/**
 * Initialize unauthorized handler (call this in your app setup)
 */
export function initializeApiClient(onUnauthorized: OnUnauthorized) {
  api.setUnauthorizedHandler(onUnauthorized);
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
