import { ArrowUpDown, Search, X } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { SnippetFilterOptions } from "@/types/snippet";
import { SORT_OPTIONS } from "@/types/snippet";

interface SnippetFilterBarProps {
  search: string;
  language: string;
  tag: string;
  sortBy: string;
  filterOptions: SnippetFilterOptions | undefined;
  onSearchChange: (value: string) => void;
  onLanguageChange: (value: string) => void;
  onTagChange: (value: string) => void;
  onSortChange: (value: string) => void;
  onClearFilters: () => void;
}

const ALL_VALUE = "__all__";

export function SnippetFilterBar({
  search,
  language,
  tag,
  sortBy,
  filterOptions,
  onSearchChange,
  onLanguageChange,
  onTagChange,
  onSortChange,
  onClearFilters,
}: SnippetFilterBarProps) {
  return (
    <div className="flex flex-col md:flex-row gap-4 items-center bg-card p-4 rounded-xl border border-border/60 shadow-sm">
      <div className="relative flex-1 w-full">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by title, description, or content..."
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          className="pl-9 h-10 w-full bg-background"
        />
        {search && (
          <button onClick={() => onSearchChange("")} className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground">
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-3 w-full md:w-auto shrink-0">
        <Select value={language || ALL_VALUE} onValueChange={(v) => { onLanguageChange(v === ALL_VALUE ? "" : v); onClearFilters(); }}>
          <SelectTrigger className="h-10 w-[150px] bg-background">
            <SelectValue placeholder="Language" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_VALUE}>All Languages</SelectItem>
            {filterOptions?.languages.map((lang) => (
              <SelectItem key={lang} value={lang}>{lang}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={tag || ALL_VALUE} onValueChange={(v) => { onTagChange(v === ALL_VALUE ? "" : v); onClearFilters(); }}>
          <SelectTrigger className="h-10 w-[150px] bg-background">
            <SelectValue placeholder="Tag" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_VALUE}>All Tags</SelectItem>
            {filterOptions?.tags.map((t) => (
              <SelectItem key={t} value={t}>{t}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={sortBy} onValueChange={(v) => { onSortChange(v); onClearFilters(); }}>
          <SelectTrigger className="h-10 w-[160px] bg-background">
            <div className="flex items-center gap-2">
              <ArrowUpDown className="w-3.5 h-3.5 text-muted-foreground" />
              <SelectValue />
            </div>
          </SelectTrigger>
          <SelectContent>
            {SORT_OPTIONS.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>

  );
}
