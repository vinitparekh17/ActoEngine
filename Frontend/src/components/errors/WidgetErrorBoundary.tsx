import { Component, type ReactNode } from 'react';
import { ErrorFallback } from './ErrorFallback';

interface WidgetErrorBoundaryProps {
  children: ReactNode;
  componentName?: string;
  fallback?: (error: Error, resetError: () => void, isRetrying: boolean) => ReactNode;
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
}

interface WidgetErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

/**
 * Widget-level Error Boundary
 *
 * Isolates complex widgets so their errors don't crash the entire page.
 * Provides inline error UI with manual retry capability.
 *
 * Usage:
 * ```tsx
 * <WidgetErrorBoundary componentName="User Profile">
 *   <ComplexWidget />
 * </WidgetErrorBoundary>
 * ```
 */
export class WidgetErrorBoundary extends Component<
  WidgetErrorBoundaryProps,
  WidgetErrorBoundaryState
> {
  constructor(props: WidgetErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
    };
  }

  static getDerivedStateFromError(error: Error): WidgetErrorBoundaryState {
    return {
      hasError: true,
      error,
    };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Log error in development
    if (import.meta.env.DEV) {
      console.error('[WidgetErrorBoundary] Caught error:', error, errorInfo);
    }

    // Call optional error handler with both error and errorInfo
    this.props.onError?.(error, errorInfo);

    // Optional: Send to error tracking service
    // logErrorToService(error, errorInfo);
  }

  reset = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError && this.state.error) {
      // Use custom fallback if provided
      if (this.props.fallback) {
        return this.props.fallback(this.state.error, this.reset, false);
      }

      // Default widget error UI
      return (
        <ErrorFallback
          error={this.state.error}
          resetError={this.reset}
          variant="widget"
          componentName={this.props.componentName}
        />
      );
    }

    return this.props.children;
  }
}

/**
 * Widget Error Boundary with Retry Callbacks
 *
 * Provides a convenient wrapper that calls optional retry and error callbacks.
 * Use this for widgets that need custom retry or error handling logic.
 */
interface WidgetErrorBoundaryWithCallbacksProps {
  children: ReactNode;
  componentName?: string;
  onRetry?: () => void;
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
}

/**
 * Wraps the given children with an error boundary that displays a widget-styled fallback and wires retry and error callbacks.
 *
 * @param children - The subtree to protect from render errors.
 * @param componentName - Optional name of the widget to show in the fallback UI for context.
 * @param onRetry - Optional callback invoked when the user triggers a retry from the fallback; invoked before the boundary reset.
 * @param onError - Optional callback invoked when an error is caught, receiving the error and React error info.
 * @returns A React element that renders `children` inside an error boundary which shows a widget fallback and supports retry behavior.
 */
export function WidgetErrorBoundaryWithCallbacks({
  children,
  componentName,
  onRetry,
  onError,
}: WidgetErrorBoundaryWithCallbacksProps) {
  return (
    <WidgetErrorBoundary
      componentName={componentName}
      onError={(error, errorInfo) => onError?.(error, errorInfo)}
      fallback={(error, reset, isRetrying) => (
        <ErrorFallback
          error={error}
          resetError={() => {
            onRetry?.();
            reset();
          }}
          variant="widget"
          componentName={componentName}
          isRetrying={isRetrying}
        />
      )}
    >
      {children}
    </WidgetErrorBoundary>
  );
}