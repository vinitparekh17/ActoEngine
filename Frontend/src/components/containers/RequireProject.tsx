import { ReactNode } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { AlertCircle } from 'lucide-react';
import { useProject } from '@/hooks/useProject';

interface RequireProjectProps {
  children: ReactNode;
  /**
   * Fallback UI variant to display when no project is selected
   * - 'alert': Alert with navigation link (best for pages)
   * - 'message': Simple centered message (best for full pages)
   * - 'card': Card with alert inside (best for embedded components)
   * - 'card-simple': Card with simple text (best for widgets)
   * - 'silent': Return null (best for optional badges/indicators)
   * - 'redirect': Redirect to /projects page
   * @default 'alert'
   */
  fallback?: 'alert' | 'message' | 'card' | 'card-simple' | 'silent' | 'redirect';
  /**
   * Custom message to display in the fallback UI
   * @default 'Please select a project first'
   */
  message?: string;
  /**
   * Custom redirect path when using 'redirect' fallback
   * @default '/projects'
   */
  redirectTo?: string;
  /**
   * Custom fallback UI (overrides the fallback variant)
   */
  customFallback?: ReactNode;
}

/**
 * Container component that ensures a project is selected before rendering children.
 * Provides various fallback UI options when no project is selected.
 *
 * @example
 * // With default alert fallback
 * <RequireProject>
 *   <YourComponent />
 * </RequireProject>
 *
 * @example
 * // With custom message and card fallback
 * <RequireProject
 *   fallback="card"
 *   message="Please select a project to manage experts."
 * >
 *   <ExpertsList />
 * </RequireProject>
 *
 * @example
 * // Silent fallback for optional components
 * <RequireProject fallback="silent">
 *   <ProjectBadge />
 * </RequireProject>
 *
 * @example
 * // Auto-redirect to projects page
 * <RequireProject fallback="redirect">
 *   <ProjectSettings />
 * </RequireProject>
 */
export function RequireProject({
  children,
  fallback = 'alert',
  message = 'Please select a project first',
  redirectTo = '/projects',
  customFallback,
}: RequireProjectProps) {
  const { hasProject } = useProject();

  if (hasProject) {
    return <>{children}</>;
  }

  // Custom fallback takes precedence
  if (customFallback) {
    return <>{customFallback}</>;
  }

  // Built-in fallback variants
  switch (fallback) {
    case 'silent':
      return null;

    case 'redirect':
      return <Navigate to={redirectTo} replace />;

    case 'message':
      return (
        <div className="flex h-screen items-center justify-center">
          <p className="text-gray-500">{message}</p>
        </div>
      );

    case 'card-simple':
      return (
        <Card>
          <CardContent className="py-4">
            <p className="text-xs text-muted-foreground text-center">
              {message}
            </p>
          </CardContent>
        </Card>
      );

    case 'card':
      return (
        <Card>
          <CardContent className="py-6">
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{message}</AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      );

    case 'alert':
    default:
      return (
        <div className="space-y-6 p-6">
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{message}</AlertDescription>
          </Alert>
          <div className="flex justify-center">
            <Button asChild>
              <Link to={redirectTo}>Select Project</Link>
            </Button>
          </div>
        </div>
      );
  }
}
