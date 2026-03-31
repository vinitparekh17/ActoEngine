import { Search, X } from "lucide-react";
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
  const hasActiveFilters = !!search || !!language || !!tag;

  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:flex-wrap">
      <div className="relative flex-1 min-w-[200px]">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground pointer-events-none" />
        <Input
          placeholder="Search title, code, or description..."
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          className="pl-9"
        />
      </div>

      <Select
        value={language || ALL_VALUE}
        onValueChange={(v) => onLanguageChange(v === ALL_VALUE ? "" : v)}
      >
        <SelectTrigger className="w-[150px]">
          <SelectValue placeholder="Language" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL_VALUE}>All Languages</SelectItem>
          {filterOptions?.languages.map((lang) => (
            <SelectItem key={lang} value={lang}>
              {lang}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        value={tag || ALL_VALUE}
        onValueChange={(v) => onTagChange(v === ALL_VALUE ? "" : v)}
      >
        <SelectTrigger className="w-[150px]">
          <SelectValue placeholder="Tag" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL_VALUE}>All Tags</SelectItem>
          {filterOptions?.tags.map((t) => (
            <SelectItem key={t} value={t}>
              {t}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select value={sortBy} onValueChange={onSortChange}>
        <SelectTrigger className="w-[160px]">
          <SelectValue placeholder="Sort by" />
        </SelectTrigger>
        <SelectContent>
          {SORT_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {hasActiveFilters && (
        <Button
          variant="ghost"
          size="sm"
          onClick={onClearFilters}
          className="gap-1.5 text-muted-foreground"
        >
          <X className="h-3.5 w-3.5" />
          Clear
        </Button>
      )}
    </div>
  );
}
