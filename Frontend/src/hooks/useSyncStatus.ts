import { useEffect, useState, useRef, useCallback } from "react";
import { api } from "@/lib/api";
import { useAuthStore } from "@/hooks/useAuth";
import type { SyncStatusResponse } from "@/types/project";

export interface SyncStatusData {
  projectId: number;
  status: string;
  progress: number;
  lastSyncAttempt?: Date;
  timestamp?: Date;
}

export interface UseSyncStatusReturn {
  status: string | null;
  progress: number;
  lastSyncAttempt?: Date;
  isConnected: boolean;
  error: string | null;
  reconnect: () => void;
  refresh: () => Promise<void>;
}

interface UseSyncStatusOptions {
  enabled?: boolean;
  useSSE?: boolean;
  onStatusChange?: (data: SyncStatusData) => void;
  onComplete?: (data: SyncStatusData) => void;
  onError?: (error: string) => void;
}

// Singleton SSE connection manager
interface SseConnection {
  eventSource: EventSource;
  subscribers: Set<(data: SyncStatusData | { error: string; message: string }) => void>;
  currentStatus: SyncStatusData | null;
}

const sseConnections = new Map<number, SseConnection>();

/**
 * Checks if a status is terminal (no more updates expected)
 */
function isTerminalStatus(status: string | null | undefined): boolean {
  if (!status || status === "never") return true;
  return status === "Completed" || status.startsWith("Failed");
}

function normalizeApiBaseUrl(rawUrl: string): string {
  let normalized = rawUrl;

  if (!normalized) {
    return normalized;
  }

  while (normalized.endsWith("/")) {
    normalized = normalized.slice(0, -1);
  }

  if (normalized.endsWith("/api")) {
    normalized = normalized.slice(0, -4);
  }

  return normalized;
}

/**
 * Creates or retrieves an existing SSE connection for a project.
 */
function getOrCreateSseConnection(
  projectId: number,
  apiUrl: string,
  token: string | null,
  onError: (error: string) => void
): SseConnection {
  if (sseConnections.has(projectId)) {
    return sseConnections.get(projectId)!;
  }

  const tokenParam = token ? `?token=${encodeURIComponent(token)}` : "";
  const baseUrl = normalizeApiBaseUrl(apiUrl);
  const sseUrl = `${baseUrl}/api/projects/${projectId}/sync-status/stream${tokenParam}`;

  console.log(`[SSE] Creating new connection for project ${projectId}`);
  const eventSource = new EventSource(sseUrl, { withCredentials: true });
  const subscribers = new Set<(data: SyncStatusData | { error: string; message: string }) => void>();

  const connection: SseConnection = {
    eventSource,
    subscribers,
    currentStatus: null,
  };

  eventSource.onopen = () => {
    console.log(`[SSE] Connected to sync status stream for project ${projectId}`);
  };

  eventSource.onmessage = (event) => {
    try {
      const data = JSON.parse(event.data);

      if ("error" in data) {
        console.log(`[SSE] Error from server: ${data.message}`);
        subscribers.forEach((callback) => {
          callback(data);
        });
        eventSource.close();
        sseConnections.delete(projectId);
        return;
      }

      const statusData: SyncStatusData = {
        projectId: data.projectId,
        status: data.status,
        progress: data.progress,
        lastSyncAttempt: data.lastSyncAttempt ? new Date(data.lastSyncAttempt) : undefined,
        timestamp: data.timestamp ? new Date(data.timestamp) : undefined,
      };

      connection.currentStatus = statusData;
      subscribers.forEach((callback) => {
        callback(statusData);
      });

      console.log(`[SSE] Status update: ${data.status} (${data.progress}%)`);

      if (isTerminalStatus(data.status)) {
        console.log(`[SSE] Sync finished: ${data.status}, closing connection`);
        eventSource.close();
        sseConnections.delete(projectId);
      }
    } catch (err) {
      console.error("[SSE] Failed to parse message:", err);
      onError("Failed to parse sync status");
    }
  };

  eventSource.onerror = () => {
    console.error("[SSE] Connection error");
    eventSource.close();
    sseConnections.delete(projectId);
    const errorData = { error: "Connection error", message: "SSE connection lost" };
    subscribers.forEach((callback) => {
      callback(errorData);
    });
  };

  sseConnections.set(projectId, connection);
  return connection;
}

function subscribeToSse(
  projectId: number,
  apiUrl: string,
  token: string | null,
  callback: (data: SyncStatusData | { error: string; message: string }) => void,
  onError: (error: string) => void
): () => void {
  const connection = getOrCreateSseConnection(projectId, apiUrl, token, onError);
  connection.subscribers.add(callback);

  if (connection.currentStatus) {
    callback(connection.currentStatus);
  }

  return () => {
    connection.subscribers.delete(callback);
    if (connection.subscribers.size === 0) {
      console.log(`[SSE] No subscribers for project ${projectId}, closing`);
      connection.eventSource.close();
      sseConnections.delete(projectId);
    }
  };
}

/**
 * Hook to monitor sync status:
 * - Fetches status via REST once on mount
 * - Only connects to SSE if sync is actively running (not terminal)
 * - Uses singleton SSE connection per project
 */
export function useSyncStatus(
  projectId: number | undefined,
  options: UseSyncStatusOptions = {}
): UseSyncStatusReturn {
  const {
    enabled = true,
    useSSE = true,
    onStatusChange,
    onComplete,
    onError,
  } = options;

  const [status, setStatus] = useState<string | null>(null);
  const [progress, setProgress] = useState<number>(0);
  const [lastSyncAttempt, setLastSyncAttempt] = useState<Date | undefined>();
  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Store callbacks in refs to avoid dependency issues
  const onStatusChangeRef = useRef(onStatusChange);
  const onCompleteRef = useRef(onComplete);
  const onErrorRef = useRef(onError);

  // Update refs when callbacks change
  useEffect(() => {
    onStatusChangeRef.current = onStatusChange;
    onCompleteRef.current = onComplete;
    onErrorRef.current = onError;
  }, [onStatusChange, onComplete, onError]);

  const mountedRef = useRef<boolean>(true);
  const unsubscribeRef = useRef<(() => void) | null>(null);
  const hasFetchedRef = useRef<boolean>(false);
  const apiUrl = normalizeApiBaseUrl(
    import.meta.env.VITE_API_BASE_URL || "http://localhost:5093"
  );

  /**
   * Fetch sync status via REST API (one-time)
   */
  const fetchSyncStatus = useCallback(async (): Promise<SyncStatusData | null> => {
    if (!projectId) return null;

    try {
      console.log(`[REST] Fetching sync status for project ${projectId}`);

      const result = await api.get<SyncStatusResponse>(
        `/projects/${projectId}/sync-status`
      );

      const data: SyncStatusData = {
        projectId: result.projectId,
        status: result.status,
        progress: result.syncProgress,
        lastSyncAttempt: result.lastSyncAttempt
          ? new Date(result.lastSyncAttempt)
          : undefined,
      };

      if (!mountedRef.current) return null;

      setStatus(data.status);
      setProgress(data.progress);
      setLastSyncAttempt(data.lastSyncAttempt);
      setError(null);

      onStatusChangeRef.current?.(data);

      if (isTerminalStatus(data.status)) {
        console.log(`[REST] Status is terminal: ${data.status}`);
        onCompleteRef.current?.(data);
      }

      return data;
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to fetch sync status";
      console.error("[REST] Fetch error:", errorMsg);
      if (mountedRef.current) {
        setError(errorMsg);
        onErrorRef.current?.(errorMsg);
      }
      return null;
    }
  }, [projectId]);

  const reconnect = useCallback(() => {
    setError(null);
    hasFetchedRef.current = false;
    fetchSyncStatus();
  }, [fetchSyncStatus]);

  const refresh = useCallback(async () => {
    await fetchSyncStatus();
  }, [fetchSyncStatus]);

  // Reset when projectId changes
  useEffect(() => {
    hasFetchedRef.current = false;
    setStatus(null);
    setProgress(0);
    setLastSyncAttempt(undefined);
    setIsConnected(false);
    setError(null);
  }, [projectId]);

  // Main effect - runs once per projectId
  useEffect(() => {
    mountedRef.current = true;

    if (!enabled || !projectId) return;

    // Prevent double fetch
    if (hasFetchedRef.current) return;
    hasFetchedRef.current = true;

    const cleanup = () => {
      if (unsubscribeRef.current) {
        unsubscribeRef.current();
        unsubscribeRef.current = null;
      }
    };

    const initializeStatus = async () => {
      const currentStatus = await fetchSyncStatus();

      if (!mountedRef.current) return;

      // Only connect SSE if status is non-terminal
      if (useSSE && currentStatus && !isTerminalStatus(currentStatus.status)) {
        console.log(`[SSE] Status is ${currentStatus.status}, subscribing`);
        const token = useAuthStore.getState().token;

        unsubscribeRef.current = subscribeToSse(
          projectId,
          apiUrl,
          token,
          (data) => {
            if (!mountedRef.current) return;

            if ("error" in data) {
              setError(data.message || "Stream error");
              setIsConnected(false);
              onErrorRef.current?.(data.message);
              return;
            }

            setStatus(data.status);
            setProgress(data.progress);
            if (data.lastSyncAttempt) {
              setLastSyncAttempt(new Date(data.lastSyncAttempt));
            }
            setIsConnected(true);
            setError(null);

            onStatusChangeRef.current?.(data);

            if (isTerminalStatus(data.status)) {
              onCompleteRef.current?.(data);
              setIsConnected(false);
            }
          },
          (errorMsg) => {
            if (!mountedRef.current) return;
            setError(errorMsg);
            onErrorRef.current?.(errorMsg);
          }
        );
      } else if (currentStatus && isTerminalStatus(currentStatus.status)) {
        console.log(`[SSE] Status is terminal (${currentStatus.status}), not connecting`);
      }
    };

    initializeStatus();

    return () => {
      mountedRef.current = false;
      cleanup();
    };
  }, [enabled, projectId, useSSE, apiUrl, fetchSyncStatus]);

  return {
    status,
    progress,
    lastSyncAttempt,
    isConnected,
    error,
    reconnect,
    refresh,
  };
}
