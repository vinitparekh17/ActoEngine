"use client"

import { useEffect, useState } from "react"
import { Button } from "../ui/button"
import { Moon, Sun } from "lucide-react"

export default function ThemeToggle() {
  const [dark, setDark] = useState(false)

  useEffect(() => {
    const root = document.documentElement
    if (dark) {
      root.classList.add("dark")
    } else {
      root.classList.remove("dark")
    }
  }, [dark])

  return (
    <Button
      variant="ghost"
      size="icon"
      className="rounded-xl"
      aria-label="Toggle theme"
      onClick={() =>
        setDark((prev) => {
          const next = !prev;
          localStorage.setItem('theme', next ? 'dark' : 'light');
          return next;
        })
      }
    >
      {dark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
    </Button>
  )
}
