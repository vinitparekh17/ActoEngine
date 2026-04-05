import { lazy, Suspense, useState } from "react";
import { AlertTriangle, Calendar, User } from "lucide-react";
import { MONACO_LANGUAGE_MAP, SnippetDetail } from "@/types/snippet";
import { Badge } from "../ui/badge";
import { Alert } from "../ui/alert";
import { cn, formatDate } from "@/lib/utils";
import { CodeWindowFrame, langBadgeClass } from "./CodeWindowFrame";
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
    <div className="flex flex-col lg:flex-row flex-1 min-h-0 animate-in fade-in duration-300 overflow-hidden">

      {/* ── LEFT: Metadata panel ─────────────────────────────────── */}
      <div className="lg:w-[38%] xl:w-[33%] shrink-0 overflow-y-auto border-r border-border/50">
        <div className="px-6 lg:px-8 py-8 space-y-6">

          {/* Header Info */}
          <div className="space-y-4">
            <div className="flex items-center gap-3 flex-wrap">
              <Badge variant="outline" className={cn("text-xs font-mono font-bold uppercase tracking-wider px-2.5 py-0.5 rounded", langBadgeClass(snippet.language))}>
                {snippet.language}
              </Badge>
              <div className="flex items-center gap-4 text-xs text-muted-foreground font-medium">
                <span className="flex items-center gap-1.5"><User className="h-3.5 w-3.5" /> {snippet.authorName}</span>
                <span className="flex items-center gap-1.5"><Calendar className="h-3.5 w-3.5" /> {formatDate(snippet.createdAt, "PPP", "N/A")}</span>
              </div>
            </div>

            <div>
              <h1 className="text-2xl font-bold tracking-tight text-foreground mb-2">{snippet.title}</h1>
              {snippet.description && <p className="text-sm text-muted-foreground leading-relaxed">{snippet.description}</p>}
            </div>

            {snippet.tags.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {snippet.tags.map((t) => (
                  <Badge key={t} variant="secondary" className="text-xs font-medium px-2.5 py-0.5 bg-muted/60 border-transparent text-muted-foreground">
                    {t}
                  </Badge>
                ))}
              </div>
            )}
          </div>

          {snippet.notes && (
            <Alert className="bg-amber-50/50 border-amber-200/60 text-amber-800 dark:bg-amber-950/20 dark:border-amber-900/40 dark:text-amber-300 flex flex-row items-start gap-3">
              <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0 !text-amber-600 dark:!text-amber-400" />
              <div>
                <div className="font-semibold text-sm mb-1">Implementation Notes</div>
                <div className="text-sm leading-relaxed opacity-90 whitespace-pre-wrap">{snippet.notes}</div>
              </div>
            </Alert>
          )}

        </div>
      </div>

      {/* ── RIGHT: Code window ──────────────────────────────────── */}
      <div className="flex-1 flex flex-col min-h-0 overflow-hidden p-4 lg:p-6">
        {isEditorFullscreen ? (
          <div className="fixed inset-0 z-[100] bg-background/95 backdrop-blur flex flex-col">
            <CodeWindowFrame
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
            </CodeWindowFrame>
          </div>
        ) : (
          <CodeWindowFrame
            language={snippet.language}
            monacoLang={monacoLang}
            onCopy={handleCopy}
            onToggleFullscreen={onToggleFullscreen}
            isFullscreen={false}
            copied={copied}
            className="flex-1"
          >
            {editorContent}
          </CodeWindowFrame>
        )}
      </div>

    </div>
  );
}
