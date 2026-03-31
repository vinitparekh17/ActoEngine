import { lazy, Suspense, useState } from "react";
import { Copy, Check, Heart, Pencil, User, Calendar, AlertTriangle } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { SnippetDetail, SnippetListItem } from "@/types/snippet";
import { MONACO_LANGUAGE_MAP } from "@/types/snippet";

const MonacoEditor = lazy(() => import("@monaco-editor/react"));

interface SnippetDetailModalProps {
  isOpen: boolean;
  snippet: SnippetDetail | null;
  currentUserId?: number;
  isFavoriting: boolean;
  onClose: () => void;
  onEdit: (snippet: SnippetDetail) => void;
  onFavorite: (snippetId: number) => void;
  onCopy: (snippetId: number) => void;
}

export function SnippetDetailModal({
  isOpen,
  snippet,
  currentUserId,
  isFavoriting,
  onClose,
  onEdit,
  onFavorite,
  onCopy,
}: SnippetDetailModalProps) {
  const [copied, setCopied] = useState(false);

  if (!snippet) return null;

  const isOwner = currentUserId === snippet.createdBy;
  const monacoLang = MONACO_LANGUAGE_MAP[snippet.language] ?? "plaintext";

  const formattedDate = new Date(snippet.createdAt).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(snippet.code);
      setCopied(true);
      onCopy(snippet.snippetId);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API unavailable — fallback handled gracefully
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-4xl w-full max-h-[90vh] flex flex-col overflow-hidden p-0">
        <DialogHeader className="px-6 pt-6 pb-4 border-b border-border/60 shrink-0">
          <div className="flex items-start justify-between gap-4">
            <div className="flex-1 min-w-0">
              <DialogTitle className="text-lg leading-snug">{snippet.title}</DialogTitle>
              {snippet.description && (
                <p className="text-sm text-muted-foreground mt-1">{snippet.description}</p>
              )}
            </div>
            <div className="flex items-center gap-2 shrink-0">
              <Badge variant="secondary" className="font-mono">
                {snippet.language}
              </Badge>
              {isOwner && (
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-1.5"
                  onClick={() => onEdit(snippet)}
                >
                  <Pencil className="h-3.5 w-3.5" />
                  Edit
                </Button>
              )}
            </div>
          </div>

          {/* Meta row */}
          <div className="flex items-center gap-4 text-xs text-muted-foreground mt-2">
            <span className="flex items-center gap-1">
              <User className="h-3 w-3" />
              {snippet.authorName}
            </span>
            <span className="flex items-center gap-1">
              <Calendar className="h-3 w-3" />
              {formattedDate}
            </span>
            <span className="flex items-center gap-1">
              <Copy className="h-3 w-3" />
              {snippet.copyCount} {snippet.copyCount === 1 ? "copy" : "copies"}
            </span>
            <span className="flex items-center gap-1">
              <Heart className={`h-3 w-3 ${snippet.isFavorited ? "fill-red-500 text-red-500" : ""}`} />
              {snippet.favoriteCount} {snippet.favoriteCount === 1 ? "favorite" : "favorites"}
            </span>
          </div>

          {snippet.tags.length > 0 && (
            <div className="flex flex-wrap gap-1 mt-2">
              {snippet.tags.map((tag) => (
                <Badge key={tag} variant="outline" className="text-xs px-1.5 py-0">
                  {tag}
                </Badge>
              ))}
            </div>
          )}
        </DialogHeader>

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
          {/* Notes / Warnings */}
          {snippet.notes && (
            <div className="flex gap-3 rounded-lg border border-amber-500/30 bg-amber-500/5 px-4 py-3 text-sm">
              <AlertTriangle className="h-4 w-4 text-amber-500 shrink-0 mt-0.5" />
              <p className="text-amber-700 dark:text-amber-400 whitespace-pre-wrap leading-relaxed">
                {snippet.notes}
              </p>
            </div>
          )}

          {/* Code viewer */}
          <div className="relative rounded-lg overflow-hidden border border-border/40 bg-[#1e1e1e]">
            <div className="absolute top-2 right-2 z-10 flex gap-1.5">
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 bg-zinc-700/80 hover:bg-zinc-600 text-zinc-300"
                    onClick={handleCopy}
                  >
                    {copied ? (
                      <Check className="h-3.5 w-3.5 text-emerald-400" />
                    ) : (
                      <Copy className="h-3.5 w-3.5" />
                    )}
                  </Button>
                </TooltipTrigger>
                <TooltipContent>{copied ? "Copied!" : "Copy code"}</TooltipContent>
              </Tooltip>

              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 bg-zinc-700/80 hover:bg-zinc-600 text-zinc-300"
                    disabled={isFavoriting}
                    onClick={() => onFavorite(snippet.snippetId)}
                  >
                    <Heart
                      className={`h-3.5 w-3.5 ${
                        snippet.isFavorited ? "fill-red-500 text-red-500" : ""
                      }`}
                    />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>{snippet.isFavorited ? "Unfavorite" : "Favorite"}</TooltipContent>
              </Tooltip>
            </div>

            <Suspense
              fallback={
                <div className="p-6 space-y-3">
                  {Array.from({ length: 12 }).map((_, i) => (
                    <Skeleton
                      key={i}
                      className="h-3.5 opacity-10 bg-primary"
                      style={{ width: `${40 + (i * 11) % 50}%` }}
                    />
                  ))}
                </div>
              }
            >
              <MonacoEditor
                height="380px"
                language={monacoLang}
                theme="vs-dark"
                value={snippet.code}
                options={{
                  readOnly: true,
                  minimap: { enabled: false },
                  fontSize: 13,
                  fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
                  wordWrap: "on",
                  scrollBeyondLastLine: false,
                  padding: { top: 12, bottom: 12 },
                  lineHeight: 21,
                  renderLineHighlight: "none",
                  contextmenu: false,
                }}
              />
            </Suspense>
          </div>
        </div>

        <div className="flex justify-end gap-2 px-6 py-4 border-t border-border/60 shrink-0">
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
          <Button className="gap-2" onClick={handleCopy}>
            {copied ? (
              <>
                <Check className="h-4 w-4" />
                Copied!
              </>
            ) : (
              <>
                <Copy className="h-4 w-4" />
                Copy Code
              </>
            )}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
