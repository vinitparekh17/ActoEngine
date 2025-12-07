/**
 * Request Queue System
 *
 * Manages failed API requests due to 401 errors and retries them
 * after successful re-authentication without losing user state.
 *
 * Features:
 * - Queues requests that fail due to 401 errors
 * - Retries all queued requests after successful re-authentication
 * - Prevents duplicate requests from being queued
 * - Clears queue on user-initiated cancellation
 */

export interface QueuedRequestOptions {
  method?: string;
  headers?: Record<string, string>;
  data?: unknown;
  params?: unknown;
  signal?: AbortSignal;
}

interface QueuedRequest {
  id: string;
  endpoint: string;
  options: QueuedRequestOptions;
  resolves: Array<(value: any) => void>;
  rejects: Array<(error: any) => void>;
  timestamp: number;
  timedOut?: boolean;
}

class RequestQueue {
  private queue: QueuedRequest[] = [];
  private isProcessing = false;
  private isWaitingForAuth = false;
  private reLoginCallback?: () => Promise<void>;

  /**
   * Set the re-login callback that will be triggered when 401 is detected
   */
  setReLoginCallback(callback: () => Promise<void>) {
    this.reLoginCallback = callback;
  }

  /**
   * Add a request to the queue
   * Returns a promise that resolves when the request is retried successfully
   */
  enqueue(endpoint: string, options: QueuedRequestOptions): Promise<any> {
    return new Promise((resolve, reject) => {
      // Capture timestamp once for consistency
      const now = Date.now();

      // Create predicate function once to avoid duplication
      const isDuplicatePredicate = (req: QueuedRequest) =>
        req.endpoint === endpoint &&
        req.options.method === options.method &&
        now - req.timestamp < 1000;

      // Single find() call to check for duplicate
      const existingRequest = this.queue.find(isDuplicatePredicate);

      if (existingRequest) {
        // Duplicate found - attach handlers to existing request
        console.log(`[RequestQueue] Duplicate request ignored: ${endpoint}`);
        existingRequest.resolves.push(resolve);
        existingRequest.rejects.push(reject);
      } else {
        // No duplicate - create and queue new request
        const id = `${endpoint}-${now}-${Math.random()}`;
        const request: QueuedRequest = {
          id,
          endpoint,
          options,
          resolves: [resolve],
          rejects: [reject],
          timestamp: now,
        };

        this.queue.push(request);
        console.log(`[RequestQueue] Queued request: ${endpoint}`, {
          queueSize: this.queue.length,
        });
      }
    });
  }

  /**
   * Execute a fetch with timeout using AbortController
   * @param fetchFn The fetch function to use
   * @param endpoint The endpoint to fetch
   * @param options Request options
   * @param timeoutMs Timeout in milliseconds (default: 30 seconds)
   */
  private async fetchWithTimeout(
    fetchFn: (endpoint: string, options: QueuedRequestOptions) => Promise<any>,
    endpoint: string,
    options: QueuedRequestOptions,
    timeoutMs: number = 30000,
    request?: QueuedRequest,
  ): Promise<any> {
    // Create merged controller that responds to both timeout and user signals
    const mergedController = new AbortController();
    const timeoutId = setTimeout(() => {
      if (request) {
        request.timedOut = true;
      }
      mergedController.abort();
    }, timeoutMs);

    // Listen to user-provided signal if it exists
    const userSignal = options.signal;
    let userAbortHandler: (() => void) | undefined;

    if (userSignal) {
      // If already aborted, abort merged controller immediately
      if (userSignal.aborted) {
        mergedController.abort();
      } else {
        // Listen for user signal abort
        userAbortHandler = () => mergedController.abort();
        userSignal.addEventListener("abort", userAbortHandler);
      }
    }

    try {
      const result = await fetchFn(endpoint, {
        ...options,
        signal: mergedController.signal,
      });
      return result;
    } finally {
      // Always cleanup timeout and event listeners
      clearTimeout(timeoutId);
      if (userAbortHandler && userSignal) {
        userSignal.removeEventListener("abort", userAbortHandler);
      }
    }
  }

  /**
   * Process all queued requests after successful re-authentication
   * @param fetchFn The fetch function to use for retrying requests
   */
  async processQueue(
    fetchFn: (endpoint: string, options: QueuedRequestOptions) => Promise<any>,
  ) {
    if (this.isProcessing || this.queue.length === 0) {
      return;
    }

    this.isProcessing = true;
    this.isWaitingForAuth = false;

    console.log(
      `[RequestQueue] Processing ${this.queue.length} queued requests`,
    );

    // Process all requests sequentially to avoid race conditions
    const requests = [...this.queue];
    this.queue = [];

    for (const request of requests) {
      try {
        console.log(`[RequestQueue] Retrying: ${request.endpoint}`);
        const result = await this.fetchWithTimeout(
          fetchFn,
          request.endpoint,
          request.options,
          30000, // 30 second timeout
          request, // Pass request to track timeout
        );
        request.resolves.forEach((resolve) => resolve(result));
      } catch (error) {
        if (error instanceof Error && error.name === "AbortError") {
          if (request.timedOut) {
            console.error(
              `[RequestQueue] Timeout after 30s: ${request.endpoint}`,
            );
            request.rejects.forEach((reject) =>
              reject(new Error(`Request timeout: ${request.endpoint}`)),
            );
          } else {
            console.log(
              `[RequestQueue] Request cancelled: ${request.endpoint}`,
            );
            request.rejects.forEach((reject) =>
              reject(new Error(`Request cancelled by user: ${request.endpoint}`)),
            );
          }
        } else {
          console.error(
            `[RequestQueue] Retry failed: ${request.endpoint}`,
            error,
          );
          request.rejects.forEach((reject) => reject(error));
        }
      }
    }

    this.isProcessing = false;
    console.log("[RequestQueue] Queue processing complete");
  }

  /**
   * Clear all queued requests (called on user cancellation or logout)
   */
  clear() {
    console.log(`[RequestQueue] Clearing ${this.queue.length} queued requests`);
    this.queue.forEach((request) => {
      request.rejects.forEach((reject) =>
        reject(new Error("Request cancelled by user")),
      );
    });
    this.queue = [];
    this.isWaitingForAuth = false;
  }

  /**
   * Get the number of queued requests
   */
  get size(): number {
    return this.queue.length;
  }

  /**
   * Check if the queue is waiting for authentication
   */
  get isWaiting(): boolean {
    return this.isWaitingForAuth;
  }

  /**
   * Mark that we're waiting for authentication
   */
  setWaitingForAuth(waiting: boolean) {
    this.isWaitingForAuth = waiting;
  }

  /**
   * Atomic compare-and-set for waiting flag (prevents race conditions)
   * Returns true if the flag was successfully set, false if already waiting
   */
  compareAndSetWaitingForAuth(): boolean {
    if (this.isWaitingForAuth) {
      return false; // Already waiting, don't trigger again
    }
    this.isWaitingForAuth = true;
    return true; // Successfully set, caller should trigger re-login
  }

  /**
   * Trigger re-login flow
   * Fixed: Use atomic compareAndSetWaitingForAuth to prevent TOCTOU race
   */
  async triggerReLogin(): Promise<void> {
    if (!this.reLoginCallback) {
      throw new Error("Re-login callback not set");
    }

    // Atomic check-and-set to claim the re-login flow
    if (!this.compareAndSetWaitingForAuth()) {
      console.log("[RequestQueue] Already waiting for authentication");
      return;
    }

    console.log("[RequestQueue] Triggering re-login flow");

    try {
      await this.reLoginCallback();
    } catch (error) {
      this.clear();
      throw error;
    } finally {
      // Always clear waiting flag in finally block
      this.isWaitingForAuth = false;
    }
  }
}

// Export singleton instance
export const requestQueue = new RequestQueue();
