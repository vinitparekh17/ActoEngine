import { useState, useCallback } from 'react';
import { LogIn, AlertCircle } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useAuthStore } from '@/hooks/useAuth';
import { api } from '@/lib/api';

interface ReLoginModalProps {
  isOpen: boolean;
  onSuccess: () => void;
  onCancel: () => void;
}

interface LoginRequest {
  username: string;
  password: string;
}

interface LoginResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: {
    userId: number;
    username: string;
    email: string;
    role: string;
  } | null;
}

/**
 * Re-login Modal for 401 error recovery
 *
 * Allows users to re-authenticate without losing page state or pending requests.
 * After successful re-authentication, queued requests are automatically retried.
 */
export function ReLoginModal({ isOpen, onSuccess, onCancel }: ReLoginModalProps) {
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const user = useAuthStore((s) => s.user);
  const setAuth = useAuthStore((s) => s.setAuth);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      setError(null);

      if (!user?.username) {
        setError('Username not found. Please refresh and try again.');
        return;
      }

      if (!password.trim()) {
        setError('Please enter your password');
        return;
      }

      setIsLoading(true);

      try {
        const response = await api.post<LoginResponse>('/Auth/login', {
          username: user.username,
          password,
        } as LoginRequest);

        // Update auth store with new tokens
        setAuth(
          response.token,
          response.refreshToken,
          response.user ?? user,
          response.expiresAt
        );

        // Clear password and notify success
        setPassword('');
        onSuccess();
      } catch (err: unknown) {
        // Only log detailed errors in development
        if (import.meta.env.DEV) {
          console.error('Re-login failed:', err);
        }

        // Safely extract user-facing error message
        let errorMessage = 'Invalid password. Please try again.';
        if (err && typeof err === 'object') {
          if ('message' in err && typeof err.message === 'string') {
            errorMessage = err.message;
          }
        }

        setError(errorMessage);
      } finally {
        setIsLoading(false);
      }
    },
    [user, password, setAuth, onSuccess]
  );

  const handleCancel = useCallback(() => {
    setPassword('');
    setError(null);
    onCancel();
  }, [onCancel]);

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && !isLoading && handleCancel()}>
      <DialogContent showCloseButton={false}>
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <LogIn className="h-5 w-5" />
              Session Expired
            </DialogTitle>
            <DialogDescription>
              Your session has expired. Please enter your password to continue without
              losing your work.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4 py-4">
            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="username">Username</Label>
              <Input
                id="username"
                type="text"
                value={user?.username || ''}
                disabled
                className="bg-muted"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                autoFocus
                disabled={isLoading}
                autoComplete="current-password"
              />
            </div>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={handleCancel}
              disabled={isLoading}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isLoading || !password.trim()}>
              {isLoading ? 'Signing in...' : 'Sign In'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
