import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { useState } from "react";
import { toast } from "sonner";

// ============================================
// Query Client Configuration
// ============================================
function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Global defaults for all queries
        staleTime: 60 * 1000, // 1 minute
        gcTime: 5 * 60 * 1000, // 5 minutes (formerly cacheTime)
        retry: (failureCount, error: any) => {
          // Don't retry on 401/403
          if (error?.status === 401 || error?.status === 403) {
            return false;
          }
          // Don't retry on 404
          if (error?.status === 404) {
            return false;
          }
          // Retry other errors up to 2 times
          return failureCount < 2;
        },
        refetchOnWindowFocus: false,
        // React Query v5: Use throwOnError instead of useErrorBoundary
        throwOnError: (error: any) => {
          // Throw to error boundary for 401 and 500+ errors
          return error?.status === 401 || error?.status >= 500;
        },
      },
      mutations: {
        // Global defaults for all mutations
        retry: false,
        // Mutations don't throw by default, handle errors manually
        throwOnError: false,
        // Show error toast for mutations by default
        onError: (error: any) => {
          // Skip toast if error will be handled by boundary
          if (error?.status === 401 || error?.status >= 500) {
            return;
          }

          // Show toast for client errors (400-499)
          toast.error(error?.message || "Operation failed");
        },
      },
    },
  });
}

let browserQueryClient: QueryClient | undefined = undefined;

function getQueryClient() {
  if (globalThis.window === undefined) {
    // Server: always make a new query client
    return makeQueryClient();
  } else {
    // Browser: make a new query client if we don't already have one
    browserQueryClient ??= makeQueryClient();
    return browserQueryClient;
  }
}

// ============================================
// Provider Component
// ============================================
interface QueryProviderProps {
  children: React.ReactNode;
}

export function QueryProvider({ children }: Readonly<QueryProviderProps>) {
  const [queryClient] = useState(() => getQueryClient());

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      {/* Only show devtools in development */}
      {import.meta.env.DEV && (
        <ReactQueryDevtools initialIsOpen={false} position="bottom" />
      )}
    </QueryClientProvider>
  );
}

// ============================================
// Export Query Client for imperative use
// ============================================
export { getQueryClient };
