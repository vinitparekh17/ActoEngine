import { useState, useCallback, useEffect } from "react";
import {
  Plus,
  TerminalSquare,
  ChevronLeft, ChevronRight,
  Heart, Pencil, Trash2,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import {
  Breadcrumb, BreadcrumbItem, BreadcrumbLink,
  BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { cn } from "@/lib/utils";

import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription, AlertDialogFooter,
  AlertDialogHeader, AlertDialogTitle,
} from "@/components/ui/alert-dialog";

import { useApi, useApiMutation, useApiPost, queryKeys } from "@/hooks/useApi";
import { useAuth } from "@/hooks/useAuth";

import type {
  SnippetListItem, SnippetDetail, CreateSnippetRequest, UpdateSnippetRequest,
  SnippetFilterOptions, PaginatedResponse,
} from "@/types/snippet";

import { DetailView, EditView, EmptyView, SnippetCard, SnippetFilterBar } from "@/components/snippets";

const PAGE_SIZE = 24;

type ViewMode = "list" | "detail" | "create" | "edit";

export default function SnippetLibraryPage() {
  const { user } = useAuth();

  const [search, setSearch] = useState("");
  const [language, setLanguage] = useState("");
  const [tag, setTag] = useState("");
  const [sortBy, setSortBy] = useState("recent");
  const [page, setPage] = useState(1);

  const [viewMode, setViewMode] = useState<ViewMode>("list");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [editingSnippet, setEditingSnippet] = useState<SnippetDetail | null>(null);
  const [favoritingId, setFavoritingId] = useState<number | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<SnippetDetail | null>(null);
  const [isEditorFullscreen, setIsEditorFullscreen] = useState(false);

  const [debouncedSearch, setDebouncedSearch] = useState("");
  useEffect(() => {
    const timer = setTimeout(() => { setDebouncedSearch(search); setPage(1); }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  // ─── API queries ────────────────────────────────────────────────────
  const listEndpoint = `/Snippet?search=${encodeURIComponent(debouncedSearch)}&language=${encodeURIComponent(language)}&tag=${encodeURIComponent(tag)}&sortBy=${sortBy}&page=${page}&pageSize=${PAGE_SIZE}`;

  const { data: listData, isLoading } = useApi<PaginatedResponse<SnippetListItem>>(listEndpoint, {
    queryKey: Array.from(queryKeys.snippets.list(debouncedSearch, language, tag, sortBy, page)),
    enabled: viewMode === "list",
  });

  const { data: filterOptions } = useApi<SnippetFilterOptions>("/Snippet/filters", {
    queryKey: Array.from(queryKeys.snippets.filters()),
    staleTime: 5 * 60 * 1000,
  });

  const { data: selectedSnippet, isLoading: isLoadingDetail } = useApi<SnippetDetail>(
    selectedId ? `/Snippet/${selectedId}` : "",
    {
      queryKey: selectedId ? Array.from(queryKeys.snippets.detail(selectedId)) : ["noop"],
      enabled: !!selectedId && (viewMode === "detail" || viewMode === "edit"),
    }
  );

  const invalidateAll = [Array.from(queryKeys.snippets.all()), Array.from(queryKeys.snippets.filters())];

  const createMutation = useApiPost<SnippetDetail, CreateSnippetRequest>("/Snippet", {
    successMessage: "Snippet created successfully", invalidateKeys: invalidateAll,
  });

  const updateMutation = useApiMutation<void, UpdateSnippetRequest & { snippetId: number }>("/Snippet/:snippetId", "PUT", {
    successMessage: "Snippet updated successfully", invalidateKeys: invalidateAll,
  });

  const deleteMutation = useApiMutation<void, { snippetId: number }>("/Snippet/:snippetId", "DELETE", {
    successMessage: "Snippet deleted", invalidateKeys: invalidateAll,
  });

  const favoriteMutation = useApiPost<{ isFavorited: boolean }, { snippetId: number }>("/Snippet/:snippetId/favorite", {
    showSuccessToast: false, invalidateKeys: invalidateAll,
  });

  const copyMutation = useApiPost<void, { snippetId: number }>("/Snippet/:snippetId/copy", {
    showSuccessToast: false, showErrorToast: false, invalidateKeys: invalidateAll,
  });

  // ─── Handlers ───────────────────────────────────────────────────────
  const handleSelect = useCallback((snippet: SnippetListItem) => {
    setSelectedId(snippet.snippetId);
    setViewMode("detail");
    setEditingSnippet(null);
    setIsEditorFullscreen(false);
  }, []);

  const handleCreate = useCallback((data: CreateSnippetRequest) => {
    createMutation.mutate(data, {
      onSuccess: (created) => {
        setSelectedId(created.snippetId);
        setViewMode("detail");
        setIsEditorFullscreen(false);
      },
    });
  }, [createMutation]);

  const handleUpdate = useCallback((data: CreateSnippetRequest) => {
    if (!editingSnippet) return;
    updateMutation.mutate({ ...data, snippetId: editingSnippet.snippetId }, {
      onSuccess: () => {
        setSelectedId(editingSnippet.snippetId);
        setViewMode("detail");
        setEditingSnippet(null);
        setIsEditorFullscreen(false);
      },
    });
  }, [editingSnippet, updateMutation]);

  const handleDelete = useCallback(() => {
    if (!deleteTarget) return;
    deleteMutation.mutate({ snippetId: deleteTarget.snippetId }, {
      onSuccess: () => {
        setViewMode("list");
        setSelectedId(null);
        setDeleteTarget(null);
        setIsEditorFullscreen(false);
      },
    });
  }, [deleteTarget, deleteMutation]);

  const handleFavorite = useCallback((snippetId: number) => {
    setFavoritingId(snippetId);
    favoriteMutation.mutate({ snippetId }, { onSettled: () => setFavoritingId(null) });
  }, [favoriteMutation]);

  const handleCopy = useCallback((snippetId: number) => {
    copyMutation.mutate({ snippetId });
  }, [copyMutation]);

  const openCreate = useCallback(() => {
    setViewMode("create");
    setEditingSnippet(null);
    setIsEditorFullscreen(false);
  }, []);

  const openEdit = useCallback(() => {
    if (selectedSnippet) {
      setEditingSnippet(selectedSnippet);
      setViewMode("edit");
      setIsEditorFullscreen(false);
    }
  }, [selectedSnippet]);

  const handleBackToList = useCallback(() => {
    setViewMode("list");
    setEditingSnippet(null);
    setIsEditorFullscreen(false);
  }, []);

  const handleCancelEdit = useCallback(() => {
    if (selectedId && viewMode === "edit") {
      setViewMode("detail");
    } else {
      setViewMode("list");
    }
    setEditingSnippet(null);
    setIsEditorFullscreen(false);
  }, [selectedId, viewMode]);

  const handleClearFilters = useCallback(() => {
    setSearch(""); setLanguage(""); setTag(""); setSortBy("recent"); setPage(1);
  }, []);

  const snippets = listData?.items ?? [];
  const totalCount = listData?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const hasActiveFilters = !!debouncedSearch || !!language || !!tag;

  const isPendingMutation = createMutation.isPending || updateMutation.isPending;
  const snippetTitle = selectedSnippet?.title ?? editingSnippet?.title ?? "";

  // ─── Header Right-Side Actions ───────────────────────────────────────
  const renderHeaderActions = () => {
    if (viewMode === "list") {
      return (
        <Button size="sm" className="h-8 shadow-sm" onClick={openCreate}>
          <Plus className="h-4 w-4 mr-1.5" /> New Snippet
        </Button>
      );
    }

    if (viewMode === "detail" && selectedSnippet) {
      const isOwner = user?.userId === selectedSnippet.createdBy;
      const isAdmin = user?.role === "Admin";
      const canModify = isOwner || isAdmin;
      return (
        <div className="flex items-center gap-2">
          <Button
            variant="outline" size="sm"
            className={cn("gap-1.5 h-8", selectedSnippet.isFavorited && "text-red-500 border-red-200 hover:bg-red-50 dark:border-red-900 dark:hover:bg-red-950/30")}
            disabled={favoritingId === selectedSnippet.snippetId}
            onClick={() => handleFavorite(selectedSnippet.snippetId)}
          >
            <Heart className={cn("h-3.5 w-3.5", selectedSnippet.isFavorited && "fill-red-500")} />
            {selectedSnippet.isFavorited ? "Favorited" : "Favorite"}
          </Button>
          {canModify && (
            <>
              <Button variant="outline" size="sm" className="gap-1.5 h-8" onClick={openEdit}>
                <Pencil className="h-3.5 w-3.5" /> Edit
              </Button>
              <Button
                variant="outline" size="sm"
                className="gap-1.5 h-8 text-destructive hover:bg-destructive/10 hover:text-destructive border-destructive/30"
                onClick={() => setDeleteTarget(selectedSnippet)}
              >
                <Trash2 className="h-3.5 w-3.5" /> Delete
              </Button>
            </>
          )}
        </div>
      );
    }

    if (viewMode === "create" || viewMode === "edit") {
      return (
        <div className="flex items-center gap-2">
          <Button type="button" variant="outline" size="sm" className="h-8" onClick={handleCancelEdit} disabled={isPendingMutation}>
            Cancel
          </Button>
          <Button type="submit" size="sm" className="h-8 shadow-sm" form="snippet-form" disabled={isPendingMutation}>
            {isPendingMutation ? "Saving..." : viewMode === "edit" ? "Save Changes" : "Create Snippet"}
          </Button>
        </div>
      );
    }

    return null;
  };

  // ─── Breadcrumb ──────────────────────────────────────────────────────
  const renderBreadcrumb = () => (
    <Breadcrumb>
      <BreadcrumbList>
        <BreadcrumbItem>
          {viewMode === "list" ? (
            <BreadcrumbPage className="font-semibold flex items-center gap-2">
              <TerminalSquare className="h-4 w-4 text-primary" />
              Snippet Library
            </BreadcrumbPage>
          ) : (
            <BreadcrumbLink
              className="flex items-center gap-2 cursor-pointer hover:text-foreground transition-colors"
              onClick={handleBackToList}
            >
              <TerminalSquare className="h-4 w-4 text-primary" />
              Snippet Library
            </BreadcrumbLink>
          )}
        </BreadcrumbItem>

        {(viewMode === "detail" || viewMode === "edit") && snippetTitle && (
          <>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              {viewMode === "detail" ? (
                <BreadcrumbPage className="font-medium max-w-[240px] truncate">{snippetTitle}</BreadcrumbPage>
              ) : (
                <BreadcrumbLink
                  className="cursor-pointer hover:text-foreground transition-colors max-w-[180px] truncate"
                  onClick={() => setViewMode("detail")}
                >
                  {snippetTitle}
                </BreadcrumbLink>
              )}
            </BreadcrumbItem>
          </>
        )}

        {viewMode === "edit" && (
          <>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage className="font-medium">Editing</BreadcrumbPage>
            </BreadcrumbItem>
          </>
        )}

        {viewMode === "create" && (
          <>
            <BreadcrumbSeparator />
            <BreadcrumbItem>
              <BreadcrumbPage className="font-medium">New Snippet</BreadcrumbPage>
            </BreadcrumbItem>
          </>
        )}
      </BreadcrumbList>
    </Breadcrumb>
  );

  return (
    <TooltipProvider>
      <div className="flex flex-col h-[calc(100vh-7rem)] bg-background overflow-hidden">

        {/* ─── Sticky Top Header ─────────────────────────────────────── */}
        <header className="h-14 px-6 border-b bg-background/95 backdrop-blur shrink-0 flex items-center justify-between z-30 supports-[backdrop-filter]:bg-background/60">
          <div className="flex items-center gap-3 min-w-0">
            {renderBreadcrumb()}
            {viewMode === "list" && totalCount > 0 && (
              <>
                <Separator orientation="vertical" className="h-4" />
                <Badge variant="outline" className="font-normal text-xs shrink-0">
                  {totalCount} total
                </Badge>
              </>
            )}
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {renderHeaderActions()}
          </div>
        </header>

        {/* ─── List View (needs scrolling) ─────────────────────────── */}
        {viewMode === "list" && (
          <ScrollArea className="flex-1 w-full bg-muted/10 dark:bg-muted/5">
            <div className="max-w-[1920px] mx-auto w-full p-6 lg:p-8 space-y-6">
              <SnippetFilterBar
                search={search}
                language={language}
                tag={tag}
                sortBy={sortBy}
                filterOptions={filterOptions}
                onSearchChange={setSearch}
                onLanguageChange={(v) => { setLanguage(v); setPage(1); }}
                onTagChange={(v) => { setTag(v); setPage(1); }}
                onSortChange={(v) => { setSortBy(v); setPage(1); }}
                onClearFilters={handleClearFilters}
              />

              {isLoading ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4 gap-6">
                  {Array.from({ length: 12 }).map((_, i) => (
                    <div key={i} className="h-[220px] rounded-xl bg-card border border-border/50 p-5 flex flex-col gap-4 animate-pulse">
                      <div className="flex justify-between items-center">
                        <div className="h-6 w-20 bg-muted rounded" />
                        <div className="h-4 w-16 bg-muted rounded" />
                      </div>
                      <div className="space-y-2">
                        <div className="h-5 w-3/4 bg-muted rounded" />
                        <div className="h-4 w-full bg-muted rounded" />
                        <div className="h-4 w-5/6 bg-muted rounded" />
                      </div>
                      <div className="mt-auto h-4 w-full bg-muted/50 rounded" />
                    </div>
                  ))}
                </div>
              ) : snippets.length === 0 ? (
                <EmptyView hasFilters={hasActiveFilters} onClear={handleClearFilters} onCreate={openCreate} />
              ) : (
                <div className="space-y-8 animate-in fade-in duration-500">
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4 gap-6">
                    {snippets.map((snippet) => (
                      <SnippetCard
                        key={snippet.snippetId}
                        snippet={snippet}
                        onClick={() => handleSelect(snippet)}
                      />
                    ))}
                  </div>

                  {totalPages > 1 && (
                    <div className="flex items-center justify-between bg-card border rounded-xl p-3 shadow-sm">
                      <span className="text-sm text-muted-foreground font-medium pl-2">
                        Showing page {page} of {totalPages}
                      </span>
                      <div className="flex gap-2">
                        <Button variant="outline" size="sm" className="h-8 px-3" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                          <ChevronLeft className="w-4 h-4 mr-1" /> Previous
                        </Button>
                        <Button variant="outline" size="sm" className="h-8 px-3" disabled={page >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>
                          Next <ChevronRight className="w-4 h-4 ml-1" />
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </ScrollArea>
        )}

        {/* ─── Detail View (direct flex child — no ScrollArea wrapper) ── */}
        {viewMode === "detail" && (
          isLoadingDetail ? (
            <div className="flex-1 overflow-y-auto">
              <div className="max-w-5xl mx-auto w-full py-8 px-6 lg:px-8 space-y-6">
                <Skeleton className="w-full h-[80px] rounded-xl" />
                <Skeleton className="w-full h-[600px] rounded-xl" />
              </div>
            </div>
          ) : selectedSnippet ? (
            <DetailView
              snippet={selectedSnippet}
              currentUserId={user?.userId}
              isFavoriting={favoritingId === selectedSnippet.snippetId}
              isEditorFullscreen={isEditorFullscreen}
              onToggleFullscreen={() => setIsEditorFullscreen((v) => !v)}
              onBack={handleBackToList}
              onEdit={openEdit}
              onFavorite={() => handleFavorite(selectedSnippet.snippetId)}
              onCopy={() => handleCopy(selectedSnippet.snippetId)}
              onDelete={() => setDeleteTarget(selectedSnippet)}
            />
          ) : (
            <EmptyView hasFilters={false} onClear={() => { }} onCreate={openCreate} />
          )
        )}

        {/* ─── Edit / Create View (direct flex child — no ScrollArea wrapper) ── */}
        {(viewMode === "create" || viewMode === "edit") && (
          <EditView
            isEditing={viewMode === "edit"}
            editingSnippet={editingSnippet}
            isPending={isPendingMutation}
            onSubmit={viewMode === "edit" ? handleUpdate : handleCreate}
            onCancel={handleCancelEdit}
          />
        )}

        {/* Delete confirmation Dialog */}
        <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete Snippet?</AlertDialogTitle>
              <AlertDialogDescription>
                This will permanently delete "{deleteTarget?.title}". This action cannot be undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleDelete}
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              >
                {deleteMutation.isPending ? "Deleting..." : "Delete Permanently"}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

      </div>
    </TooltipProvider>
  );
}
