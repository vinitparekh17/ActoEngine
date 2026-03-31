import { useState, useCallback } from "react";
import { Plus, BookOpen } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import { useApi, useApiMutation, useApiPost, queryKeys } from "@/hooks/useApi";
import { useAuth } from "@/hooks/useAuth";
import {
  SnippetCard,
  SnippetFilterBar,
  SnippetFormModal,
  SnippetDetailModal,
} from "@/components/snippets";
import type {
  SnippetListItem,
  SnippetDetail,
  CreateSnippetRequest,
  UpdateSnippetRequest,
  SnippetFilterOptions,
  PaginatedResponse,
} from "@/types/snippet";

const PAGE_SIZE = 18;

export default function SnippetLibraryPage() {
  const { user } = useAuth();

  // Filter state
  const [search, setSearch] = useState("");
  const [language, setLanguage] = useState("");
  const [tag, setTag] = useState("");
  const [sortBy, setSortBy] = useState("recent");
  const [page, setPage] = useState(1);

  // Modal state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editingSnippet, setEditingSnippet] = useState<SnippetDetail | null>(null);
  const [viewingSnippetId, setViewingSnippetId] = useState<number | null>(null);

  // Optimistic favorite state
  const [favoritingId, setFavoritingId] = useState<number | null>(null);

  const listEndpoint = `/Snippet?search=${encodeURIComponent(search)}&language=${encodeURIComponent(language)}&tag=${encodeURIComponent(tag)}&sortBy=${sortBy}&page=${page}&pageSize=${PAGE_SIZE}`;

  const { data: listData, isLoading } = useApi<PaginatedResponse<SnippetListItem>>(listEndpoint, {
    // Granular key so each unique filter combo is cached separately
    queryKey: Array.from(queryKeys.snippets.list(search, language, tag, sortBy, page)),
  });

  const { data: filterOptions } = useApi<SnippetFilterOptions>("/Snippet/filters", {
    queryKey: Array.from(queryKeys.snippets.filters()),
    staleTime: 5 * 60 * 1000,
  });

  const { data: viewingSnippet } = useApi<SnippetDetail>(
    viewingSnippetId ? `/Snippet/${viewingSnippetId}` : "",
    {
      queryKey: viewingSnippetId ? Array.from(queryKeys.snippets.detail(viewingSnippetId)) : ["noop"],
      enabled: !!viewingSnippetId && isDetailOpen,
    },
  );

  // Invalidate all list + filter queries on any mutation — prefix ["snippets"] covers all of them
  const invalidateAll: string[][] = [
    Array.from(queryKeys.snippets.all()),
    Array.from(queryKeys.snippets.filters()),
  ];

  const createMutation = useApiPost<SnippetDetail, CreateSnippetRequest>("/Snippet", {
    successMessage: "Snippet added to library",
    invalidateKeys: invalidateAll,
  });

  const updateMutation = useApiMutation<void, UpdateSnippetRequest & { snippetId: number }>(
    "/Snippet/:snippetId",
    "PUT",
    {
      successMessage: "Snippet updated",
      invalidateKeys: invalidateAll,
    },
  );

  const favoriteMutation = useApiPost<{ isFavorited: boolean }, { snippetId: number }>(
    "/Snippet/:snippetId/favorite",
    { showSuccessToast: false },
  );

  const copyMutation = useApiPost<void, { snippetId: number }>("/Snippet/:snippetId/copy", {
    showSuccessToast: false,
    showErrorToast: false,
  });

  const handleCreate = (data: CreateSnippetRequest) => {
    createMutation.mutate(data, {
      onSuccess: () => {
        setIsFormOpen(false);
      },
    });
  };

  const handleUpdate = (data: CreateSnippetRequest) => {
    if (!editingSnippet) return;
    updateMutation.mutate(
      { ...data, snippetId: editingSnippet.snippetId },
      {
        onSuccess: () => {
          setIsFormOpen(false);
          setIsDetailOpen(false);
          setEditingSnippet(null);
        },
      },
    );
  };

  const handleView = useCallback((snippet: SnippetListItem) => {
    setViewingSnippetId(snippet.snippetId);
    setIsDetailOpen(true);
  }, []);

  const handleEdit = useCallback((snippet: SnippetListItem | SnippetDetail) => {
    if ("code" in snippet) {
      setEditingSnippet(snippet as SnippetDetail);
    } else {
      setEditingSnippet(null);
      setViewingSnippetId(snippet.snippetId);
    }
    setIsEditing(true);
    setIsDetailOpen(false);
    setIsFormOpen(true);
  }, []);

  const handleFavorite = useCallback(
    (snippetId: number) => {
      setFavoritingId(snippetId);
      favoriteMutation.mutate(
        { snippetId },
        { onSettled: () => setFavoritingId(null) },
      );
    },
    [favoriteMutation],
  );

  const handleCopy = useCallback(
    (snippetId: number) => {
      copyMutation.mutate({ snippetId });
    },
    [copyMutation],
  );

  const handleFormClose = () => {
    setIsFormOpen(false);
    setIsEditing(false);
    setEditingSnippet(null);
  };

  const handleDetailClose = () => {
    setIsDetailOpen(false);
    setViewingSnippetId(null);
  };

  const handleEditFromDetail = (snippet: SnippetDetail) => {
    setEditingSnippet(snippet);
    setIsEditing(true);
    setIsDetailOpen(false);
    setIsFormOpen(true);
  };

  const handleClearFilters = () => {
    setSearch("");
    setLanguage("");
    setTag("");
    setSortBy("recent");
    setPage(1);
  };

  const snippets = listData?.items ?? [];
  const totalCount = listData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const isPending =
    createMutation.isPending || updateMutation.isPending;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <BookOpen className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-xl font-semibold">Snippet Library</h1>
            <p className="text-sm text-muted-foreground">
              {totalCount > 0
                ? `${totalCount} shared snippet${totalCount !== 1 ? "s" : ""}`
                : "Shared code snippets for the team"}
            </p>
          </div>
        </div>
        <Button
          onClick={() => {
            setIsEditing(false);
            setEditingSnippet(null);
            setIsFormOpen(true);
          }}
          className="gap-2"
        >
          <Plus className="h-4 w-4" />
          New Snippet
        </Button>
      </div>

      {/* Filters */}
      <SnippetFilterBar
        search={search}
        language={language}
        tag={tag}
        sortBy={sortBy}
        filterOptions={filterOptions}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        onLanguageChange={(v) => { setLanguage(v); setPage(1); }}
        onTagChange={(v) => { setTag(v); setPage(1); }}
        onSortChange={(v) => { setSortBy(v); setPage(1); }}
        onClearFilters={handleClearFilters}
      />

      {/* Grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div
              key={i}
              className="h-44 rounded-xl border border-border/60 bg-card animate-pulse"
            />
          ))}
        </div>
      ) : snippets.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 gap-3 text-center">
          <BookOpen className="h-12 w-12 text-muted-foreground/30" />
          <p className="text-muted-foreground font-medium">
            {search || language || tag
              ? "No snippets match your filters"
              : "No snippets yet — be the first to add one!"}
          </p>
          {!search && !language && !tag && (
            <Button
              variant="outline"
              className="mt-2"
              onClick={() => {
                setIsEditing(false);
                setEditingSnippet(null);
                setIsFormOpen(true);
              }}
            >
              <Plus className="h-4 w-4 mr-2" />
              Add First Snippet
            </Button>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {snippets.map((snippet) => (
            <SnippetCard
              key={snippet.snippetId}
              snippet={snippet}
              currentUserId={user?.userId}
              onView={handleView}
              onEdit={handleEdit}
              onFavorite={handleFavorite}
              isFavoriting={favoritingId === snippet.snippetId}
            />
          ))}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex justify-center pt-2">
          <Pagination>
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious
                  onClick={() => page > 1 && handlePageChange(page - 1)}
                  aria-disabled={page <= 1}
                  className={page <= 1 ? "pointer-events-none opacity-50" : "cursor-pointer"}
                />
              </PaginationItem>

              {Array.from({ length: totalPages }, (_, i) => i + 1)
                .filter(
                  (p) =>
                    p === 1 ||
                    p === totalPages ||
                    Math.abs(p - page) <= 1,
                )
                .reduce<(number | "ellipsis")[]>((acc, p, i, arr) => {
                  if (i > 0 && p - (arr[i - 1] as number) > 1) acc.push("ellipsis");
                  acc.push(p);
                  return acc;
                }, [])
                .map((p, i) =>
                  p === "ellipsis" ? (
                    <PaginationItem key={`ellipsis-${i}`}>
                      <span className="px-3 py-2 text-muted-foreground">…</span>
                    </PaginationItem>
                  ) : (
                    <PaginationItem key={p}>
                      <PaginationLink
                        isActive={p === page}
                        onClick={() => handlePageChange(p as number)}
                        className="cursor-pointer"
                      >
                        {p}
                      </PaginationLink>
                    </PaginationItem>
                  ),
                )}

              <PaginationItem>
                <PaginationNext
                  onClick={() => page < totalPages && handlePageChange(page + 1)}
                  aria-disabled={page >= totalPages}
                  className={page >= totalPages ? "pointer-events-none opacity-50" : "cursor-pointer"}
                />
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        </div>
      )}

      {/* Modals */}
      <SnippetFormModal
        isOpen={isFormOpen}
        isEditing={isEditing}
        editingSnippet={editingSnippet}
        isPending={isPending}
        onSubmit={isEditing ? handleUpdate : handleCreate}
        onClose={handleFormClose}
      />

      <SnippetDetailModal
        isOpen={isDetailOpen}
        snippet={viewingSnippet ?? null}
        currentUserId={user?.userId}
        isFavoriting={favoritingId === viewingSnippetId}
        onClose={handleDetailClose}
        onEdit={handleEditFromDetail}
        onFavorite={handleFavorite}
        onCopy={handleCopy}
      />
    </div>
  );
}
