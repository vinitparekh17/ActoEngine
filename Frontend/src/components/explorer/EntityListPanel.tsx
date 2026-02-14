// components/explorer/EntityListPanel.tsx
import React, { useMemo } from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { useContextBatch } from "@/hooks/useContext";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { getDefaultSchema } from "@/lib/schema-utils";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import TreeView, { type TreeNode } from "@/components/database/TreeView";
import { InlineContextBadge } from "@/components/context/InlineContextBadge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Search,
  Database,
  Table as TableIcon,
  Code2,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
} from "lucide-react";
import { TableMetadataDto, StoredProcedureMetadataDto } from "@/types/context";
import { Skeleton, TableSkeleton } from "@/components/ui/skeletons";

// Types
export type EntityType = "TABLE" | "SP" | "COLUMN";
export type FilterType = "ALL" | EntityType;
type SortField = "name" | "schema" | "modified";
type SortOrder = "asc" | "desc";

export interface UnifiedEntity {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  schemaName?: string;
  description?: string;
  modifiedDate?: string;
  expertCount?: number;
}

interface EntityListPanelProps {
  selectedEntityId?: number;
  selectedEntityType?: EntityType;
  onSelectEntity: (entity: UnifiedEntity | null) => void;
  viewMode: "tree" | "list";
  onViewModeChange: (mode: "tree" | "list") => void;
  keepSelectedVisible?: boolean;
  // Data props lifted from internal state
  tablesData?: TableMetadataDto[];
  proceduresData?: StoredProcedureMetadataDto[];
  isLoadingTables?: boolean;
  isLoadingSPs?: boolean;
  onRefresh: () => void;
}

export function EntityListPanel({
  selectedEntityId,
  selectedEntityType,
  onSelectEntity,
  viewMode,
  onViewModeChange,
  keepSelectedVisible = true,
  tablesData = [],
  proceduresData = [],
  isLoadingTables = false,
  isLoadingSPs = false,
  onRefresh,
}: EntityListPanelProps) {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  // Local state
  const [searchQuery, setSearchQuery] = React.useState("");
  const [filterType, setFilterType] = React.useState<FilterType>("ALL");
  const [sortBy, setSortBy] = React.useState<SortField>("name");
  const [sortOrder, setSortOrder] = React.useState<SortOrder>("asc");
  const [currentPage, setCurrentPage] = React.useState(1);
  const pageSize = 50;

  // Ref for search input to support Ctrl+K shortcut
  const searchInputRef = React.useRef<HTMLInputElement>(null);

  // Fetch tree data for tree view
  const {
    data: treeDataResponse,
    isLoading: isLoadingTree,
    error: treeError,
  } = useApi<TreeNode>(`/DatabaseBrowser/projects/${selectedProjectId}/tree`, {
    enabled: hasProject && !!selectedProjectId && viewMode === "tree",
    staleTime: 5 * 60 * 1000,
    retry: 2,
  });

  const treeData = treeDataResponse ? [treeDataResponse] : undefined;
  const isLoading =
    isLoadingTables || isLoadingSPs || (viewMode === "tree" && isLoadingTree);

  // Transform data to unified format
  const allEntities = useMemo(() => {
    const entities: UnifiedEntity[] = [];

    if (filterType === "ALL" || filterType === "TABLE") {
      (tablesData || []).forEach((table) => {
        entities.push({
          entityType: "TABLE",
          entityId: table.tableId,
          entityName: table.tableName,
          schemaName: table.schemaName,
          description: undefined,
          modifiedDate: undefined,
        });
      });
    }

    if (filterType === "ALL" || filterType === "SP") {
      (proceduresData || []).forEach((sp) => {
        entities.push({
          entityType: "SP",
          entityId: sp.spId,
          entityName: sp.procedureName,
          schemaName: sp.schemaName,
          description: undefined,
          modifiedDate: undefined,
        });
      });
    }

    return entities;
  }, [tablesData, proceduresData, filterType]);

  // Filter and sort entities
  const filteredEntities = useMemo(() => {
    let filtered = [...allEntities];

    // Client-side search
    if (searchQuery.trim().length > 0) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (entity) =>
          entity.entityName.toLowerCase().includes(query) ||
          entity.schemaName?.toLowerCase().includes(query) ||
          entity.description?.toLowerCase().includes(query),
      );
    }

    // Keep selected entity visible even if filtered out
    if (keepSelectedVisible && selectedEntityId && selectedEntityType) {
      const selectedExists = filtered.some(
        (e) =>
          e.entityId === selectedEntityId &&
          e.entityType === selectedEntityType,
      );
      if (!selectedExists) {
        const selectedEntity = allEntities.find(
          (e) =>
            e.entityId === selectedEntityId &&
            e.entityType === selectedEntityType,
        );
        if (selectedEntity) {
          filtered = [selectedEntity, ...filtered];
        }
      }
    }

    // Sort
    filtered.sort((a, b) => {
      let comparison = 0;
      switch (sortBy) {
        case "name":
          comparison = a.entityName.localeCompare(b.entityName);
          break;
        case "schema":
          {
            const defaultSchema = getDefaultSchema(
              selectedProject?.databaseType,
            );
            comparison = (a.schemaName || defaultSchema).localeCompare(
              b.schemaName || defaultSchema,
            );
          }
          break;
        case "modified": {
          const aRaw = a.modifiedDate ? Date.parse(a.modifiedDate) : 0;
          const bRaw = b.modifiedDate ? Date.parse(b.modifiedDate) : 0;
          comparison = (isNaN(aRaw) ? 0 : aRaw) - (isNaN(bRaw) ? 0 : bRaw);
          break;
        }
      }
      return sortOrder === "asc" ? comparison : -comparison;
    });

    return filtered;
  }, [
    allEntities,
    searchQuery,
    sortBy,
    sortOrder,
    selectedProject?.databaseType,
    keepSelectedVisible,
    selectedEntityId,
    selectedEntityType,
  ]);

  // Pagination
  const totalPages = Math.ceil(filteredEntities.length / pageSize);
  const paginatedEntities = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredEntities.slice(start, start + pageSize);
  }, [filteredEntities, currentPage, pageSize]);

  // Reset page when filters change
  React.useEffect(() => {
    const validPage = totalPages > 0 ? Math.min(currentPage, totalPages) : 1;
    if (validPage !== currentPage) {
      setCurrentPage(validPage);
    }
  }, [searchQuery, filterType, sortBy, sortOrder, totalPages, currentPage]);

  // Batch fetch context for visible entities
  const { data: batchContextData, isLoading: isBatchLoading } = useContextBatch(
    paginatedEntities.map((e) => ({
      entityType: e.entityType,
      entityId: e.entityId,
    })),
    { enabled: paginatedEntities.length > 0 },
  );

  // Map context data for easy lookup
  const contextMap = useMemo(() => {
    if (!batchContextData) return {};
    const map: Record<string, any> = {};
    batchContextData.forEach((ctx) => {
      if (ctx.context?.entityType && ctx.context?.entityId) {
        map[`${ctx.context.entityType}:${ctx.context.entityId}`] = ctx;
      }
    });
    return map;
  }, [batchContextData]);

  // Helper functions
  const getEntityIcon = (type: EntityType) => {
    switch (type) {
      case "TABLE":
        return <TableIcon className="h-4 w-4 text-green-600" />;
      case "SP":
        return <Code2 className="h-4 w-4 text-indigo-600" />;
      case "COLUMN":
        return <Database className="h-4 w-4 text-blue-600" />;
      default:
        return <Database className="h-4 w-4" />;
    }
  };

  const isSelected = (entity: UnifiedEntity) =>
    entity.entityId === selectedEntityId &&
    entity.entityType === selectedEntityType;

  // Keyboard navigation state (must be before any returns)
  const [focusedIndex, setFocusedIndex] = React.useState<number>(-1);
  const listContainerRef = React.useRef<HTMLDivElement>(null);
  const itemRefs = React.useRef<(HTMLTableRowElement | null)[]>([]);

  // Reset focus when list changes
  React.useEffect(() => {
    setFocusedIndex(-1);
  }, [paginatedEntities]);

  // Global keyboard shortcut for Ctrl+K to focus search
  React.useEffect(() => {
    const handleGlobalKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };

    window.addEventListener("keydown", handleGlobalKeyDown);
    return () => window.removeEventListener("keydown", handleGlobalKeyDown);
  }, []);

  // Handle keyboard navigation
  React.useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (viewMode !== "list") return;

      // Only handle nav keys if we aren't typing in an input
      if (document.activeElement?.tagName === "INPUT") {
        if (e.key === "ArrowDown" || e.key === "ArrowUp") {
          return;
        }
        if (e.key === "Enter") return;
      }

      switch (e.key) {
        case "ArrowDown":
          e.preventDefault();
          setFocusedIndex((prev) => {
            const next = prev + 1;
            if (next >= paginatedEntities.length) return prev;
            return next;
          });
          break;
        case "ArrowUp":
          e.preventDefault();
          setFocusedIndex((prev) => {
            const next = prev - 1;
            if (next < 0) return 0;
            return next;
          });
          break;
        case "Enter":
          e.preventDefault();
          if (focusedIndex >= 0 && focusedIndex < paginatedEntities.length) {
            onSelectEntity(paginatedEntities[focusedIndex]);
          }
          break;
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [focusedIndex, paginatedEntities, viewMode, onSelectEntity]);

  // Scroll focused item into view
  React.useEffect(() => {
    if (focusedIndex >= 0 && itemRefs.current[focusedIndex]) {
      itemRefs.current[focusedIndex]?.scrollIntoView({
        block: "nearest",
        behavior: "smooth",
      });
    }
  }, [focusedIndex]);

  // Loading state
  if (isLoading && !allEntities.length) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-full" />
        <TableSkeleton columns={4} rows={8} />
      </div>
    );
  }

  return (
    <div className="space-y-4 h-full flex flex-col">
      {/* Search and Filters */}
      <div className="space-y-3">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            ref={searchInputRef}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search entities... (Ctrl+K)"
            className="pl-10"
          />
        </div>

        <div className="flex flex-wrap gap-2">
          <Select
            value={filterType}
            onValueChange={(v) => setFilterType(v as FilterType)}
          >
            <SelectTrigger className="w-[130px]">
              <SelectValue placeholder="All Types" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Types</SelectItem>
              <SelectItem value="TABLE">Tables</SelectItem>
              <SelectItem value="SP">Stored Procedures</SelectItem>
            </SelectContent>
          </Select>

          <Select
            value={sortBy}
            onValueChange={(v) => setSortBy(v as SortField)}
          >
            <SelectTrigger className="w-[100px]">
              <SelectValue placeholder="Sort" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="name">Name</SelectItem>
              <SelectItem value="schema">Schema</SelectItem>
              <SelectItem value="modified">Modified</SelectItem>
            </SelectContent>
          </Select>

          <div className="flex gap-1 ml-auto">
            <Button
              variant={viewMode === "list" ? "default" : "outline"}
              size="sm"
              onClick={() => onViewModeChange("list")}
            >
              List
            </Button>
            <Button
              variant={viewMode === "tree" ? "default" : "outline"}
              size="sm"
              onClick={() => onViewModeChange("tree")}
            >
              Tree
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={onRefresh}
              title="Refresh"
            >
              <RefreshCw className={`h-4 w-4 ${isLoading ? "animate-spin" : ""}`} />
            </Button>
          </div>
        </div>

        <p className="text-sm text-muted-foreground">
          {filteredEntities.length}{" "}
          {filteredEntities.length === 1 ? "entity" : "entities"}
          {searchQuery && ` matching "${searchQuery}"`}
        </p>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto" ref={listContainerRef}>
        {viewMode === "list" ? (
          <div className="border rounded-lg overflow-hidden">
            {filteredEntities.length === 0 ? (
              <div className="p-4 text-center text-muted-foreground">
                {searchQuery
                  ? "No entities found matching your search."
                  : "No entities found."}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="text-center">Entity</TableHead>
                    <TableHead className="text-center">Context</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {paginatedEntities.map((entity, index) => {
                    const isFocused = index === focusedIndex;
                    const selected = isSelected(entity);
                    return (
                      <TableRow
                        key={`${entity.entityType}-${entity.entityId}`}
                        ref={(el) => {
                          itemRefs.current[index] = el;
                        }}
                        className={`cursor-pointer ${selected
                          ? "bg-accent"
                          : isFocused
                            ? "bg-accent/50"
                            : "hover:bg-accent"
                          }`}
                        onClick={() => onSelectEntity(entity)}
                      >
                        <TableCell className="font-medium">
                          <div className="flex items-center gap-2">
                            {getEntityIcon(entity.entityType)}
                            <div className="flex flex-col">
                              <span className="truncate max-w-[180px]">
                                {entity.entityName}
                              </span>
                              <span className="text-xs text-muted-foreground">
                                {entity.schemaName ||
                                  getDefaultSchema(
                                    selectedProject?.databaseType,
                                  )}
                              </span>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>
                          <InlineContextBadge
                            entityType={entity.entityType}
                            entityId={entity.entityId}
                            entityName={entity.entityName}
                            variant="minimal"
                            preloadedContext={
                              contextMap[
                              `${entity.entityType}:${entity.entityId}`
                              ]
                            }
                            disableFetch={true}
                            loading={isBatchLoading}
                          />
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </div>
        ) : (
          <Card>
            <CardContent className="p-4">
              {isLoadingTree ? (
                <div className="flex flex-col space-y-2">
                  {Array.from({ length: 6 }).map((_, i) => (
                    <div key={i} className="flex gap-2">
                      <Skeleton className="h-4 w-4" />
                      <Skeleton className="h-4 flex-1" />
                    </div>
                  ))}
                </div>
              ) : treeError ? (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    Failed to load database tree.
                  </AlertDescription>
                </Alert>
              ) : treeData ? (
                <TreeView
                  treeData={treeData}
                  searchQuery={searchQuery}
                  onSearchChange={setSearchQuery}
                  persistenceKey={`entity-explorer-tree-${selectedProjectId}`}
                  hideSearch={true}
                  onSelectNode={(node) => {
                    // Map tree node type to UnifiedEntity type
                    let type: EntityType | undefined;
                    if (node.type === "table") type = "TABLE";
                    else if (node.type === "stored-procedure") type = "SP";
                    else if (node.type === "column") type = "COLUMN";

                    if (type) {
                      // Use explicit entityId if available, otherwise fallback to robust regex parsing
                      const entityId =
                        node.entityId ||
                        parseInt(/\b(\d+)$/.exec(node.id)?.[1] || "0", 10);

                      // Legacy fallback just in case
                      if (isNaN(entityId) || entityId === 0) {
                        console.warn("Could not extract entity ID from node", node);
                        return;
                      }
                      // Find full entity details if possible, or construct partial
                      const found = allEntities.find(
                        (e) => e.entityType === type && e.entityId === entityId,
                      );

                      onSelectEntity(
                        found || {
                          entityType: type,
                          entityId: entityId,
                          entityName: node.name,
                          // Schema might be missing if not found in list, fallback
                          schemaName: getDefaultSchema(
                            selectedProject?.databaseType,
                          ),
                        },
                      );
                    }
                  }}
                />
              ) : (
                <div className="text-center text-muted-foreground p-4">
                  No database structure found.
                </div>
              )}
            </CardContent>
          </Card>
        )}
      </div>

      {/* Pagination */}
      {viewMode === "list" && filteredEntities.length > pageSize && (
        <div className="flex justify-between items-center pt-2 border-t">
          <span className="text-sm text-muted-foreground">
            Page {currentPage} of {totalPages}
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              disabled={currentPage === 1}
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
              disabled={currentPage === totalPages}
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
