import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useApiPost } from './useApi';
import { rolePermissions } from '../config/permissions';

// ----------------------------
// Types & constants
// ----------------------------
interface User {
  userId: number;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  role: string;
  permissions?: string[];
}

interface LoginRequest { username: string; password: string; }
interface AuthTokens { token: string; refreshToken: string; expiresAt: string; }
interface LoginResponse extends AuthTokens { user: User | null; }
interface RefreshResponse extends AuthTokens { user: User | null; }

const TOKEN_REFRESH_BUFFER = 5 * 60 * 1000;
const AUTH_STORAGE_KEY = 'actox-auth';

// ----------------------------
// Zustand store
// ----------------------------
interface AuthStore {
  token: string | null;
  refreshToken: string | null;
  user: User | null;
  tokenExpiresAt: string | null;

  setAuth: (token: string, refreshToken: string, user: User | null, expiresAt: string) => void;
  clearAuth: () => void;
  updateUser: (user: User) => void;
  isTokenExpired: () => boolean;
  getTimeUntilExpiry: () => number;
}

export const useAuthStore = create<AuthStore>()(
  persist(
    (set, get) => ({
      token: null,
      refreshToken: null,
      user: null,
      tokenExpiresAt: null,

      setAuth: (token, refreshToken, user, expiresAt) =>
        set({ token, refreshToken, user, tokenExpiresAt: expiresAt }),

      clearAuth: () =>
        set({ token: null, refreshToken: null, user: null, tokenExpiresAt: null }),

      updateUser: (user) => set({ user }),

      isTokenExpired: () => {
        const expiry = get().tokenExpiresAt;
        return !expiry || Date.now() >= new Date(expiry).getTime();
      },

      getTimeUntilExpiry: () => {
        const expiry = get().tokenExpiresAt;
        return expiry ? Math.max(0, new Date(expiry).getTime() - Date.now()) : 0;
      },
    }),
    {
      name: AUTH_STORAGE_KEY,
      partialize: (s) => ({
        token: s.token,
        refreshToken: s.refreshToken,
        user: s.user,
        tokenExpiresAt: s.tokenExpiresAt,
      }),
    }
  )
);

// ----------------------------
// Pure util for headers (no React/Zustand import)
// ----------------------------
export function makeAuthHeaders(token?: string): HeadersInit {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

// Optional convenience wrapper that reads current state (keeps usage explicit)
export function getAuthHeaders(): HeadersInit {
  const s = useAuthStore.getState();
  // Option A: Always attach headers when a token exists to avoid races during refresh
  return s.token ? makeAuthHeaders(s.token) : {};
}

// ----------------------------
// useAuth hook
// ----------------------------
export function useAuth() {
  const navigate = useNavigate();
  const location = useLocation();

  // Subscribe to stable store fields only
  const token = useAuthStore((s) => s.token);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const user = useAuthStore((s) => s.user);
  // FIXED: Subscribe to stable string instead of time-based value
  const tokenExpiresAt = useAuthStore((s) => s.tokenExpiresAt);

  const refreshTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // refresh dedupe state (keeps UI reactive)
  const isRefreshingRef = useRef(false);
  const refreshPromiseRef = useRef<Promise<void> | null>(null);
  const [, setRefreshingState] = useState<boolean>(false); // used to trigger rerenders when dedupe toggles

  // keep latest performRefresh for focus listener
  const performRefreshLatestRef = useRef<() => Promise<void>>(async () => { });

  // ----------------------------
  // Mutations - Called at top level (Rules of Hooks)
  // ----------------------------
  const loginMutation = useApiPost<LoginResponse, LoginRequest>('/Auth/login', {
    showSuccessToast: false,
    showErrorToast: true,
  });

  const logoutMutation = useApiPost<void, void>('/Auth/logout', {
    showSuccessToast: false,
    showErrorToast: false,
    onSettled: () => {
      useAuthStore.getState().clearAuth();
    },
  });

  const refreshTokenMutation = useApiPost<RefreshResponse, { refreshToken: string }>(
    '/Auth/refresh',
    { showSuccessToast: false, showErrorToast: false }
  );

  // ----------------------------
  // performRefresh with dedup + freshest user merging
  // ----------------------------
  const performRefresh = useCallback(async (): Promise<void> => {
    // dedupe
    if (isRefreshingRef.current && refreshPromiseRef.current) return refreshPromiseRef.current;

    const state = useAuthStore.getState();
    const currentRefreshToken = state.refreshToken;
    if (!currentRefreshToken) {
      state.clearAuth();
      navigate('/login', { replace: true, state: { from: location.pathname, reason: 'No refresh token' } });
      return;
    }

    isRefreshingRef.current = true;
    setRefreshingState(true);

    refreshPromiseRef.current = (async () => {
      try {
        const result = await refreshTokenMutation.mutateAsync({ refreshToken: currentRefreshToken });
        // prefer server-supplied user if present; otherwise read the freshest user from store (which may have changed)
        const postRefreshUser = result.user ?? useAuthStore.getState().user ?? null;
        useAuthStore.getState().setAuth(result.token, result.refreshToken, postRefreshUser, result.expiresAt);
      } catch (err) {
        console.error('Token refresh failed:', err);
        useAuthStore.getState().clearAuth();
        navigate('/login', { replace: true, state: { from: location.pathname, reason: 'Session expired' } });
        throw err;
      } finally {
        isRefreshingRef.current = false;
        refreshPromiseRef.current = null;
        setRefreshingState(false);
      }
    })();

    return refreshPromiseRef.current;
  }, [navigate, location.pathname, refreshTokenMutation]);

  // keep latest reference for focus listener
  performRefreshLatestRef.current = performRefresh;

  // ----------------------------
  // scheduleRefresh: calculates time on-demand
  // ----------------------------
  const clearRefreshTimer = useCallback(() => {
    if (refreshTimerRef.current) {
      clearTimeout(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }
  }, []);

  const scheduleRefresh = useCallback(() => {
    clearRefreshTimer();

    const state = useAuthStore.getState();
    if (!state.token || !state.refreshToken || !state.tokenExpiresAt) return;

    // FIXED: Calculate time on-demand, not as a reactive value
    const expiryTime = new Date(state.tokenExpiresAt).getTime();
    const now = Date.now();
    const timeUntilExpiry = expiryTime - now;

    // If already expired, refresh immediately
    if (timeUntilExpiry <= 0) {
      performRefreshLatestRef.current().catch(() => { });
      return;
    }

    // Calculate when to refresh (5 minutes before expiry)
    const delay = Math.max(0, timeUntilExpiry - TOKEN_REFRESH_BUFFER);

    // If we need to refresh within the buffer period, do it now
    if (delay === 0) {
      performRefreshLatestRef.current().catch(() => { });
      return;
    }

    // Schedule the refresh
    refreshTimerRef.current = setTimeout(() => {
      performRefreshLatestRef.current().catch(() => { });
    }, delay);
  }, [clearRefreshTimer]);

  // FIXED: Effect now depends on stable string tokenExpiresAt, not time-based value
  useEffect(() => {
    // Only schedule if we have valid auth state
    if (!token || !refreshToken || !tokenExpiresAt) return;

    scheduleRefresh();
    return clearRefreshTimer;
  }, [token, refreshToken, tokenExpiresAt, scheduleRefresh, clearRefreshTimer]);

  // ----------------------------
  // focus listener uses ref to call latest performRefresh
  // ----------------------------
  useEffect(() => {
    const onFocus = () => {
      const s = useAuthStore.getState();
      if (s.token && s.isTokenExpired()) {
        performRefreshLatestRef.current().catch(() => { });
      }
    };
    window.addEventListener('focus', onFocus);
    return () => window.removeEventListener('focus', onFocus);
  }, []); // stable because ref is updated every render

  // ----------------------------
  // login wrapper that sets auth internally
  // ----------------------------
  const login = useCallback(
    async (payload: LoginRequest) => {
      const res = await loginMutation.mutateAsync(payload);
      // on success, ensure store updated exactly once
      if (res && res.token) {
        useAuthStore.getState().setAuth(res.token, res.refreshToken, res.user ?? null, res.expiresAt);
      }
      return res;
    },
    [loginMutation]
  );

  // ----------------------------
  // logout that clears timer first (race fix)
  // ----------------------------
  const logout = useCallback(() => {
    clearRefreshTimer();
    logoutMutation.mutate();
  }, [logoutMutation, clearRefreshTimer]);

  // ----------------------------
  // FIXED: Compute isAuthenticated on-demand from stable tokenExpiresAt
  // ----------------------------
  const isAuthenticated = Boolean(
    token &&
    user &&
    tokenExpiresAt &&
    new Date(tokenExpiresAt).getTime() > Date.now()
  );

  // isRefreshing: combine network pending and internal dedupe state (reactive)
  const isRefreshing = refreshTokenMutation.isPending || isRefreshingRef.current;

  const isLoading = loginMutation.isPending || refreshTokenMutation.isPending || logoutMutation.isPending;

  return {
    user,
    token,
    isAuthenticated, // Now computed on-demand, no continuous re-renders!
    isLoading,
    login,
    logout,
    refreshToken: performRefresh,
    updateUser: useAuthStore.getState().updateUser,
    loginError: loginMutation.error?.message,
    isLoggingIn: loginMutation.isPending,
    isLoggingOut: logoutMutation.isPending,
    isRefreshing,
    clearError: loginMutation.reset,
  };
}

// ----------------------------
// typed auth error handler
// ----------------------------
type HttpError = { response?: { status?: number } };
function isHttpError(err: unknown): err is HttpError {
  return typeof err === 'object' && err !== null && 'response' in (err as Record<string, unknown>);
}

export function useAuthErrorHandler() {
  const navigate = useNavigate();
  const location = useLocation();

  return useCallback(
    (error: unknown) => {
      if (isHttpError(error) && error.response?.status === 401) {
        useAuthStore.getState().clearAuth();
        navigate('/login', {
          replace: true,
          state: { from: location.pathname, message: 'Your session has expired. Please log in again.' },
        });
      }
    },
    [navigate, location.pathname]
  );
}

// ----------------------------
// Protected Route Hook
// ----------------------------
export function useRequireAuth(redirectTo: string = '/login') {
  const { isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      navigate(redirectTo, {
        replace: true,
        state: { from: location.pathname }
      });
    }
  }, [isAuthenticated, isLoading, navigate, location.pathname, redirectTo]);

  return { isAuthenticated, isLoading };
}

// ----------------------------
// Role-Based Access Control Hooks
// ----------------------------
export function useHasRole(requiredRole: string): boolean {
  const { user, isAuthenticated } = useAuth();
  return isAuthenticated && user?.role === requiredRole;
}

export function useHasAnyRole(roles: string[]): boolean {
  const { user, isAuthenticated } = useAuth();
  return isAuthenticated && !!user && roles.includes(user.role);
}

// ----------------------------
// Permission-based access control (if needed)
// ----------------------------
export function useAuthorization(resource: string): boolean {
  const { user, isAuthenticated } = useAuth();

  if (!isAuthenticated || !user) return false;

  // Prefer server-provided permissions on the user object; fall back to config mapping
  const userPermissions = (user.permissions && user.permissions.length > 0)
    ? user.permissions
    : (rolePermissions[user.role] || []);

  return userPermissions.includes('all') || userPermissions.includes(resource);
}