import { cn, formatCompactRelativeTime } from "@/lib/utils";
import type { SnippetListItem } from "@/types/snippet";
import { Clock, Copy, Heart, User } from "lucide-react";

interface SnippetCardProps {
  snippet: SnippetListItem;
  onClick: () => void;
}

function langAccent(lang: string): string {
  const l = lang.toLowerCase();
  if (l.includes("sql")) return "#3b82f6";
  if (l.includes("js") || l.includes("javascript")) return "#eab308";
  if (l.includes("ts") || l.includes("typescript")) return "#3b82f6";
  if (l.includes("c#") || l.includes("csharp")) return "#a855f7";
  if (l.includes("python")) return "#22c55e";
  if (l.includes("html") || l.includes("css")) return "#f97316";
  if (l.includes("json") || l.includes("xml")) return "#10b981";
  if (l.includes("bash") || l.includes("shell")) return "#64748b";
  return "#94a3b8";
}

export function SnippetCard({ snippet, onClick }: SnippetCardProps) {
  const relative = formatCompactRelativeTime(snippet.createdAt, "recently");

  return (
    <div
      onClick={onClick}
      className={cn(
        "group relative flex flex-col h-[220px] p-5 rounded-2xl cursor-pointer transition-all duration-300",
        "bg-card border border-border/40 shadow-sm",
        "hover:border-primary/30 hover:shadow-md hover:bg-accent/5",
      )}
    >
      <div
        className="absolute top-0 inset-x-0 h-[1px] opacity-0 group-hover:opacity-100 transition-opacity duration-500"
        style={{
          background: `linear-gradient(90deg, transparent, ${langAccent(snippet.language)}, transparent)`,
        }}
      />

      <div className="flex items-start justify-between gap-3 mb-4">
        <div className="flex items-center gap-2">
          <div
            className="w-2 h-2 rounded-full shadow-sm"
            style={{ backgroundColor: langAccent(snippet.language) }}
          />
          <span className="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">
            {snippet.language}
          </span>
        </div>

        <div className="flex items-center gap-2.5 text-muted-foreground/50 transition-colors group-hover:text-muted-foreground/80">
          {snippet.copyCount > 0 && (
            <div
              className="flex items-center gap-1 text-[11px] font-medium"
              title={`${snippet.copyCount} copies`}
            >
              <Copy className="h-3 w-3" /> {snippet.copyCount}
            </div>
          )}
          {snippet.favoriteCount > 0 && (
            <div
              className="flex items-center gap-1 text-[11px] font-medium"
              title={`${snippet.favoriteCount} favorites`}
            >
              <Heart
                className={cn(
                  "h-3 w-3",
                  snippet.isFavorited && "fill-red-500 text-red-500",
                )}
              />{" "}
              {snippet.favoriteCount}
            </div>
          )}
        </div>
      </div>

      <div className="space-y-1.5">
        <h3 className="text-base font-semibold text-foreground leading-tight line-clamp-2 group-hover:text-primary transition-colors">
          {snippet.title}
        </h3>
        {snippet.description ? (
          <p className="text-xs text-muted-foreground/80 line-clamp-2 leading-relaxed">
            {snippet.description}
          </p>
        ) : (
          <p className="text-xs text-muted-foreground/40 italic line-clamp-1">
            No description
          </p>
        )}
      </div>

      <div className="mt-auto pt-2 flex flex-col gap-3.5">
        <div className="flex flex-wrap gap-1.5 overflow-hidden h-[22px]">
          {snippet.tags.slice(0, 3).map((t) => (
            <span
              key={t}
              className="text-[10px] px-2 py-0.5 rounded-md bg-muted/60 text-muted-foreground font-medium whitespace-nowrap"
            >
              {t}
            </span>
          ))}
          {snippet.tags.length > 3 && (
            <span className="text-[10px] text-muted-foreground/50 px-1 py-0.5 font-medium">
              +{snippet.tags.length - 3}
            </span>
          )}
        </div>

        <div className="flex items-center justify-between text-[11px] text-muted-foreground/70">
          <span className="flex items-center gap-1.5 font-medium truncate pr-2">
            <User className="h-3 w-3 opacity-60" /> {snippet.authorName}
          </span>
          <span className="shrink-0 flex items-center gap-1.5">
            <Clock className="h-3 w-3 opacity-60" /> {relative}
          </span>
        </div>
      </div>
    </div>
  );
}
