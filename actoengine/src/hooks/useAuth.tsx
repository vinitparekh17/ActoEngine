import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useApiPost } from '../hooks/useApi';

// ============================================
// Types matching backend response
// ============================================
interface User {
  userId: number;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  role: string;
}

interface LoginRequest {
  username: string;
  password: string;
}

interface LoginResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: User | null;
}

interface RefreshResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: User | null;
}

// ============================================
// Zustand Store (Token + User persistence)
// ============================================
interface AuthStore {
  token: string | null;
  refreshToken: string | null;
  user: User | null;
  setAuth: (token: string, refreshToken: string, user: User | null, expiresAt: string) => void;
  clearAuth: () => void;
  getTokenExpiry: () => string | null;
}

export const useAuthStore = create<AuthStore>()(
  persist(
    (set, _get) => ({
      token: null,
      refreshToken: null,
      user: null,

      setAuth: (token, refreshToken, user, expiresAt) => {
        set({ token, refreshToken, user });
        localStorage.setItem('tokenExpiresAt', expiresAt);
      },

      clearAuth: () => {
        set({ token: null, refreshToken: null, user: null });
        localStorage.removeItem('tokenExpiresAt');
      },

      getTokenExpiry: () => {
        return localStorage.getItem('tokenExpiresAt');
      },
    }),
    {
      name: 'actox-auth',
      partialize: (state) => ({
        token: state.token,
        refreshToken: state.refreshToken,
        user: state.user,
      }),
    }
  )
);

// ============================================
// Main useAuth Hook (Using useApi)
// ============================================
export function useAuth() {
  const { token, refreshToken, user, setAuth, clearAuth, getTokenExpiry } = useAuthStore();
  
  const isAuthenticated = !!token && !!user;

  // Login Mutation (Using useApiPost)
  const loginMutation = useApiPost<LoginResponse, LoginRequest>('/Auth/login', {
    showSuccessToast: false, // We'll show custom success message
    showErrorToast: true,
    onSuccess: (data) => {
      setAuth(data.token, data.refreshToken, data.user, data.expiresAt);
    },
  });

  // Logout Mutation
  const logoutMutation = useApiPost<void, void>('/Auth/logout', {
    showSuccessToast: false,
    showErrorToast: false,
    onSettled: () => {
      clearAuth();
    },
  });

  // Refresh Token Function (called automatically)
  const refreshTokenMutation = useApiPost<RefreshResponse, { refreshToken: string }>(
    '/Auth/refresh',
    {
      showSuccessToast: false,
      showErrorToast: false,
      onSuccess: (data) => {
        setAuth(data.token, data.refreshToken, data.user || user, data.expiresAt);
      },
      onError: () => {
        // Refresh failed - logout
        clearAuth();
      },
    }
  );

  // Auto-refresh token before expiry
  useEffect(() => {
    if (!token || !refreshToken) return;

    const expiresAt = getTokenExpiry();
    if (!expiresAt) return;

    const expiryTime = new Date(expiresAt).getTime();
    const now = Date.now();
    const timeUntilExpiry = expiryTime - now;

    // Refresh 5 minutes before expiry
    const refreshTime = timeUntilExpiry - 5 * 60 * 1000;

    if (refreshTime <= 0) {
      // Token already expired
      refreshTokenMutation.mutate({ refreshToken });
      return;
    }

    const timer = setTimeout(() => {
      refreshTokenMutation.mutate({ refreshToken });
    }, refreshTime);

    return () => clearTimeout(timer);
  }, [token, refreshToken, getTokenExpiry]);

  return {
    // State
    user,
    token,
    isAuthenticated,
    isLoading: loginMutation.isPending,
    
    // Actions
    login: loginMutation.mutateAsync,
    logout: () => logoutMutation.mutate(),
    refreshToken: () => refreshToken && refreshTokenMutation.mutate({ refreshToken }),
    
    // Mutation states
    loginError: loginMutation.error?.message,
    isLoggingIn: loginMutation.isPending,
    isLoggingOut: logoutMutation.isPending,
    
    // Utils
    clearError: () => loginMutation.reset(),
  };
}

// ============================================
// Protected Route Hook
// ============================================
export function useRequireAuth() {
  const { isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      navigate('/login', { 
        replace: true,
        state: { from: location.pathname }
      });
    }
  }, [isAuthenticated, isLoading, navigate]);

  return { isAuthenticated, isLoading };
}

// ============================================
// Auth Headers Helper
// ============================================
export function getAuthHeaders(): HeadersInit {
  const token = useAuthStore.getState().token;
  return token ? { 'Authorization': `Bearer ${token}` } : {};
}

// ============================================
// Auth Token Getter
// ============================================
export function getAuthToken(): string | null {
  return useAuthStore.getState().token;
}

// ============================================
// Role-Based Access Control Hooks
// ============================================
export function useHasRole(requiredRole: string): boolean {
  const { user } = useAuth();
  return user?.role === requiredRole;
}

export function useHasAnyRole(roles: string[]): boolean {
  const { user } = useAuth();
  return roles.some(role => user?.role === role);
}