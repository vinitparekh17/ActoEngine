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
  useSSE?: boolean; // Whether to use SSE for real-time updates
  onStatusChange?: (data: SyncStatusData) => void;
  onComplete?: (data: SyncStatusData) => void;
  onError?: (error: string) => void;
}

/**
 * Hook to monitor sync status:
 * - Uses SSE for real-time updates during active sync
 * - Fetches once via REST when SSE closes or if SSE is unavailable
 * - No continuous polling (saves resources when no sync is active)
 *
 * @param projectId - The project ID to monitor
 * @param options - Configuration options
 * @returns Sync status data and connection state
 */
export function useSyncStatus(
  projectId: number | undefined,
  options: UseSyncStatusOptions = {},
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

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(
    null,
  );
  const reconnectAttemptsRef = useRef<number>(0);
  const mountedRef = useRef<boolean>(true);
  const maxReconnectAttempts = 5;

  const apiUrl = import.meta.env.VITE_API_BASE_URL || "http://localhost:5093";

  /**
   * Fetch sync status via REST API (one-time fetch, no polling)
   * Uses authenticated API client
   */
  const fetchSyncStatus = useCallback(async () => {
    if (!projectId) return;

    try {
      console.log(`[REST] Fetching sync status for project ${projectId}`);

      // Use authenticated API client
      const result = await api.get<SyncStatusResponse>(
        `/projects/${projectId}/sync-status`,
      );

      const data: SyncStatusData = {
        projectId: result.projectId,
        status: result.status,
        progress: result.syncProgress,
        lastSyncAttempt: result.lastSyncAttempt
          ? new Date(result.lastSyncAttempt)
          : undefined,
      };

      // Check if component is still mounted before updating state
      if (!mountedRef.current) return;

      setStatus(data.status);
      setProgress(data.progress);
      setLastSyncAttempt(data.lastSyncAttempt);
      setError(null);

      onStatusChange?.(data);

      // Check if sync is complete
      if (data.status === "Completed" || data.status?.startsWith("Failed")) {
        onComplete?.(data);
      }

      return data;
    } catch (err) {
      const errorMsg =
        err instanceof Error ? err.message : "Failed to fetch sync status";
      console.error("[REST] Fetch error:", errorMsg);
      setError(errorMsg);
      onError?.(errorMsg);
    }
  }, [projectId, onStatusChange, onComplete, onError]);

  /**
   * Connect to SSE stream for real-time updates
   * Includes authentication token as query parameter (EventSource doesn't support custom headers)
   */
  const connectSSE = useCallback(() => {
    if (!projectId || !enabled || !useSSE) return;

    // Close existing connection
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
    }

    // Get auth token for SSE connection
    const token = useAuthStore.getState().token;
    const tokenParam = token ? `?token=${encodeURIComponent(token)}` : "";
    const sseUrl = `${apiUrl}/api/projects/${projectId}/sync-status/stream${tokenParam}`;

    try {
      const eventSource = new EventSource(sseUrl, { withCredentials: true });
      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        setIsConnected(true);
        setError(null);
        reconnectAttemptsRef.current = 0;
        console.log(
          `[SSE] Connected to sync status stream for project ${projectId}`,
        );
      };

      eventSource.onmessage = (event) => {
        try {
          const data: SyncStatusData = JSON.parse(event.data);

          // Handle error messages
          if ("error" in data) {
            const errorMessage = (data as any).message || "Stream error";
            setError(errorMessage);
            onError?.(errorMessage);
            return;
          }

          // Update state
          setStatus(data.status);
          setProgress(data.progress);
          if (data.lastSyncAttempt) {
            setLastSyncAttempt(new Date(data.lastSyncAttempt));
          }

          console.log(
            `[SSE] Status update: ${data.status} (${data.progress}%)`,
          );

          // Call callbacks
          onStatusChange?.(data);

          // Check if sync is complete
          if (
            data.status === "Completed" ||
            data.status?.startsWith("Failed")
          ) {
            console.log(`[SSE] Sync finished: ${data.status} at ${data.progress}%`);

            // Close SSE connection first
            eventSource.close();
            setIsConnected(false);

            // Fetch final status via REST to ensure we have the absolute latest
            // This is critical because there might be a race between the SSE message
            // and the final database update
            setTimeout(async () => {
              console.log("[SSE] Fetching final status via REST after completion");
              await fetchSyncStatus();
              // Call onComplete after we've fetched the final status
              onComplete?.(data);
            }, 500); // Small delay to ensure DB has been updated
          }
        } catch (err) {
          console.error("[SSE] Failed to parse message:", err);
          setError("Failed to parse sync status");
        }
      };

      eventSource.onerror = (err) => {
        console.error("[SSE] Connection error:", err);
        setIsConnected(false);

        // Close the connection
        eventSource.close();

        // Attempt reconnection with exponential backoff
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          const delay = Math.min(
            1000 * Math.pow(2, reconnectAttemptsRef.current),
            30000,
          );
          reconnectAttemptsRef.current++;

          console.log(
            `[SSE] Reconnecting in ${delay}ms (attempt ${reconnectAttemptsRef.current}/${maxReconnectAttempts})`,
          );

          reconnectTimeoutRef.current = setTimeout(() => {
            connectSSE();
          }, delay);
        } else {
          const errorMsg = "SSE connection failed. Fetching status via REST.";
          console.warn(errorMsg);
          setError(null); // Don't show error to user
          onError?.(errorMsg);

          // Fetch once via REST to get current status
          fetchSyncStatus();
        }
      };
    } catch (err) {
      console.error("[SSE] Failed to create EventSource:", err);
      setError("Failed to establish SSE connection");
      setIsConnected(false);
      // Fetch once via REST as fallback
      fetchSyncStatus();
    }
  }, [
    projectId,
    enabled,
    useSSE,
    apiUrl,
    onStatusChange,
    onComplete,
    onError,
    fetchSyncStatus,
  ]);

  /**
   * Manual reconnect (for SSE) or refresh (for REST)
   */
  const reconnect = useCallback(() => {
    reconnectAttemptsRef.current = 0;
    setError(null);
    if (useSSE) {
      connectSSE();
    } else {
      fetchSyncStatus();
    }
  }, [useSSE, connectSSE, fetchSyncStatus]);

  /**
   * Manual refresh - fetch latest status via REST
   */
  const refresh = useCallback(async () => {
    await fetchSyncStatus();
  }, [fetchSyncStatus]);

  useEffect(() => {
    mountedRef.current = true;

    if (!enabled || !projectId) return;

    // Use SSE for real-time updates if enabled, otherwise fetch once via REST
    if (useSSE) {
      connectSSE();
    } else {
      fetchSyncStatus();
    }

    return () => {
      // Cleanup on unmount
      mountedRef.current = false;
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        eventSourceRef.current = null;
      }
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
    };
  }, [enabled, projectId, useSSE, connectSSE, fetchSyncStatus]);

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
