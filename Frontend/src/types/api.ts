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

export interface ErrorResponse {
  error: string;
  message: string;
  timestamp: string;
  path: string;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public errors?: string[],
  ) {
    super(message);
    this.name = "ApiError";
  }
}
