import { useState, useCallback, useRef, useEffect } from 'react';

export interface ErrorRecoveryState {
  error: Error | null;
  retryCount: number;
  isRetrying: boolean;
  shouldShowRetryUI: boolean;
}

export interface UseErrorRecoveryOptions {
  maxAutoRetries?: number;
  retryDelay?: number;
  onRetry?: () => void;
  onError?: (error: Error) => void;
}

/**
 * Hook for smart hybrid error recovery with automatic retry logic
 * - Retries once automatically and silently
 * - Shows manual retry UI if auto-retry fails
 *
 * @param options Configuration options for retry behavior
 * @returns Error recovery state and control functions
 */
export function useErrorRecovery(options: UseErrorRecoveryOptions = {}) {
  const {
    maxAutoRetries = 1,
    retryDelay = 1000,
    onRetry,
    onError,
  } = options;

  const [state, setState] = useState<ErrorRecoveryState>({
    error: null,
    retryCount: 0,
    isRetrying: false,
    shouldShowRetryUI: false,
  });

  const retryTimeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  /**
   * Handle error and trigger smart retry logic
   * Fixed: Side effects extracted outside setState updater (React purity requirement)
   */
  const handleError = useCallback((error: Error) => {
    // Clear any existing retry timeout to avoid duplicates
    if (retryTimeoutRef.current) {
      clearTimeout(retryTimeoutRef.current);
      retryTimeoutRef.current = undefined;
    }

    // Compute new state purely (no side effects)
    let shouldAutoRetry = false;
    setState((prev) => {
      const newRetryCount = prev.retryCount + 1;
      shouldAutoRetry = newRetryCount <= maxAutoRetries;

      // If we should auto-retry, don't show UI yet
      if (shouldAutoRetry) {
        return {
          error,
          retryCount: newRetryCount,
          isRetrying: false,
          shouldShowRetryUI: false,
        };
      }

      // Max auto-retries exceeded, show manual retry UI
      return {
        error,
        retryCount: newRetryCount,
        isRetrying: false,
        shouldShowRetryUI: true,
      };
    });

    // Side effects executed AFTER setState (not inside updater)
    onError?.(error);

    if (shouldAutoRetry) {
      // Schedule automatic retry
      retryTimeoutRef.current = setTimeout(() => {
        setState((current) => ({
          ...current,
          isRetrying: true,
        }));
        onRetry?.();
      }, retryDelay);
    }
  }, [maxAutoRetries, retryDelay, onRetry, onError]);

  /**
   * Manual retry triggered by user
   * Fixed: Clear pending auto-retry timeout before manual retry
   */
  const retry = useCallback(() => {
    // Clear any pending auto-retry timeout
    if (retryTimeoutRef.current) {
      clearTimeout(retryTimeoutRef.current);
      retryTimeoutRef.current = undefined;
    }

    setState((prev) => ({
      ...prev,
      isRetrying: true,
      shouldShowRetryUI: false,
    }));
    onRetry?.();
  }, [onRetry]);

  /**
   * Reset error state
   * Fixed: Clear ref after clearTimeout for consistency
   */
  const reset = useCallback(() => {
    if (retryTimeoutRef.current) {
      clearTimeout(retryTimeoutRef.current);
      retryTimeoutRef.current = undefined;
    }
    setState({
      error: null,
      retryCount: 0,
      isRetrying: false,
      shouldShowRetryUI: false,
    });
  }, []);

  /**
   * Mark retry as complete (success or failure)
   * Fixed: Handle case where completeRetry(false) is called without error
   */
  const completeRetry = useCallback((success: boolean, error?: Error) => {
    if (success) {
      reset();
    } else {
      // Treat failed retry as error (generate generic Error if not provided)
      const retryError = error || new Error('Retry failed');
      handleError(retryError);
    }
  }, [reset, handleError]);

  /**
   * Cleanup on unmount - clear any pending timeouts
   */
  useEffect(() => {
    return () => {
      if (retryTimeoutRef.current) {
        clearTimeout(retryTimeoutRef.current);
      }
    };
  }, []);

  return {
    ...state,
    handleError,
    retry,
    reset,
    completeRetry,
  };
}
