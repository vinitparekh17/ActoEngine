import { useState, useCallback, type ReactNode } from "react";
/**
 * Form field error type
 */
export interface FieldError {
  field: string;
  message: string;
}

/**
 * Form validation state and methods returned by useFormValidation hook
 * Fixed: Expanded to match actual hook return shape for type safety
 */
export interface FormValidationState {
  errors: Record<string, string>;
  hasErrors: boolean;
  setFieldError: (field: string, message: string) => void;
  setFieldErrors: (fieldErrors: Record<string, string>) => void;
  setApiError: (error: unknown) => void;
  clearError: (field: string) => void;
  clearAllErrors: () => void;
  hasError: (field: string) => boolean;
  getError: (field: string) => string | undefined;
  validateField: (
    field: string,
    value: unknown,
    rules: ValidationRule[],
  ) => boolean;
}

/**
 * Hook for inline form validation with 400 error handling
 *
 * Automatically extracts and displays validation errors from 400 API responses.
 * Provides utilities for setting, clearing, and managing field-level errors.
 *
 * Usage:
 * ```tsx
 * const { errors, setApiError, clearError, clearAllErrors, hasError } = useFormValidation();
 *
 * try {
 *   await api.post('/endpoint', data);
 * } catch (error) {
 *   if (error.status === 400) {
 *     setApiError(error);
 *   }
 * }
 *
 * <Input error={errors.username} />
 * ```
 */
export function useFormValidation(): FormValidationState {
  const [errors, setErrors] = useState<Record<string, string>>({});

  /**
   * Set error for a specific field
   */
  const setFieldError = useCallback((field: string, message: string) => {
    setErrors((prev) => ({
      ...prev,
      [field]: message,
    }));
  }, []);

  /**
   * Set multiple field errors at once
   */
  const setFieldErrors = useCallback((fieldErrors: Record<string, string>) => {
    setErrors((prev) => ({
      ...prev,
      ...fieldErrors,
    }));
  }, []);

  /**
   * Extract and set errors from API error response (400 validation errors)
   * Fixed: Added explicit type definition for error object structure
   */
  const setApiError = useCallback(
    (error: unknown) => {
      // Define the expected error structure for type narrowing
      interface ValidationError {
        errors?: Record<string, string[] | string>;
        message?: string;
      }

      // Type guard to check if error matches ValidationError structure
      const isValidationError = (err: unknown): err is ValidationError => {
        return (
          err !== null &&
          typeof err === "object" &&
          ("errors" in err || "message" in err) &&
          (("errors" in err &&
            (typeof (err as any).errors === "object" ||
              Array.isArray((err as any).errors))) ||
            ("message" in err && typeof (err as any).message === "string"))
        );
      };

      if (!isValidationError(error)) {
        return;
      }

      // Handle ApiError with errors field
      if (error.errors && typeof error.errors === "object") {
        const fieldErrors: Record<string, string> = {};

        // Convert errors object to flat field-message map
        // Fixed: Explicit type assertion after validation
        Object.entries(
          error.errors as Record<string, string[] | string>,
        ).forEach(([field, messages]) => {
          if (Array.isArray(messages) && messages.length > 0) {
            // Use first error message for each field
            fieldErrors[field] = messages[0];
          } else if (typeof messages === "string") {
            fieldErrors[field] = messages;
          }
        });

        setErrors(fieldErrors);
        return;
      }

      // Handle standard validation error format
      if (error.message && typeof error.message === "string") {
        // Try to extract field name from message like "Username is required"
        const match = error.message.match(/^(\w+)\s/i);
        if (match) {
          const field = match[1];
          setFieldError(field, error.message);
        } else {
          // Generic error - set on '_general' field
          setFieldError("_general", error.message);
        }
      }
    },
    [setFieldError],
  );

  /**
   * Clear error for a specific field
   */
  const clearError = useCallback((field: string) => {
    setErrors((prev) => {
      const newErrors = { ...prev };
      delete newErrors[field];
      return newErrors;
    });
  }, []);

  /**
   * Clear all errors
   */
  const clearAllErrors = useCallback(() => {
    setErrors({});
  }, []);

  /**
   * Check if a specific field has an error
   */
  const hasError = useCallback(
    (field: string): boolean => {
      return field in errors && errors[field].length > 0;
    },
    [errors],
  );

  /**
   * Get error message for a specific field
   */
  const getError = useCallback(
    (field: string): string | undefined => {
      return errors[field];
    },
    [errors],
  );

  /**
   * Validate a field manually (for custom validation rules)
   * Fixed: Changed value parameter from any to unknown for type safety
   */
  const validateField = useCallback(
    (field: string, value: unknown, rules: ValidationRule[]): boolean => {
      for (const rule of rules) {
        const error = rule(value);
        if (error) {
          setFieldError(field, error);
          return false;
        }
      }
      clearError(field);
      return true;
    },
    [setFieldError, clearError],
  );

  return {
    errors,
    hasErrors: Object.keys(errors).length > 0,
    setFieldError,
    setFieldErrors,
    setApiError,
    clearError,
    clearAllErrors,
    hasError,
    getError,
    validateField,
  };
}

/**
 * Validation rule type
 * Fixed: Changed value parameter from any to unknown for type safety
 */
export type ValidationRule = (value: unknown) => string | null;

/**
 * Common validation rules
 */
export const validationRules = {
  /**
   * Fixed: Only reject null, undefined, or empty string (accept false and 0)
   */
  required:
    (fieldName: string): ValidationRule =>
      (value) => {
        if (
          value === null ||
          value === undefined ||
          (typeof value === "string" && value.trim() === "")
        ) {
          return `${fieldName} is required`;
        }
        return null;
      },

  /**
   * Fixed: Guard .length access with type check
   */
  minLength:
    (fieldName: string, min: number): ValidationRule =>
      (value) => {
        if (
          value != null &&
          (typeof value === "string" || Array.isArray(value))
        ) {
          if (value.length < min) {
            return `${fieldName} must be at least ${min} characters`;
          }
        }
        return null;
      },

  /**
   * Fixed: Guard .length access with type check
   */
  maxLength:
    (fieldName: string, max: number): ValidationRule =>
      (value) => {
        if (
          value != null &&
          (typeof value === "string" || Array.isArray(value))
        ) {
          if (value.length > max) {
            return `${fieldName} must be at most ${max} characters`;
          }
        }
        return null;
      },

  email:
    (fieldName: string): ValidationRule =>
      (value) => {
        if (
          value &&
          typeof value === "string" &&
          !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
        ) {
          return `${fieldName} must be a valid email address`;
        }
        return null;
      },

  pattern:
    (fieldName: string, pattern: RegExp, message: string): ValidationRule =>
      (value) => {
        if (value && typeof value === "string" && !pattern.test(value)) {
          return message;
        }
        return null;
      },

  match:
    (
      fieldName: string,
      otherValue: unknown,
      otherFieldName: string,
    ): ValidationRule =>
      (value) => {
        if (value !== otherValue) {
          return `${fieldName} must match ${otherFieldName}`;
        }
        return null;
      },

  custom:
    (validator: (value: unknown) => boolean, message: string): ValidationRule =>
      (value) => {
        if (!validator(value)) {
          return message;
        }
        return null;
      },
};

/**
 * Form field component wrapper that displays validation errors
 *
 * Usage:
 * ```tsx
 * const { errors, clearError } = useFormValidation();
 *
 * <FormField
 *   error={errors.username}
 *   onFocus={() => clearError('username')}
 * >
 *   <Input name="username" />
 * </FormField>
 * ```
 */
interface FormFieldProps {
  children: ReactNode;
  error?: string;
  onFocus?: () => void;
  className?: string;
}

export function FormField({
  children,
  error,
  onFocus,
  className = "",
}: FormFieldProps) {
  return (
    <div className={`space-y-1 ${className}`} onFocus={onFocus}>
      {children}
      {error && (
        <p className="text-xs text-destructive font-medium" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
