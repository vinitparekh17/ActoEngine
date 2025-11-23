import { Component, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { ErrorFallback } from './ErrorFallback';

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
export class ApiErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
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
      console.error('API Error Boundary caught:', error, errorInfo);
    }

    // Handle 401 - Redirect to login
    if (apiError.status === 401) {
      // Error boundary can't use hooks, so we manually navigate
      window.location.href = '/login';
      return;
    }

    // Show toast for other errors
    if (apiError.status === 403) {
      toast.error('Access denied. You do not have permission.');
    } else if (apiError.status && apiError.status >= 500) {
      toast.error('Server error. Please try again later.');
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

      // Default page-level error UI
      return (
        <ErrorFallback
          error={this.state.error}
          resetError={this.reset}
          variant="page"
          showDetails={import.meta.env.DEV}
        />
      );
    }

    return this.props.children;
  }
}

// ============================================
// Hook-based Error Boundary (for functional components)
// ============================================
import { useQueryErrorResetBoundary } from '@tanstack/react-query';

export function QueryErrorBoundary({ children }: { children: ReactNode }) {
  const { reset } = useQueryErrorResetBoundary();
  const navigate = useNavigate();

  return (
    <ApiErrorBoundary
      fallback={(error, resetError) => {
        if (error.status === 401) {
          navigate('/login', { replace: true });
          return null;
        }

        return (
          <ErrorFallback
            error={error}
            resetError={() => {
              reset();
              resetError();
            }}
            variant="page"
            showDetails={import.meta.env.DEV}
          />
        );
      }}
    >
      {children}
    </ApiErrorBoundary>
  );
}