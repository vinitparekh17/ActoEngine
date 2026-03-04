import { lazy } from "react";
import { Skeleton } from "@/components/ui/skeletons";

const MonacoEditor = lazy(() => import("@monaco-editor/react"));

const SKELETON_WIDTHS = [65, 82, 45, 78, 56, 90, 40, 70, 85, 50, 75, 60];

export default function SPPreviewPane({
  sqlCode,
  onChange,
  isLoading = false,
}: {
  sqlCode: string;
  onChange: (value: string) => void;
  isLoading?: boolean;
}) {
  return (
    <div className="border border-border/40 rounded-2xl overflow-hidden h-full bg-[#1e1e1e] shadow-inner">
      {isLoading ? (
        <div className="p-8 space-y-4">
          {Array.from({ length: 12 }).map((_, i) => (
            <Skeleton
              key={i}
              className="h-4 opacity-10 bg-primary"
              style={{ width: `${SKELETON_WIDTHS[i % SKELETON_WIDTHS.length]}%` }}
            />
          ))}
        </div>
      ) : (
        <MonacoEditor
          height="100%"
          defaultLanguage="sql"
          theme="vs-dark"
          value={sqlCode}
          onChange={(v) => onChange(v || "")}
          options={{
            minimap: { enabled: false },
            fontSize: 13,
            fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
            wordWrap: "on",
            scrollBeyondLastLine: false,
            padding: { top: 24, bottom: 24 },
            renderLineHighlight: "all",
            lineHeight: 21,
          }}
        />
      )}
    </div>
  );
}
