export interface SnippetListItem {
  snippetId: number;
  title: string;
  description?: string;
  language: string;
  tags: string[];
  authorName: string;
  createdBy: number;
  copyCount: number;
  favoriteCount: number;
  isFavorited: boolean;
  createdAt: string;
}

export interface SnippetDetail extends SnippetListItem {
  code: string;
  notes?: string;
  updatedAt?: string;
}

export interface CreateSnippetRequest {
  title: string;
  code: string;
  language: string;
  description?: string;
  notes?: string;
  tags: string[];
}

export interface UpdateSnippetRequest extends CreateSnippetRequest {}

export interface SnippetFilterOptions {
  tags: string[];
  languages: string[];
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const SNIPPET_LANGUAGES = [
  "SQL",
  "JavaScript",
  "TypeScript",
  "C#",
  "HTML",
  "CSS",
  "PowerShell",
  "Bash",
  "JSON",
  "XML",
  "Python",
  "Other",
] as const;

export type SnippetLanguage = (typeof SNIPPET_LANGUAGES)[number];

/** Maps display language name to Monaco editor language ID */
export const MONACO_LANGUAGE_MAP: Record<string, string> = {
  SQL: "sql",
  JavaScript: "javascript",
  TypeScript: "typescript",
  "C#": "csharp",
  HTML: "html",
  CSS: "css",
  PowerShell: "powershell",
  Bash: "shell",
  JSON: "json",
  XML: "xml",
  Python: "python",
  Other: "plaintext",
};

export const SORT_OPTIONS = [
  { value: "recent", label: "Most Recent" },
  { value: "popular", label: "Most Copied" },
  { value: "favorites", label: "Most Favorited" },
  { value: "title", label: "Title A–Z" },
] as const;
