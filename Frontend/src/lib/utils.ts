import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";
import { formatDistanceToNow, format } from "date-fns";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export type DateValue = string | Date | null | undefined;

/**
 * Safely formats a date string or Date object as relative time (e.g., "2 hours ago")
 * Returns a fallback string if the date is invalid
 *
 * @param dateValue - Date string, Date object, or undefined/null
 * @param fallback - Fallback string if date is invalid (default: "recently")
 * @returns Formatted relative time string or fallback
 */
export function formatRelativeTime(
  dateValue: DateValue,
  fallback: string = "recently",
): string {
  if (!dateValue) return fallback;

  const date = typeof dateValue === "string" ? new Date(dateValue) : dateValue;

  // Check if date is valid
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  return formatDistanceToNow(date, { addSuffix: true });
}

/**
 * Formats a timestamp into a compact relative label for dense UI surfaces.
 * Examples: "just now", "3h ago", "Yesterday", "Apr 5"
 */
export function formatCompactRelativeTime(
  dateValue: DateValue,
  fallback: string = "recently",
): string {
  if (!dateValue) return fallback;

  const date = typeof dateValue === "string" ? new Date(dateValue) : dateValue;

  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  const diffMs = Date.now() - date.getTime();

  if (diffMs < 0) {
    return format(date, "MMM d, yyyy");
  }

  const diffMinutes = Math.floor(diffMs / 60000);
  if (diffMinutes < 1) return "just now";
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 2) return "Yesterday";
  if (diffDays < 7) return `${diffDays}d ago`;
  if (diffDays < 30) return `${Math.floor(diffDays / 7)}w ago`;

  const currentYear = new Date().getFullYear();
  return format(
    date,
    date.getFullYear() === currentYear ? "MMM d" : "MMM d, yyyy",
  );
}

/**
 * Safely formats a date string or Date object with a custom format
 * Returns a fallback string if the date is invalid
 *
 * @param dateValue - Date string, Date object, or undefined/null
 * @param formatString - date-fns format string (default: "PPP" - e.g., "Apr 29, 2023")
 * @param fallback - Fallback string if date is invalid (default: "N/A")
 * @returns Formatted date string or fallback
 */
export function formatDate(
  dateValue: DateValue,
  formatString: string = "PPP",
  fallback: string = "N/A",
): string {
  if (!dateValue) return fallback;

  const date = typeof dateValue === "string" ? new Date(dateValue) : dateValue;

  // Check if date is valid
  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  return format(date, formatString);
}

/**
 * Safely formats a date using the standard application format (PPpp).
 * E.g., "Apr 29, 2023, 5:00 PM"
 */
export function safeFormatDate(
  dateValue: DateValue,
  fallback: string = "-",
): string {
  return formatDate(dateValue, "PPpp", fallback);
}

/**
 * Converts a UTC date value to a formatted local time string.
 * Use this when displaying timestamps stored as UTC in the database.
 *
 * @param dateValue - UTC date string (e.g. "2026-02-26T15:00:00Z"), Date object, or null/undefined
 * @param formatString - date-fns format string (default: "PPpp" → "Feb 26, 2026, 8:30 PM")
 * @param fallback - Fallback string if date is invalid (default: "-")
 * @returns Formatted local time string or fallback
 */
export function utcToLocal(
  dateValue: DateValue,
  formatString: string = "PPpp",
  fallback: string = "-",
): string {
  if (!dateValue) return fallback;

  // Ensure UTC strings without a trailing "Z" are treated as UTC
  const timezoneSuffix = /([Zz]|[+\-]\d{2}:\d{2})$/;
  const raw =
    typeof dateValue === "string"
      ? (() => {
          const trimmed = dateValue.trim();
          return timezoneSuffix.test(trimmed) ? trimmed : `${trimmed}Z`;
        })()
      : dateValue;

  const date = typeof raw === "string" ? new Date(raw) : raw;

  if (Number.isNaN(date.getTime())) {
    return fallback;
  }

  return format(date, formatString);
}
