import { Navigate } from 'react-router-dom';
import { useAuthorization } from '@/hooks/useAuth';
import { useAuth } from '@/hooks/useAuth';

interface PermissionRouteProps {
    children: React.ReactNode;
    permission: string;
    fallback?: React.ReactNode;
}

/**
 * Route wrapper that enforces permission checks.
 * If user lacks the required permission, redirects to fallback or dashboard.
 */
export function PermissionRoute({ children, permission, fallback }: PermissionRouteProps) {
    const hasPermission = useAuthorization(permission);
    const { user } = useAuth();

    // Debug logging
    console.log('[PermissionRoute] Checking permission:', permission);
    console.log('[PermissionRoute] User:', user);
    console.log('[PermissionRoute] User permissions:', user?.permissions);
    console.log('[PermissionRoute] Has permission:', hasPermission);

    if (!hasPermission) {
        console.log('[PermissionRoute] Access DENIED - redirecting to dashboard');
        if (fallback) {
            return <>{fallback}</>;
        }
        // Redirect to dashboard with a state indicating access denied
        return <Navigate to="/dashboard" replace state={{ accessDenied: true, requiredPermission: permission }} />;
    }

    console.log('[PermissionRoute] Access GRANTED - rendering children');
    return <>{children}</>;
}
