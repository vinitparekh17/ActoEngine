import { useEffect, useState } from "react";
import { Button } from "../ui/button";
import { Moon, Sun } from "lucide-react";

export default function ThemeToggle() {
  const [dark, setDark] = useState<boolean | null>(null);
  const [mounted, setMounted] = useState(false);

  // Initialize theme from localStorage or system preference
  useEffect(() => {
    setMounted(true);

    try {
      if (globalThis.window === undefined) return;

      const savedTheme = localStorage.getItem("theme");
      if (savedTheme) {
        setDark(savedTheme === "dark");
      } else {
        const prefersDark = globalThis.window.matchMedia("(prefers-color-scheme: dark)").matches;
        setDark(prefersDark);
      }
    } catch (error) {
      console.error("Failed to load theme preference:", error);
      setDark(false); // Fallback to light mode
    }
  }, []);

  // Apply theme to DOM
  useEffect(() => {
    if (dark === null) return; // Don't apply until initialized

    const root = document.documentElement;
    if (dark) {
      root.classList.add("dark");
    } else {
      root.classList.remove("dark");
    }
  }, [dark]);

  // Don't render until mounted to avoid SSR mismatch
  if (!mounted || dark === null) {
    return (
      <Button
        variant="ghost"
        size="icon"
        className="rounded-xl"
        aria-label="Toggle theme"
        disabled
      >
        <Moon className="h-5 w-5" />
      </Button>
    );
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      className="rounded-xl"
      aria-label="Toggle theme"
      onClick={() =>
        setDark((prev) => {
          const next = !prev;
          try {
            localStorage.setItem("theme", next ? "dark" : "light");
          } catch (error) {
            console.error("Failed to save theme preference:", error);
          }
          return next;
        })
      }
    >
      {dark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
    </Button>
  );
}
