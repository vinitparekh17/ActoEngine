import { AlertTriangle, RefreshCw, Home, ChevronLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

export interface ErrorFallbackProps {
  error: Error;
  resetError: () => void;
  variant?: 'widget' | 'page' | 'app';
  isRetrying?: boolean;
  componentName?: string;
  showDetails?: boolean;
}

/**
 * Shared error fallback UI component for error boundaries
 * Supports different variants for widget, page, and app-level errors
 */
export function ErrorFallback({
  error,
  resetError,
  variant = 'widget',
  isRetrying = false,
  componentName,
  showDetails = import.meta.env.DEV,
}: ErrorFallbackProps) {
  const isWidget = variant === 'widget';
  const isPage = variant === 'page';
  const isApp = variant === 'app';

  // Widget-level error (inline, compact)
  if (isWidget) {
    return (
      <Alert variant="destructive" className="m-4">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>
          {componentName ? `${componentName} Error` : 'Something went wrong'}
        </AlertTitle>
        <AlertDescription className="mt-2 space-y-3">
          <p className="text-sm">{error.message || 'An unexpected error occurred'}</p>
          {showDetails && error.stack && (
            <details className="text-xs opacity-75">
              <summary className="cursor-pointer hover:opacity-100">
                View technical details
              </summary>
              <pre className="mt-2 overflow-auto max-h-32 p-2 bg-destructive/10 rounded">
                {error.stack}
              </pre>
            </details>
          )}
          <Button
            onClick={resetError}
            disabled={isRetrying}
            size="sm"
            variant="outline"
            className="mt-2"
          >
            {isRetrying ? (
              <>
                <RefreshCw className="mr-2 h-3 w-3 animate-spin" />
                Retrying...
              </>
            ) : (
              <>
                <RefreshCw className="mr-2 h-3 w-3" />
                Try Again
              </>
            )}
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  // Page-level error (full page, centered)
  if (isPage) {
    const navigate = useNavigate();
    
    return (
      <div className="flex items-center justify-center min-h-[400px] p-8">
        <div className="max-w-md w-full space-y-6">
          <div className="text-center">
            <AlertTriangle className="mx-auto h-12 w-12 text-destructive" />
            <h2 className="mt-4 text-2xl font-bold">Page Error</h2>
            <p className="mt-2 text-muted-foreground">
              {error.message || 'An unexpected error occurred while loading this page'}
            </p>
          </div>

          {showDetails && error.stack && (
            <Alert>
              <AlertDescription>
                <details className="text-xs">
                  <summary className="cursor-pointer font-medium mb-2">
                    Technical Details
                  </summary>
                  <pre className="overflow-auto max-h-48 p-3 bg-muted rounded text-xs">
                    {error.stack}
                  </pre>
                </details>
              </AlertDescription>
            </Alert>
          )}

          <div className="flex gap-3">
            <Button
              onClick={resetError}
              disabled={isRetrying}
              className="flex-1"
              variant="default"
            >
              {isRetrying ? (
                <>
                  <RefreshCw className="mr-2 h-4 w-4 animate-spin" />
                  Retrying...
                </>
              ) : (
                <>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Try Again
                </>
              )}
            </Button>
            <Button
              onClick={() => {
                try {
                  navigate(-1);
                } catch {
                  navigate('/');
                }
              }}
              variant="outline"
              className="flex-1"
            >
              <ChevronLeft className="mr-2 h-4 w-4" />
              Go Back
            </Button>
          </div>
        </div>
      </div>
    );
  }

  // App-level error (full screen, critical)
  if (isApp) {
    return (
      <div className="flex items-center justify-center min-h-screen p-8 bg-background">
        <div className="max-w-lg w-full space-y-8">
          <div className="text-center space-y-4">
            <AlertTriangle className="mx-auto h-16 w-16 text-destructive" />
            <h1 className="text-3xl font-bold">Application Error</h1>
            <p className="text-lg text-muted-foreground">
              We encountered a critical error. Please try refreshing the page.
            </p>
            <p className="text-sm text-muted-foreground">
              {error.message || 'An unexpected error occurred'}
            </p>
          </div>

          {showDetails && error.stack && (
            <Alert>
              <AlertDescription>
                <details className="text-xs">
                  <summary className="cursor-pointer font-medium mb-2">
                    Error Details (for developers)
                  </summary>
                  <pre className="overflow-auto max-h-64 p-4 bg-muted rounded text-xs font-mono">
                    {error.stack}
                  </pre>
                </details>
              </AlertDescription>
            </Alert>
          )}

          <div className="flex flex-col gap-3">
            <Button
              onClick={resetError}
              disabled={isRetrying}
              size="lg"
              className="w-full"
            >
              {isRetrying ? (
                <>
                  <RefreshCw className="mr-2 h-5 w-5 animate-spin" />
                  Retrying...
                </>
              ) : (
                <>
                  <RefreshCw className="mr-2 h-5 w-5" />
                  Retry
                </>
              )}
            </Button>
            <Button
              onClick={() => (window.location.href = '/')}
              variant="outline"
              size="lg"
              className="w-full"
            >
              <Home className="mr-2 h-5 w-5" />
              Go to Dashboard
            </Button>
          </div>

          <p className="text-xs text-center text-muted-foreground">
            If this problem persists, please contact support with the error details above.
          </p>
        </div>
      </div>
    );
  }

  return null;
}
