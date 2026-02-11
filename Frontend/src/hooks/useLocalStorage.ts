import { useState, useEffect } from "react";

/**
 * Hook to persist state in localStorage
 * @param key LocalStorage key
 * @param initialValue Initial value if nothing in storage (should be a stable reference to avoid re-initialization)
 * @returns [storedValue, setValue]
 *
 * Note: Callers should provide stable initialValue references (e.g., constants, useMemo, or useRef)
 * to avoid unnecessary re-initialization when the initialValue reference changes.
 */
export function useLocalStorage<T>(
  key: string,
  initialValue: T,
): [T, (value: T | ((val: T) => T)) => void] {
  // Get from local storage then parse stored json or return initialValue
  const [storedValue, setStoredValue] = useState<T>(() => {
    if (typeof window === "undefined") {
      return initialValue;
    }
    try {
      const item = window.localStorage.getItem(key);
      return item ? JSON.parse(item) : initialValue;
    } catch (error) {
      console.warn(`Error reading localStorage key "${key}":`, error);
      return initialValue;
    }
  });

  // Return a wrapped version of useState's setter function that ...
  // ... persists the new value to localStorage.
  const setValue = (value: T | ((val: T) => T)) => {
    try {
      // Allow value to be a function so we have same API as useState
      const valueToStore =
        value instanceof Function ? value(storedValue) : value;

      // Save state
      setStoredValue(valueToStore);

      // Save to local storage
      if (typeof window !== "undefined") {
        window.localStorage.setItem(key, JSON.stringify(valueToStore));
      }
    } catch (error) {
      console.warn(`Error setting localStorage key "${key}":`, error);
    }
  };

  // Sync with localStorage when key changes
  // Note: initialValue is intentionally NOT in dependencies to avoid infinite loops
  // when non-primitive initialValue references change. The initialValue is already
  // captured in the state initializer above.
  useEffect(() => {
    setStoredValue(() => {
      try {
        const item = window.localStorage.getItem(key);
        return item ? JSON.parse(item) : initialValue;
      } catch {
        return initialValue;
      }
    });
  }, [key]);

  return [storedValue, setValue];
}
