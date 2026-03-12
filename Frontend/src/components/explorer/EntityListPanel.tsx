import React, { useMemo, useCallback, memo, useRef, useEffect, useState } from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { useContextBatch } from "@/hooks/useContext";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
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
  Link2,
} from "lucide-react";
import { TableMetadataDto, StoredProcedureMetadataDto } from "@/types/context";
import { Skeleton, TableSkeleton } from "@/components/ui/skeletons";

// --- Fix 4: Move icon maps outside component ---
const ENTITY_ICONS = {
  TABLE: <TableIcon className="h-4 w-4 text-green-600" />,
  SP: <Code2 className="h-4 w-4 text-indigo-600" />,
  COLUMN: <Database className="h-4 w-4 text-blue-600" />,
};

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
  selectedForResync?: Set<string>;
  onToggleResyncSelection?: (entity: UnifiedEntity, checked: boolean) => void;
  onToggleAllFilteredResync?: (
    entities: UnifiedEntity[],
    checked: boolean,
  ) => void;
  viewMode: "tree" | "list";
  onViewModeChange: (mode: "tree" | "list") => void;
  keepSelectedVisible?: boolean;
  tablesData?: TableMetadataDto[];
  proceduresData?: StoredProcedureMetadataDto[];
  pendingFkCounts?: Map<number, number>;
  isLoadingTables?: boolean;
  isLoadingSPs?: boolean;
  onRefresh: () => void;
}

interface EntityRowProps {
  entity: UnifiedEntity;
  index: number;
  isFocused: boolean;
  isSelected: boolean;
  pendingCount: number | undefined;
  isBulkSelectionEnabled: boolean;
  isCheckedForResync: boolean;
  onSelect: (entity: UnifiedEntity) => void;
  onToggleResync?: (entity: UnifiedEntity, checked: boolean) => void;
  itemRefs: React.MutableRefObject<(HTMLTableRowElement | null)[]>;
  defaultSchema: string;
  contextData: any;
  isBatchLoading: boolean;
}

// Memoized row: only selection changes/focus changes affect specific rows
const EntityRow = memo(function EntityRow({
  entity,
  index,
  isFocused,
  isSelected,
  pendingCount,
  isBulkSelectionEnabled,
  isCheckedForResync,
  onSelect,
  onToggleResync,
  itemRefs,
  defaultSchema,
  contextData,
  isBatchLoading,
}: EntityRowProps) {
  return (
    <TableRow
      ref={(el) => {
        itemRefs.current[index] = el;
      }}
      className={`cursor-pointer ${isSelected ? "bg-accent" : isFocused ? "bg-accent/50" : "hover:bg-accent"
        }`}
      onClick={() => onSelect(entity)}
    >
      {isBulkSelectionEnabled ? (
        <TableCell className="w-10" onClick={(e) => e.stopPropagation()}>
          <div className="flex justify-center">
            <Checkbox
              checked={isCheckedForResync}
              onCheckedChange={(checked) => onToggleResync?.(entity, checked === true)}
              aria-label={`Select ${entity.entityName} for resync`}
            />
          </div>
        </TableCell>
      ) : null}
      <TableCell className="font-medium">
        <div className="flex items-center gap-2">
          {ENTITY_ICONS[entity.entityType] ?? <Database className="h-4 w-4" />}
          <div className="flex flex-col">
            <span className="truncate max-w-[180px]">{entity.entityName}</span>
            <span className="text-xs text-muted-foreground flex items-center gap-2">
              <span>{entity.schemaName || defaultSchema}</span>
              {entity.entityType === "TABLE" && pendingCount ? (
                <span className="flex items-center text-amber-600 bg-amber-500/10 px-1.5 py-0.5 rounded-sm text-[10px] font-medium border border-amber-500/20">
                  <Link2 className="w-3 h-3 mr-1" />
                  {pendingCount} Pending
                </span>
              ) : null}
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
          preloadedContext={contextData}
          disableFetch={true}
          loading={isBatchLoading}
        />
      </TableCell>
    </TableRow>
  );
});

export function EntityListPanel({
  selectedEntityId,
  selectedEntityType,
  onSelectEntity,
  selectedForResync = new Set<string>(),
  onToggleResyncSelection,
  onToggleAllFilteredResync,
  viewMode,
  onViewModeChange,
  keepSelectedVisible = true,
  tablesData = [],
  proceduresData = [],
  pendingFkCounts = new Map(),
  isLoadingTables = false,
  isLoadingSPs = false,
  onRefresh,
}: EntityListPanelProps) {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  const [searchQuery, setSearchQuery] = useState("");
  const [filterType, setFilterType] = useState<FilterType>("ALL");
  const [sortBy, setSortBy] = useState<SortField>("name");
  const [sortOrder, setSortOrder] = useState<SortOrder>("asc");
  const [currentPage, setCurrentPage] = useState(1);
  const [focusedIndex, setFocusedIndex] = useState<number>(-1);
  const pageSize = 50;

  const searchInputRef = useRef<HTMLInputElement>(null);
  const listContainerRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<(HTMLTableRowElement | null)[]>([]);

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
  const isLoading = isLoadingTables || isLoadingSPs || (viewMode === "tree" && isLoadingTree);

  const allEntities = useMemo(() => {
    const entities: UnifiedEntity[] = [];
    if (filterType === "ALL" || filterType === "TABLE") {
      tablesData.forEach((table) => {
        const modifiedDate =
          (table as any).modifiedDate ??
          (table as any).updatedAt ??
          (table as any).modifiedAt;
        entities.push({
          entityType: "TABLE",
          entityId: table.tableId,
          entityName: table.tableName,
          schemaName: table.schemaName,
          modifiedDate,
        });
      });
    }
    if (filterType === "ALL" || filterType === "SP") {
      proceduresData.forEach((sp) => {
        const modifiedDate =
          (sp as any).modifiedDate ??
          (sp as any).updatedAt ??
          (sp as any).modifiedAt;
        entities.push({
          entityType: "SP",
          entityId: sp.spId,
          entityName: sp.procedureName,
          schemaName: sp.schemaName,
          modifiedDate,
        });
      });
    }
    return entities;
  }, [tablesData, proceduresData, filterType]);

  // --- Fix 3: Decouple sortedFilteredEntities from selection ---
  const sortedFilteredEntities = useMemo(() => {
    let filtered = [...allEntities];
    if (searchQuery.trim().length > 0) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(
        (e) =>
          e.entityName.toLowerCase().includes(query) ||
          e.schemaName?.toLowerCase().includes(query)
      );
    }

    filtered.sort((a, b) => {
      let comparison = 0;
      switch (sortBy) {
        case "name": {
          comparison = a.entityName.localeCompare(b.entityName);
          break;
        }
        case "schema": {
          const defSchema = getDefaultSchema(selectedProject?.databaseType);
          comparison = (a.schemaName || defSchema).localeCompare(b.schemaName || defSchema);
          break;
        }
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
  }, [allEntities, searchQuery, sortBy, sortOrder, selectedProject?.databaseType]);

  // Handle "keep selected visible" without breaking sortedFilteredEntities reference
  const filteredEntities = useMemo(() => {
    if (!keepSelectedVisible || !selectedEntityId || !selectedEntityType) {
      return sortedFilteredEntities;
    }
    const selectedExists = sortedFilteredEntities.some(
      (e) => e.entityId === selectedEntityId && e.entityType === selectedEntityType
    );
    if (selectedExists) return sortedFilteredEntities;

    const pinned = allEntities.find(
      (e) => e.entityId === selectedEntityId && e.entityType === selectedEntityType
    );
    return pinned ? [pinned, ...sortedFilteredEntities] : sortedFilteredEntities;
  }, [sortedFilteredEntities, keepSelectedVisible, selectedEntityId, selectedEntityType, allEntities]);

  const totalPages = Math.ceil(filteredEntities.length / pageSize);
  const paginatedEntities = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredEntities.slice(start, start + pageSize);
  }, [filteredEntities, currentPage]);

  useEffect(() => {
    setCurrentPage((prev) => {
      const clamped = Math.max(1, Math.min(prev, totalPages || 1));
      return prev === clamped ? prev : clamped;
    });
  }, [totalPages]);

  // --- Fix 1: Memoize the batch input with a stable string key ---
  const batchInput = useMemo(
    () => paginatedEntities.map((e) => ({ entityType: e.entityType, entityId: e.entityId })),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [paginatedEntities.map((e) => `${e.entityType}:${e.entityId}`).join(",")]
  );

  const { data: batchContextData, isLoading: isBatchLoading } = useContextBatch(
    batchInput,
    { enabled: batchInput.length > 0 }
  );

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

  const isSelected = useCallback(
    (entity: UnifiedEntity) =>
      entity.entityId === selectedEntityId && entity.entityType === selectedEntityType,
    [selectedEntityId, selectedEntityType]
  );

  const getResyncKey = useCallback((entity: UnifiedEntity) => `${entity.entityType}:${entity.entityId}`, []);
  const isBulkSelectionEnabled = Boolean(onToggleResyncSelection && onToggleAllFilteredResync);

  const selectableEntitiesForBulk = useMemo(
    () => filteredEntities.filter((e) => e.entityType === "TABLE" || e.entityType === "SP"),
    [filteredEntities]
  );

  const selectedVisibleCount = useMemo(
    () => selectableEntitiesForBulk.filter((e) => selectedForResync.has(getResyncKey(e))).length,
    [selectableEntitiesForBulk, selectedForResync, getResyncKey]
  );

  const allVisibleSelected = selectableEntitiesForBulk.length > 0 && selectedVisibleCount === selectableEntitiesForBulk.length;
  const someVisibleSelected = selectedVisibleCount > 0 && !allVisibleSelected;

  // --- Fix 2: Keyboard ref pattern for zero listener churn ---
  const focusedIndexRef = useRef(focusedIndex);
  const paginatedEntitiesRef = useRef(paginatedEntities);
  const onSelectEntityRef = useRef(onSelectEntity);

  useEffect(() => { focusedIndexRef.current = focusedIndex; }, [focusedIndex]);
  useEffect(() => { paginatedEntitiesRef.current = paginatedEntities; }, [paginatedEntities]);
  useEffect(() => { onSelectEntityRef.current = onSelectEntity; }, [onSelectEntity]);

  useEffect(() => {
    if (viewMode !== "list") return;
    const listContainer = listContainerRef.current;
    if (!listContainer) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.defaultPrevented) return;

      const activeElement = document.activeElement as HTMLElement | null;
      if (activeElement) {
        const tagName = activeElement.tagName;
        const isCheckboxInput =
          tagName === "INPUT" &&
          (activeElement as HTMLInputElement).type?.toLowerCase() === "checkbox";
        const isInteractive =
          tagName === "INPUT" ||
          tagName === "TEXTAREA" ||
          tagName === "SELECT" ||
          tagName === "BUTTON" ||
          isCheckboxInput ||
          activeElement.getAttribute("role") === "checkbox" ||
          activeElement.getAttribute("contenteditable") === "true";
        if (isInteractive) return;
      }

      const entities = paginatedEntitiesRef.current;
      const idx = focusedIndexRef.current;

      switch (e.key) {
        case "ArrowDown":
          e.preventDefault();
          setFocusedIndex((prev) => Math.min(prev + 1, entities.length - 1));
          break;
        case "ArrowUp":
          e.preventDefault();
          setFocusedIndex((prev) => Math.max(prev - 1, 0));
          break;
        case "Enter":
          e.preventDefault();
          if (idx >= 0 && idx < entities.length) {
            onSelectEntityRef.current(entities[idx]);
          }
          break;
      }
    };
    listContainer.addEventListener("keydown", handleKeyDown);
    return () => listContainer.removeEventListener("keydown", handleKeyDown);
  }, [viewMode]);

  useEffect(() => {
    setFocusedIndex(-1);
  }, [paginatedEntities]);

  useEffect(() => {
    if (focusedIndex >= 0 && itemRefs.current[focusedIndex]) {
      itemRefs.current[focusedIndex]?.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }
  }, [focusedIndex]);

  useEffect(() => {
    const handleGlobalK = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        searchInputRef.current?.focus();
      }
    };
    window.addEventListener("keydown", handleGlobalK);
    return () => window.removeEventListener("keydown", handleGlobalK);
  }, []);

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
          <Select value={filterType} onValueChange={(v) => setFilterType(v as FilterType)}>
            <SelectTrigger className="w-[130px]">
              <SelectValue placeholder="All Types" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="ALL">All Types</SelectItem>
              <SelectItem value="TABLE">Tables</SelectItem>
              <SelectItem value="SP">Stored Procedures</SelectItem>
            </SelectContent>
          </Select>

          <Select value={sortBy} onValueChange={(v) => setSortBy(v as SortField)}>
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
            <Button variant={viewMode === "list" ? "default" : "outline"} size="sm" onClick={() => onViewModeChange("list")}>List</Button>
            <Button variant={viewMode === "tree" ? "default" : "outline"} size="sm" onClick={() => onViewModeChange("tree")}>Tree</Button>
            <Button variant="outline" size="sm" onClick={onRefresh}>
              <RefreshCw className={`h-4 w-4 ${isLoading ? "animate-spin" : ""}`} />
            </Button>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-auto" ref={listContainerRef} tabIndex={0}>
        {viewMode === "list" ? (
          <div className="border rounded-lg overflow-hidden">
            {filteredEntities.length === 0 ? (
              <div className="p-4 text-center text-muted-foreground">No entities found.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    {isBulkSelectionEnabled && (
                      <TableHead className="w-10 text-center">
                        <Checkbox
                          checked={allVisibleSelected ? true : someVisibleSelected ? "indeterminate" : false}
                          onCheckedChange={(c) => onToggleAllFilteredResync?.(selectableEntitiesForBulk, c === true)}
                          aria-label="Select all visible entities"
                        />
                      </TableHead>
                    )}
                    <TableHead className="text-center">Entity</TableHead>
                    <TableHead className="text-center">Context</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {paginatedEntities.map((entity, index) => (
                    <EntityRow
                      key={`${entity.entityType}-${entity.entityId}`}
                      entity={entity}
                      index={index}
                      isFocused={index === focusedIndex}
                      isSelected={isSelected(entity)}
                      pendingCount={pendingFkCounts.get(entity.entityId)}
                      isBulkSelectionEnabled={isBulkSelectionEnabled}
                      isCheckedForResync={selectedForResync.has(getResyncKey(entity))}
                      onSelect={onSelectEntity}
                      onToggleResync={onToggleResyncSelection}
                      itemRefs={itemRefs}
                      defaultSchema={getDefaultSchema(selectedProject?.databaseType)}
                      contextData={contextMap[`${entity.entityType}:${entity.entityId}`]}
                      isBatchLoading={isBatchLoading}
                    />
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        ) : (
          <Card>
            <CardContent className="p-4">
              {treeError ? (
                <Alert variant="destructive">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>Failed to load database tree.</AlertDescription>
                </Alert>
              ) : treeData ? (
                <TreeView
                  treeData={treeData}
                  searchQuery={searchQuery}
                  onSearchChange={setSearchQuery}
                  persistenceKey={`entity-explorer-tree-${selectedProjectId}`}
                  hideSearch={true}
                  onSelectNode={(node) => {
                    let type: EntityType | undefined;
                    if (node.type === "table") type = "TABLE";
                    else if (node.type === "stored-procedure") type = "SP";
                    else if (node.type === "column") type = "COLUMN";

                    if (type) {
                      const entityId = node.entityId || parseInt(/\b(\d+)$/.exec(node.id)?.[1] || "0", 10);
                      const found = allEntities.find((e) => e.entityType === type && e.entityId === entityId);
                      onSelectEntity(found || { entityType: type, entityId, entityName: node.name, schemaName: getDefaultSchema(selectedProject?.databaseType) });
                    }
                  }}
                />
              ) : null}
            </CardContent>
          </Card>
        )}
      </div>

      {viewMode === "list" && filteredEntities.length > pageSize && (
        <div className="flex justify-between items-center pt-2 border-t">
          <span className="text-sm text-muted-foreground">Page {currentPage} of {totalPages}</span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => setCurrentPage((p) => Math.max(1, p - 1))} disabled={currentPage === 1}><ChevronLeft className="h-4 w-4" /></Button>
            <Button variant="outline" size="sm" onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))} disabled={currentPage === totalPages}><ChevronRight className="h-4 w-4" /></Button>
          </div>
        </div>
      )}
    </div>
  );
}
