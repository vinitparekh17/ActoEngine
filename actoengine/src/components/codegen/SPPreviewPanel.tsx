"use client"

import { lazy } from "react"
import { Skeleton } from "../ui/skeleton"   
const MonacoEditor = lazy(() => import("@monaco-editor/react"))

export default function SPPreviewPane({
  sqlCode,
  onChange,
  isLoading = false,
}: {
  sqlCode: string
  onChange: (value: string) => void
  isLoading?: boolean
}) {
  return (
  <div className="border rounded-xl overflow-hidden h-full">
      {isLoading ? (
        <div className="p-4 space-y-2">
          {Array.from({ length: 10 }).map((_, i) => (
            <Skeleton key={i} className="h-4 w-full" />
          ))}
        </div>
      ) : (
        <MonacoEditor
          height="100%"
          defaultLanguage="sql"
          value={sqlCode}
          onChange={(v) => onChange(v || "")}
          options={{
            minimap: { enabled: false },
            fontSize: 13,
            wordWrap: "on",
            scrollBeyondLastLine: false,
          }}
        />
      )}
    </div>
  )
}