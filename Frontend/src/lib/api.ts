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

import { ApiError, type ApiResponse, type ErrorResponse } from "@/types/api";

// ============================================
// Type Guards for Backend Response Shapes
// ============================================

/**
 * Type guard to check if response matches ApiResponse<T> shape from Common.cs
 */
export function isApiResponse<T = unknown>(
  value: any,
): value is ApiResponse<T> {
  return (
    value &&
    typeof value === "object" &&
    "status" in value &&
    "message" in value &&
    "data" in value &&
    "timestamp" in value
  );
}

export function isErrorResponse(value: any): value is ErrorResponse {
  return (
    value &&
    typeof value === "object" &&
    "error" in value &&
    "message" in value &&
    "timestamp" in value &&
    "path" in value
  );
}

// ============================================
// Helpers
// ============================================
function getCookie(name: string): string | null {
  const value = `; ${document.cookie}`;
  const parts = value.split(`; ${name}=`);
  if (parts.length === 2) return parts.pop()?.split(";").shift() || null;
  return null;
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
type OnUnauthorized = () => void;

class ApiClient {
  private readonly baseUrl: string;
  private onUnauthorized?: OnUnauthorized;

  constructor(baseUrl = "/api") {
    this.baseUrl = baseUrl;
  }

  /**
   * Set callback for 401 responses (dependency injection)
   */
  setUnauthorizedHandler(handler: OnUnauthorized) {
    this.onUnauthorized = handler;
  }

  private async request<T>(
    endpoint: string,
    options: RequestInit = {},
  ): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`;

    const csrfToken = getCookie("XSRF-TOKEN");
    const headers: HeadersInit = {
      "Content-Type": "application/json",
      // Attach CSRF token if present
      ...(csrfToken ? { "X-CSRF-TOKEN": csrfToken } : {}),
      ...options.headers,
    };

    const response = await fetch(url, {
      ...options,
      headers,
      credentials: "include",
    });

    this.handleAuthErrors(response);

    const body = await this.parseResponseBody(response);

    if (!response.ok) {
      this.handleApiErrors(response, body);
    }

    return this.unwrapResponse<T>(body, response);
  }

  private handleAuthErrors(response: Response): void {
    // Handle 401 - Token expired
    if (response.status === 401) {
      this.onUnauthorized?.();
      throw new ApiError("Session expired. Please login again.", 401);
    }

    // Handle 403 - Forbidden
    if (response.status === 403) {
      throw new ApiError(
        "You do not have permission to perform this action",
        403,
      );
    }
  }

  private async parseResponseBody(response: Response): Promise<any> {
    const contentType = response.headers.get("content-type") ?? "";
    const isJson = contentType.includes("application/json");

    if (isJson) {
      try {
        return await response.json();
      } catch {
        // Invalid JSON - will be handled by caller if needed, or treated as null body
      }
    }
    return null;
  }

  private handleApiErrors(response: Response, body: any): never {
    if (body && isApiResponse<any>(body)) {
      throw new ApiError(
        body.message || "Request failed",
        response.status,
        body.errors,
      );
    }

    if (body && isErrorResponse(body)) {
      throw new ApiError(
        body.message || body.error || "Request failed",
        response.status,
      );
    }

    throw new ApiError(
      `Request failed: ${response.status} ${response.statusText}`,
      response.status,
    );
  }

  private unwrapResponse<T>(body: any, response: Response): T {
    // Unwrap ApiResponse<T>
    if (body && isApiResponse<T>(body)) {
      if (!body.status) {
        throw new ApiError(
          body.message || "Operation failed",
          response.status,
          body.errors,
        );
      }
      return body.data as T;
    }

    // Return parsed JSON or handle empty responses
    if (body !== null && body !== undefined) {
      return body as T;
    }

    // Handle 204 No Content and other successful empty responses
    if (response.ok && (response.status === 204 || response.status === 201)) {
      return undefined as T;
    }

    throw new ApiError(
      "Response body is empty or invalid JSON",
      response.status,
    );
  }

  async get<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: "GET" });
  }

  async post<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: "POST",
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async put<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: "PUT",
      body: body ? JSON.stringify(body) : undefined,
    });
  }

  async delete<T>(endpoint: string): Promise<T> {
    return this.request<T>(endpoint, { method: "DELETE" });
  }

  async patch<T>(endpoint: string, body?: any): Promise<T> {
    return this.request<T>(endpoint, {
      method: "PATCH",
      body: body ? JSON.stringify(body) : undefined,
    });
  }
}

// Initialize unauthorized handler (call this in your app setup)
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
export const api = new ApiClient(import.meta.env.VITE_API_BASE_URL || "/api");
