import { Navigate } from "react-router-dom";
import { useAuthorization } from "@/hooks/useAuth";

interface PermissionRouteProps {
  children: React.ReactNode;
  permission: string;
  fallback?: React.ReactNode;
}

/**
 * Route wrapper that enforces permission checks.
 * If user lacks the required permission, redirects to fallback or dashboard.
 */
export function PermissionRoute({
  children,
  permission,
  fallback,
}: PermissionRouteProps) {
  const hasPermission = useAuthorization(permission);

  if (!hasPermission) {
    if (fallback) {
      return <>{fallback}</>;
    }
    // Redirect to dashboard with a state indicating access denied
    return (
      <Navigate
        to="/access-denied"
        replace
        state={{ accessDenied: true, requiredPermission: permission }}
      />
    );
  }

  return <>{children}</>;
}
