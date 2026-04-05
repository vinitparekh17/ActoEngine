import { ReactNode } from "react";
import { Button } from "../ui/button";
import { Badge } from "../ui/badge";
import { Check, Copy, Maximize2, Minimize2 } from "lucide-react";
import { cn } from "@/lib/utils";

// ─── Language accent colors ───────────────────────────────────────────────────
export function langBadgeClass(lang: string): string {
  const l = lang.toLowerCase();
  if (l.includes("sql")) return "text-blue-600 bg-blue-50 dark:bg-blue-500/10 dark:text-blue-400 border-blue-200 dark:border-blue-500/20";
  if (l.includes("json") || l.includes("xml")) return "text-emerald-600 bg-emerald-50 dark:bg-emerald-500/10 dark:text-emerald-400 border-emerald-200 dark:border-emerald-500/20";
  if (l.includes("js") || l.includes("javascript")) return "text-yellow-600 bg-yellow-50 dark:bg-yellow-500/10 dark:text-yellow-400 border-yellow-200 dark:border-yellow-500/20";
  if (l.includes("ts") || l.includes("typescript")) return "text-blue-600 bg-blue-50 dark:bg-blue-500/10 dark:text-blue-400 border-blue-200 dark:border-blue-500/20";
  if (l.includes("c#") || l.includes("csharp")) return "text-purple-600 bg-purple-50 dark:bg-purple-500/10 dark:text-purple-400 border-purple-200 dark:border-purple-500/20";
  if (l.includes("python")) return "text-green-600 bg-green-50 dark:bg-green-500/10 dark:text-green-400 border-green-200 dark:border-green-500/20";
  if (l.includes("html") || l.includes("css")) return "text-orange-600 bg-orange-50 dark:bg-orange-500/10 dark:text-orange-400 border-orange-200 dark:border-orange-500/20";
  return "text-muted-foreground bg-muted border-border";
}

export interface CodeWindowFrameProps {
  language: string;
  monacoLang: string;
  onCopy: () => void;
  onToggleFullscreen?: () => void;
  isFullscreen?: boolean;
  copied: boolean;
  children: ReactNode;
  /** If provided, overrides the default centered language badge */
  titleBarCenter?: ReactNode;
  className?: string;
}

export function CodeWindowFrame({
  language,
  monacoLang,
  onCopy,
  onToggleFullscreen,
  isFullscreen,
  copied,
  children,
  titleBarCenter,
  className,
}: CodeWindowFrameProps) {
  return (
    <div
      className={cn(
        "overflow-hidden flex flex-col w-full",
        isFullscreen ? "rounded-none border-0 h-full" : "rounded-xl border border-[#3c3c3c] shadow-xl",
        className
      )}
      style={{ background: "#1e1e1e" }}
    >
      {/* Title bar */}
      <div
        className="dark flex items-center justify-between px-4 shrink-0 relative"
        style={{ background: "#2d2d2d", borderBottom: "1px solid #3c3c3c", minHeight: 40 }}
      >
        {/* Traffic lights */}
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full inline-block" style={{ background: "#ff5f57" }} title="Close" />
          <span className="w-3 h-3 rounded-full inline-block" style={{ background: "#febc2e" }} title="Minimize" />
          <span className="w-3 h-3 rounded-full inline-block" style={{ background: "#28c840" }} title="Maximize" />
        </div>

        {/* Centered content */}
        {titleBarCenter ? (
          <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-2">
            {titleBarCenter}
          </div>
        ) : (
          <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-1.5 pointer-events-none">
            <Badge
              variant="outline"
              className={cn("text-[10px] font-mono font-bold uppercase tracking-wider px-2 py-0.5 rounded border", langBadgeClass(language))}
            >
              {language}
            </Badge>
            <span className="text-[11px] text-[#808080] font-mono">{monacoLang}</span>
          </div>
        )}

        {/* Right controls */}
        <div className="flex items-center gap-1">
          <Button
            type="button"
            size="sm"
            variant="ghost"
            className="h-7 text-xs text-[#d4d4d4] hover:text-white hover:bg-[#404040] px-2"
            onClick={onCopy}
          >
            {copied ? <Check className="h-3.5 w-3.5 text-emerald-400 mr-1.5" /> : <Copy className="h-3.5 w-3.5 mr-1.5" />}
            {copied ? "Copied!" : "Copy"}
          </Button>
          {onToggleFullscreen && (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              className="h-7 w-7 p-0 text-[#808080] hover:text-white hover:bg-[#404040]"
              onClick={onToggleFullscreen}
              aria-label={isFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
              title={isFullscreen ? "Exit fullscreen" : "Fullscreen"}
            >
              {isFullscreen ? <Minimize2 className="h-3.5 w-3.5" /> : <Maximize2 className="h-3.5 w-3.5" />}
            </Button>
          )}
        </div>
      </div>

      {/* Editor area */}
      <div className="flex-1 w-full min-h-0 h-0 relative">
        {children}
      </div>
    </div>
  );
}
