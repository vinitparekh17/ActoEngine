import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"
import { formatDistanceToNow, format } from "date-fns"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Safely formats a date string or Date object as relative time (e.g., "2 hours ago")
 * Returns a fallback string if the date is invalid
 *
 * @param dateValue - Date string, Date object, or undefined/null
 * @param fallback - Fallback string if date is invalid (default: "recently")
 * @returns Formatted relative time string or fallback
 */
export function formatRelativeTime(
  dateValue: string | Date | null | undefined,
  fallback: string = "recently"
): string {
  if (!dateValue) return fallback;

  const date = typeof dateValue === 'string' ? new Date(dateValue) : dateValue;

  // Check if date is valid
  if (isNaN(date.getTime())) {
    return fallback;
  }

  return formatDistanceToNow(date, { addSuffix: true });
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
  dateValue: string | Date | null | undefined,
  formatString: string = "PPP",
  fallback: string = "N/A"
): string {
  if (!dateValue) return fallback;

  const date = typeof dateValue === 'string' ? new Date(dateValue) : dateValue;

  // Check if date is valid
  if (isNaN(date.getTime())) {
    return fallback;
  }

  return format(date, formatString);
}
