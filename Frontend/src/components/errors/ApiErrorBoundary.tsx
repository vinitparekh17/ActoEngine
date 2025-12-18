import { Component, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { AlertTriangle, RefreshCw } from "lucide-react";

// ============================================
// Error Types
// ============================================
interface ApiError extends Error {
  status?: number;
  errors?: Record<string, string[]>;
}

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: (error: ApiError, reset: () => void) => ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: ApiError | null;
}

// ============================================
// API Error Boundary Component
// ============================================
export class ApiErrorBoundary extends Component<
  ErrorBoundaryProps,
  ErrorBoundaryState
> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
    };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return {
      hasError: true,
      error: error as ApiError,
    };
  }

  componentDidCatch(error: Error, errorInfo: any) {
    const apiError = error as ApiError;

    // Log to console in development
    if (import.meta.env.DEV) {
      console.error("API Error Boundary caught:", error, errorInfo);
    }

    // Handle 401 - Redirect to login
    if (apiError.status === 401) {
      // Error boundary can't use hooks, so we manually navigate
      window.location.href = "/login";
      return;
    }

    // Show toast for other errors
    if (apiError.status === 403) {
      toast.error("Access denied. You do not have permission.");
    } else if (apiError.status && apiError.status >= 500) {
      toast.error("Server error. Please try again later.");
    }

    // Optional: Send to error tracking service (Sentry, etc.)
    // logErrorToService(error, errorInfo);
  }

  reset = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError && this.state.error) {
      // Use custom fallback if provided
      if (this.props.fallback) {
        return this.props.fallback(this.state.error, this.reset);
      }

      // Default error UI
      return (
        <DefaultErrorFallback error={this.state.error} reset={this.reset} />
      );
    }

    return this.props.children;
  }
}

// ============================================
// Default Error Fallback UI
// ============================================
function DefaultErrorFallback({
  error,
  reset,
}: {
  error: ApiError;
  reset: () => void;
}) {
  return (
    <div className="flex min-h-[400px] items-center justify-center p-4">
      <div className="w-full max-w-md space-y-4 rounded-2xl border border-red-200 bg-red-50 p-6">
        {/* Error Icon */}
        <div className="flex justify-center">
          <div className="rounded-full bg-red-100 p-3">
            <AlertTriangle className="h-8 w-8 text-red-600" />
          </div>
        </div>

        {/* Error Message */}
        <div className="text-center">
          <h2 className="text-lg font-semibold text-red-900">
            Something went wrong
          </h2>
          <p className="mt-2 text-sm text-red-600">{error.message}</p>

          {/* Validation Errors */}
          {error.errors && (
            <div className="mt-4 space-y-1 text-left">
              {Object.entries(error.errors).map(([field, messages]) => (
                <div key={field} className="text-xs text-red-600">
                  <strong>{field}:</strong> {messages.join(", ")}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex gap-2">
          <button
            onClick={reset}
            className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700"
          >
            <RefreshCw className="h-4 w-4" />
            Try Again
          </button>

          <button
            onClick={() => (window.location.href = "/dashboard")}
            className="flex-1 rounded-xl border border-red-300 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-100"
          >
            Go to Dashboard
          </button>
        </div>
      </div>
    </div>
  );
}

// ============================================
// Hook-based Error Boundary (for functional components)
// ============================================
import { useQueryErrorResetBoundary } from "@tanstack/react-query";

export function QueryErrorBoundary({ children }: { children: ReactNode }) {
  const { reset } = useQueryErrorResetBoundary();
  const navigate = useNavigate();

  return (
    <ApiErrorBoundary
      fallback={(error, resetError) => {
        if (error.status === 401) {
          navigate("/login", { replace: true });
          return null;
        }

        return (
          <DefaultErrorFallback
            error={error}
            reset={() => {
              reset();
              resetError();
            }}
          />
        );
      }}
    >
      {children}
    </ApiErrorBoundary>
  );
}
