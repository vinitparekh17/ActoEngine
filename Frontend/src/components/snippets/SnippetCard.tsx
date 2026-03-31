import { Copy, Heart, User, Calendar } from "lucide-react";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import type { SnippetListItem } from "@/types/snippet";

interface SnippetCardProps {
  snippet: SnippetListItem;
  currentUserId?: number;
  onView: (snippet: SnippetListItem) => void;
  onEdit: (snippet: SnippetListItem) => void;
  onFavorite: (snippetId: number) => void;
  isFavoriting: boolean;
}

export function SnippetCard({
  snippet,
  currentUserId,
  onView,
  onEdit,
  onFavorite,
  isFavoriting,
}: SnippetCardProps) {
  const isOwner = currentUserId === snippet.createdBy;

  const formattedDate = new Date(snippet.createdAt).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });

  return (
    <Card
      className="flex flex-col cursor-pointer hover:border-primary/50 transition-colors"
      onClick={() => onView(snippet)}
    >
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base leading-snug line-clamp-2">
              {snippet.title}
            </CardTitle>
            {snippet.description && (
              <CardDescription className="mt-1 line-clamp-2 text-xs">
                {snippet.description}
              </CardDescription>
            )}
          </div>
          <Badge variant="secondary" className="shrink-0 text-xs font-mono">
            {snippet.language}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="flex flex-col gap-3 pt-0 flex-1">
        {snippet.tags.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {snippet.tags.slice(0, 4).map((tag) => (
              <Badge key={tag} variant="outline" className="text-xs px-1.5 py-0">
                {tag}
              </Badge>
            ))}
            {snippet.tags.length > 4 && (
              <Badge variant="outline" className="text-xs px-1.5 py-0 text-muted-foreground">
                +{snippet.tags.length - 4}
              </Badge>
            )}
          </div>
        )}

        <div className="mt-auto flex items-center justify-between text-xs text-muted-foreground">
          <div className="flex items-center gap-3">
            <span className="flex items-center gap-1">
              <User className="h-3 w-3" />
              {snippet.authorName}
            </span>
            <span className="flex items-center gap-1">
              <Calendar className="h-3 w-3" />
              {formattedDate}
            </span>
          </div>

          <div
            className="flex items-center gap-2"
            onClick={(e) => e.stopPropagation()}
          >
            <Tooltip>
              <TooltipTrigger asChild>
                <span className="flex items-center gap-1">
                  <Copy className="h-3 w-3" />
                  {snippet.copyCount}
                </span>
              </TooltipTrigger>
              <TooltipContent>Times copied</TooltipContent>
            </Tooltip>

            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6"
                  disabled={isFavoriting}
                  onClick={() => onFavorite(snippet.snippetId)}
                >
                  <Heart
                    className={`h-3.5 w-3.5 ${
                      snippet.isFavorited
                        ? "fill-red-500 text-red-500"
                        : "text-muted-foreground"
                    }`}
                  />
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                {snippet.isFavorited ? "Unfavorite" : "Favorite"} ({snippet.favoriteCount})
              </TooltipContent>
            </Tooltip>

            {isOwner && (
              <Button
                variant="ghost"
                size="sm"
                className="h-6 text-xs px-2"
                onClick={() => onEdit(snippet)}
              >
                Edit
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
