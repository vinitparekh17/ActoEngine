"use client";

import { useEffect, useState } from "react";
import { Button } from "../ui/button";
import { Moon, Sun } from "lucide-react";

/**
 * Renders a button that toggles between dark and light theme and persists the user's choice.
 *
 * Initializes theme from localStorage or the system preference, adds/removes the `dark` class on the document root and writes the selected theme to localStorage when changed, and listens for system color-scheme changes to update the theme only when the user has not explicitly chosen one.
 *
 * @returns A JSX element rendering the theme toggle button (shows a sun icon for dark mode and a moon icon for light mode).
 */
export default function ThemeToggle() {
  // Initialize from localStorage or system preference
  const [dark, setDark] = useState(() => {
    const saved = localStorage.getItem('theme');
    if (saved) return saved === 'dark';
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  });

  // Apply theme and save to localStorage
  useEffect(() => {
    const root = document.documentElement;
    if (dark) {
      root.classList.add("dark");
    } else {
      root.classList.remove("dark");
    }
    localStorage.setItem('theme', dark ? 'dark' : 'light');
  }, [dark]);

  // Listen for system preference changes
  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e: MediaQueryListEvent) => {
      // Only update if user hasn't set a preference
      if (!localStorage.getItem('theme')) {
        setDark(e.matches);
      }
    };
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, []);

  return (
    <Button
      variant="ghost"
      size="icon"
      className="rounded-xl"
      aria-label="Toggle theme"
      onClick={() => setDark((d) => !d)}
    >
      {dark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
    </Button>
  );
}