import { useEffect, useState, useRef, useCallback } from 'react';

export interface SyncStatusData {
  projectId: number;
  status: string;
  progress: number;
  lastSyncAttempt?: Date;
  timestamp: Date;
}

export interface UseSyncStatusReturn {
  status: string | null;
  progress: number;
  lastSyncAttempt?: Date;
  isConnected: boolean;
  error: string | null;
  reconnect: () => void;
}

interface UseSyncStatusOptions {
  enabled?: boolean;
  onStatusChange?: (data: SyncStatusData) => void;
  onComplete?: (data: SyncStatusData) => void;
  onError?: (error: string) => void;
}

/**
 * Hook to connect to SSE endpoint and get real-time sync status updates
 * @param projectId - The project ID to monitor
 * @param options - Configuration options
 * @returns Sync status data and connection state
 */
export function useSyncStatus(
  projectId: number | undefined,
  options: UseSyncStatusOptions = {}
): UseSyncStatusReturn {
  const { enabled = true, onStatusChange, onComplete, onError } = options;

  const [status, setStatus] = useState<string | null>(null);
  const [progress, setProgress] = useState<number>(0);
  const [lastSyncAttempt, setLastSyncAttempt] = useState<Date | undefined>();
  const [isConnected, setIsConnected] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const reconnectAttemptsRef = useRef<number>(0);
  const maxReconnectAttempts = 5;

  const connect = useCallback(() => {
    if (!projectId || !enabled) return;

    // Close existing connection
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
    }

    const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000';
    const sseUrl = `${apiUrl}/api/SyncStatus/stream/${projectId}`;

    try {
      const eventSource = new EventSource(sseUrl);
      eventSourceRef.current = eventSource;

      eventSource.onopen = () => {
        setIsConnected(true);
        setError(null);
        reconnectAttemptsRef.current = 0;
        console.log(`[SSE] Connected to sync status stream for project ${projectId}`);
      };

      eventSource.onmessage = (event) => {
        try {
          const data: SyncStatusData = JSON.parse(event.data);

          // Handle error messages
          if ('error' in data) {
            const errorMessage = (data as any).message || 'Stream error';
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

          console.log(`[SSE] Status update: ${data.status} (${data.progress}%)`);

          // Call callbacks
          onStatusChange?.(data);

          // Check if sync is complete
          if (data.status === 'Completed' || data.status?.startsWith('Failed')) {
            console.log(`[SSE] Sync finished: ${data.status}`);
            onComplete?.(data);

            // Close connection after completion
            eventSource.close();
            setIsConnected(false);
          }
        } catch (err) {
          console.error('[SSE] Failed to parse message:', err);
          setError('Failed to parse sync status');
        }
      };

      eventSource.onerror = (err) => {
        console.error('[SSE] Connection error:', err);
        setIsConnected(false);

        // Close the connection
        eventSource.close();

        // Attempt reconnection with exponential backoff
        if (reconnectAttemptsRef.current < maxReconnectAttempts) {
          const delay = Math.min(1000 * Math.pow(2, reconnectAttemptsRef.current), 30000);
          reconnectAttemptsRef.current++;

          console.log(
            `[SSE] Reconnecting in ${delay}ms (attempt ${reconnectAttemptsRef.current}/${maxReconnectAttempts})`
          );

          reconnectTimeoutRef.current = setTimeout(() => {
            connect();
          }, delay);
        } else {
          const errorMsg = 'Failed to connect to sync status stream';
          setError(errorMsg);
          onError?.(errorMsg);
        }
      };
    } catch (err) {
      console.error('[SSE] Failed to create EventSource:', err);
      setError('Failed to establish connection');
      setIsConnected(false);
    }
  }, [projectId, enabled, onStatusChange, onComplete, onError]);

  const reconnect = useCallback(() => {
    reconnectAttemptsRef.current = 0;
    setError(null);
    connect();
  }, [connect]);

  useEffect(() => {
    connect();

    return () => {
      // Cleanup on unmount
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        eventSourceRef.current = null;
      }
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
    };
  }, [connect]);

  return {
    status,
    progress,
    lastSyncAttempt,
    isConnected,
    error,
    reconnect,
  };
}
