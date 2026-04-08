export const DEFAULT_BACKEND_ORIGIN = "" // Your backend origin here, e.g., "http://localhost:5000"

export function normalizeBackendOrigin(rawValue?: string | null): string {
  let normalized = rawValue?.trim() ?? "";

  if (!normalized) {
    return DEFAULT_BACKEND_ORIGIN;
  }

  while (normalized.endsWith("/")) {
    normalized = normalized.slice(0, -1);
  }

  if (normalized.toLowerCase().endsWith("/api")) {
    normalized = normalized.slice(0, -4);
  }

  while (normalized.endsWith("/")) {
    normalized = normalized.slice(0, -1);
  }

  return normalized || DEFAULT_BACKEND_ORIGIN;
}

export function resolveBackendOrigin(rawValue?: string | null): string {
  return normalizeBackendOrigin(rawValue);
}

function getRuntimeApiBaseUrl(): string | undefined {
  if (typeof import.meta === "undefined" || !("env" in import.meta)) {
    return undefined;
  }

  return import.meta.env.VITE_API_BASE_URL;
}

export const BACKEND_ORIGIN = resolveBackendOrigin(getRuntimeApiBaseUrl());
export const API_BASE_URL = `${BACKEND_ORIGIN}/api`;

// Warn developers early when the backend origin is not configured, so silent
// misrouting (all API requests going to relative URLs) is immediately visible.
// This only fires in development; production builds never execute this branch.
if (
  typeof import.meta !== "undefined" &&
  "env" in import.meta &&
  import.meta.env.MODE === "development" &&
  !BACKEND_ORIGIN
) {
  console.warn(
    "[backend.ts] VITE_API_BASE_URL is not set — API requests will use relative URLs (e.g. /api/...).\n" +
      "This works only when Vite is configured with a proxy. " +
      "Set VITE_API_BASE_URL=http://localhost:<port> in your .env.local to route directly to the backend."
  );
}

export function buildApiUrl(path: string): string {
  const trimmedPath = path.trim();

  if (!trimmedPath || trimmedPath === "/") {
    return API_BASE_URL;
  }

  if (/^https?:\/\//i.test(trimmedPath)) {
    return trimmedPath;
  }

  const normalizedPath = trimmedPath.startsWith("/")
    ? trimmedPath
    : `/${trimmedPath}`;

  if (normalizedPath === "/api" || normalizedPath.startsWith("/api/")) {
    return `${BACKEND_ORIGIN}${normalizedPath}`;
  }

  return `${API_BASE_URL}${normalizedPath}`;
}
