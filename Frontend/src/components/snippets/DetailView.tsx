import { lazy, Suspense, useState } from "react";
import { Button } from "../ui/button";
import { AlertTriangle, Calendar, Check, Copy, Maximize2, Minimize2, User } from "lucide-react";
import { MONACO_LANGUAGE_MAP, SnippetDetail } from "@/types/snippet";
import { Badge } from "../ui/badge";
import { Alert } from "../ui/alert";
import { cn } from "@/lib/utils";

interface DetailViewProps {
  snippet: SnippetDetail;
  currentUserId?: number;
  isFavoriting: boolean;
  isEditorFullscreen?: boolean;
  onToggleFullscreen?: () => void;
  onBack: () => void;
  onEdit: () => void;
  onFavorite: () => void;
  onCopy: () => void;
  onDelete: () => void;
}

const MonacoEditor = lazy(() => import("@monaco-editor/react"));

// ─── Language accent colors ───────────────────────────────────────────────────
function langBadgeClass(lang: string): string {
  const l = lang.toLowerCase();
  if (l.includes("sql")) return "text-blue-600 bg-blue-50 dark:bg-blue-500/10 dark:text-blue-400 border-blue-200 dark:border-blue-500/20";
  if (l.includes("js") || l.includes("javascript")) return "text-yellow-600 bg-yellow-50 dark:bg-yellow-500/10 dark:text-yellow-400 border-yellow-200 dark:border-yellow-500/20";
  if (l.includes("ts") || l.includes("typescript")) return "text-blue-600 bg-blue-50 dark:bg-blue-500/10 dark:text-blue-400 border-blue-200 dark:border-blue-500/20";
  if (l.includes("c#") || l.includes("csharp")) return "text-purple-600 bg-purple-50 dark:bg-purple-500/10 dark:text-purple-400 border-purple-200 dark:border-purple-500/20";
  if (l.includes("python")) return "text-green-600 bg-green-50 dark:bg-green-500/10 dark:text-green-400 border-green-200 dark:border-green-500/20";
  if (l.includes("html") || l.includes("css")) return "text-orange-600 bg-orange-50 dark:bg-orange-500/10 dark:text-orange-400 border-orange-200 dark:border-orange-500/20";
  if (l.includes("json") || l.includes("xml")) return "text-emerald-600 bg-emerald-50 dark:bg-emerald-500/10 dark:text-emerald-400 border-emerald-200 dark:border-emerald-500/20";
  return "text-muted-foreground bg-muted border-border";
}

// ─── Mac Window Frame ─────────────────────────────────────────────────────────
interface MacWindowProps {
  language: string;
  monacoLang: string;
  onCopy: () => void;
  onToggleFullscreen?: () => void;
  isFullscreen?: boolean;
  copied: boolean;
  children: React.ReactNode;
}

function MacWindowFrame({ language, monacoLang, onCopy, onToggleFullscreen, isFullscreen, copied, children }: MacWindowProps) {
  return (
    <div className="rounded-xl overflow-hidden border border-[#3c3c3c] shadow-xl flex flex-col" style={{ background: "#1e1e1e" }}>
      {/* Title bar */}
      <div
        className="flex items-center justify-between px-4 shrink-0"
        style={{ background: "#2d2d2d", borderBottom: "1px solid #3c3c3c", minHeight: 40 }}
      >
        {/* Traffic lights */}
        <div className="flex items-center gap-1.5">
          <span className="w-3 h-3 rounded-full inline-block" style={{ background: "#ff5f57" }} title="Close" />
          <span className="w-3 h-3 rounded-full inline-block" style={{ background: "#febc2e" }} title="Minimize" />
          <span className="w-3 h-3 rounded-full inline-block" style={{ background: "#28c840" }} title="Maximize" />
        </div>

        {/* Centered language badge */}
        <div className="absolute left-1/2 -translate-x-1/2 flex items-center gap-1.5 pointer-events-none">
          <Badge
            variant="outline"
            className={cn(
              "text-[10px] font-mono font-bold uppercase tracking-wider px-2 py-0.5 rounded border",
              langBadgeClass(language)
            )}
          >
            {language}
          </Badge>
          <span className="text-[11px] text-[#808080] font-mono">{monacoLang}</span>
        </div>

        {/* Right controls */}
        <div className="flex items-center gap-1">
          <Button
            size="sm" variant="ghost"
            className="h-7 text-xs text-[#d4d4d4] hover:text-white hover:bg-[#404040] px-2"
            onClick={onCopy}
          >
            {copied ? <Check className="h-3.5 w-3.5 text-emerald-400 mr-1.5" /> : <Copy className="h-3.5 w-3.5 mr-1.5" />}
            {copied ? "Copied!" : "Copy"}
          </Button>
          {onToggleFullscreen && (
            <Button
              size="sm" variant="ghost"
              className="h-7 w-7 p-0 text-[#808080] hover:text-white hover:bg-[#404040]"
              onClick={onToggleFullscreen}
              title={isFullscreen ? "Exit fullscreen" : "Fullscreen"}
            >
              {isFullscreen ? <Minimize2 className="h-3.5 w-3.5" /> : <Maximize2 className="h-3.5 w-3.5" />}
            </Button>
          )}
        </div>
      </div>

      {/* Editor area */}
      {children}
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────
export function DetailView({ snippet, isFavoriting: _isFavoriting, isEditorFullscreen = false, onToggleFullscreen, onBack: _onBack, onEdit: _onEdit, onFavorite: _onFavorite, onCopy, onDelete: _onDelete }: DetailViewProps) {
  const [copied, setCopied] = useState(false);
  const monacoLang = MONACO_LANGUAGE_MAP[snippet.language] ?? "plaintext";

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(snippet.code);
      setCopied(true);
      onCopy();
      setTimeout(() => setCopied(false), 2000);
    } catch { }
  };

  const editorContent = (
    <Suspense fallback={<div className="flex items-center justify-center text-[#a0a0a0] text-sm font-mono animate-pulse" style={{ height: 520 }}>Loading editor...</div>}>
      <MonacoEditor
        height="100%"
        language={monacoLang}
        theme="vs-dark"
        value={snippet.code}
        options={{
          readOnly: true, minimap: { enabled: false }, fontSize: 14,
          fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace",
          padding: { top: 20, bottom: 20 }, scrollBeyondLastLine: false,
          renderLineHighlight: "none", overviewRulerBorder: false,
          lineNumbers: "on", folding: true, automaticLayout: true,
        }}
      />
    </Suspense>
  );

  return (
    <div className="max-w-5xl mx-auto w-full py-8 px-6 lg:px-8 space-y-6 animate-in fade-in duration-300">

      {/* Header Info */}
      <div className="space-y-4">
        <div className="flex items-center gap-3 flex-wrap">
          <Badge variant="outline" className={cn("text-xs font-mono font-bold uppercase tracking-wider px-2.5 py-0.5 rounded", langBadgeClass(snippet.language))}>
            {snippet.language}
          </Badge>
          <div className="flex items-center gap-4 text-xs text-muted-foreground font-medium">
            <span className="flex items-center gap-1.5"><User className="h-3.5 w-3.5" /> {snippet.authorName}</span>
            <span className="flex items-center gap-1.5"><Calendar className="h-3.5 w-3.5" /> {new Date(snippet.createdAt).toLocaleDateString()}</span>
          </div>
        </div>

        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground mb-3">{snippet.title}</h1>
          {snippet.description && <p className="text-base text-muted-foreground max-w-3xl leading-relaxed">{snippet.description}</p>}
        </div>

        {snippet.tags.length > 0 && (
          <div className="flex flex-wrap gap-2 pt-2">
            {snippet.tags.map((t) => (
              <Badge key={t} variant="secondary" className="text-xs font-medium px-2.5 py-0.5 bg-muted/60 border-transparent text-muted-foreground">
                {t}
              </Badge>
            ))}
          </div>
        )}
      </div>

      {snippet.notes && (
        <Alert className="bg-amber-50/50 border-amber-200/60 text-amber-800 dark:bg-amber-950/20 dark:border-amber-900/40 dark:text-amber-300">
          <AlertTriangle className="h-4 w-4 !text-amber-600 dark:!text-amber-400" />
          <div className="font-semibold text-sm mb-1 ml-2">Implementation Notes</div>
          <div className="text-sm ml-2 leading-relaxed opacity-90 whitespace-pre-wrap">{snippet.notes}</div>
        </Alert>
      )}

      {/* Mac Terminal Window Frame — Code */}
      {isEditorFullscreen ? (
        <div className="fixed inset-0 z-[100] bg-background/95 backdrop-blur flex flex-col">
          <MacWindowFrame
            language={snippet.language}
            monacoLang={monacoLang}
            onCopy={handleCopy}
            onToggleFullscreen={onToggleFullscreen}
            isFullscreen={true}
            copied={copied}
          >
            <div className="flex-1 w-full" style={{ height: "calc(100vh - 40px)" }}>
              {editorContent}
            </div>
          </MacWindowFrame>
        </div>
      ) : (
        <MacWindowFrame
          language={snippet.language}
          monacoLang={monacoLang}
          onCopy={handleCopy}
          onToggleFullscreen={onToggleFullscreen}
          isFullscreen={false}
          copied={copied}
        >
          <div className="h-[560px] w-full relative">
            {editorContent}
          </div>
        </MacWindowFrame>
      )}
    </div>
  );
}