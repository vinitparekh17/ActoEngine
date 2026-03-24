// pages/EntityExplorer.tsx
import { useState, useEffect, useCallback, useRef, useMemo } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useProject } from "@/hooks/useProject";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { AlertCircle, Database, ExternalLink, X } from "lucide-react";
import {
  EntityListPanel,
  EntityDetailsPanel,
  EmptyDetailsState,
  type UnifiedEntity,
  type EntityType,
  type EntityTab,
} from "@/components/explorer";
import { useApi } from "@/hooks/useApi";
import type { PendingFkCount } from "@/types/er-diagram";
import { TableMetadataDto, StoredProcedureMetadataDto } from "@/types/context";
import { getDefaultSchema } from "@/lib/schema-utils";
import { ResyncEntityDialog } from "@/components/project/ResyncEntityDialog";

// Map URL slugs to entity types
type EntityTypeSlug = "table" | "sp" | "column";

const slugToType: Record<EntityTypeSlug, EntityType> = {
  table: "TABLE",
  sp: "SP",
  column: "COLUMN",
};

const typeToSlug: Record<EntityType, EntityTypeSlug> = {
  TABLE: "table",
  SP: "sp",
  COLUMN: "column",
};

/**
 * Unified Database Entity Explorer page.
 *
 * Features:
 * - Split-panel layout (40% list / 60% details)
 * - Entity list with tree/list view toggle, search, and filters
 * - Details panel with tabs: Overview, Experts, Documentation
 * - RESTful URL structure: /project/:projectId/entities/:entityType/:entityId/:tab?
 * - Keyboard navigation support
 */
export default function EntityExplorer() {
  const {
    projectId,
    entityType: entityTypeSlug,
    entityId: entityIdParam,
    tab,
  } = useParams<{
    projectId: string;
    entityType?: EntityTypeSlug;
    entityId?: string;
    tab?: EntityTab;
  }>();
  const navigate = useNavigate();
  const { selectedProject, hasProject, selectProject } = useProject();
  const hasProjectParam = typeof projectId === "string" && projectId.trim() !== "";
  const parsedRouteProjectId = hasProjectParam
    ? Number.parseInt(projectId, 10)
    : Number.NaN;
  const routeProjectId =
    Number.isInteger(parsedRouteProjectId) && parsedRouteProjectId > 0
      ? parsedRouteProjectId
      : null;
  const hasValidRouteProjectId = routeProjectId != null;
  const hasInvalidRouteProjectId = hasProjectParam && !hasValidRouteProjectId;
  const routeProjectRef = useRef<number | null>(null);
  const routeProject =
    hasValidRouteProjectId && selectedProject?.projectId === routeProjectId
      ? selectedProject
      : null;
  const entityExplorerBasePath = hasValidRouteProjectId
    ? `/project/${routeProjectId}/entities`
    : "/projects";

  // View mode state
  const [viewMode, setViewMode] = useState<"tree" | "list">("list");

  // Sync route projectId with app state
  useEffect(() => {
    if (!hasValidRouteProjectId) {
      routeProjectRef.current = null;
      return;
    }

    if (routeProjectRef.current === routeProjectId) {
      return;
    }

    routeProjectRef.current = routeProjectId;
    selectProject(routeProjectId);
  }, [hasValidRouteProjectId, routeProjectId, selectProject]);

  // Parse URL params to state
  const selectedEntityType = entityTypeSlug
    ? slugToType[entityTypeSlug]
    : undefined;
  const selectedEntityId = entityIdParam
    ? parseInt(entityIdParam, 10)
    : undefined;

  // Validate activeTab against allowed EntityTab values
  const validTabs: EntityTab[] = ["overview", "experts", "documentation"];
  const activeTab: EntityTab =
    tab && validTabs.includes(tab as EntityTab)
      ? (tab as EntityTab)
      : "overview";

  // Selected entity (will be populated when list loads and matches URL)
  const [selectedEntity, setSelectedEntity] = useState<UnifiedEntity | null>(
    null,
  );
  const [selectedForResync, setSelectedForResync] = useState<
    Record<string, UnifiedEntity>
  >({});

  const getResyncKey = useCallback(
    (entity: UnifiedEntity) => `${entity.entityType}:${entity.entityId}`,
    [],
  );

  const selectedForResyncKeys = useMemo(
    () => new Set(Object.keys(selectedForResync)),
    [selectedForResync],
  );

  const selectedResyncEntities = useMemo(
    () => Object.values(selectedForResync),
    [selectedForResync],
  );

  const effectiveProjectId = routeProjectId ?? 0;

  // Handle entity selection from list
  const handleSelectEntity = useCallback(
    (entity: UnifiedEntity | null) => {
      setSelectedEntity(entity);
      if (entity) {
        navigate(
          `${entityExplorerBasePath}/${typeToSlug[entity.entityType]}/${entity.entityId}/overview`,
        );
      } else {
        navigate(entityExplorerBasePath);
      }
    },
    [entityExplorerBasePath, navigate],
  );

  // Handle tab change
  const handleTabChange = useCallback(
    (newTab: EntityTab) => {
      if (selectedEntity) {
        navigate(
          `${entityExplorerBasePath}/${typeToSlug[selectedEntity.entityType]}/${selectedEntity.entityId}/${newTab}`,
        );
      }
    },
    [entityExplorerBasePath, navigate, selectedEntity],
  );

  // Handle close details panel
  const handleCloseDetails = useCallback(() => {
    setSelectedEntity(null);
    navigate(entityExplorerBasePath);
  }, [entityExplorerBasePath, navigate]);

  const handleToggleResyncSelection = useCallback(
    (entity: UnifiedEntity, checked: boolean) => {
      if (entity.entityType !== "TABLE" && entity.entityType !== "SP") return;

      const key = getResyncKey(entity);
      setSelectedForResync((prev) => {
        if (checked) {
          return { ...prev, [key]: entity };
        }

        const updated = { ...prev };
        delete updated[key];
        return updated;
      });
    },
    [getResyncKey],
  );

  const handleToggleAllFilteredResync = useCallback(
    (entities: UnifiedEntity[], checked: boolean) => {
      setSelectedForResync((prev) => {
        const updated = { ...prev };
        entities.forEach((entity) => {
          if (entity.entityType !== "TABLE" && entity.entityType !== "SP") return;

          const key = getResyncKey(entity);
          if (checked) {
            updated[key] = entity;
          } else {
            delete updated[key];
          }
        });
        return updated;
      });
    },
    [getResyncKey],
  );

  const clearResyncSelection = useCallback(() => {
    setSelectedForResync({});
  }, []);

  // Fetch tables
  const {
    data: tablesData,
    isLoading: isLoadingTables,
    refetch: refetchTables,
  } = useApi<TableMetadataDto[]>(
    hasValidRouteProjectId
      ? `/DatabaseBrowser/projects/${routeProjectId}/tables`
      : "",
    {
      enabled: hasValidRouteProjectId,
      staleTime: 5 * 60 * 1000,
      retry: 2,
    },
  );

  // Fetch stored procedures
  const {
    data: proceduresData,
    isLoading: isLoadingSPs,
    refetch: refetchProcedures,
  } = useApi<StoredProcedureMetadataDto[]>(
    hasValidRouteProjectId
      ? `/DatabaseBrowser/projects/${routeProjectId}/sp-metadata`
      : "",
    {
      enabled: hasValidRouteProjectId,
      staleTime: 5 * 60 * 1000,
      retry: 2,
    },
  );

  // Fetch pending FK counts
  const {
    data: pendingFkData,
    refetch: refetchPendingFks,
  } = useApi<PendingFkCount[]>(
    hasValidRouteProjectId
      ? `/logical-fks/${routeProjectId}/pending-counts`
      : "",
    {
      enabled: hasValidRouteProjectId,
      staleTime: 2 * 60 * 1000, // 2 mins
    }
  );

  const pendingFkMap = useMemo(() => {
    const map = new Map<number, number>();
    if (pendingFkData) {
      pendingFkData.forEach((item) => {
        map.set(item.tableId, item.pendingCount);
      });
    }
    return map;
  }, [pendingFkData]);

  const resyncDialogEntities = useMemo(
    () =>
      selectedResyncEntities.map((entity) => ({
        entityType: entity.entityType as "TABLE" | "SP",
        schemaName:
          entity.schemaName || getDefaultSchema(routeProject?.databaseType),
        entityName: entity.entityName,
      })),
    [routeProject?.databaseType, selectedResyncEntities],
  );

  // Handle manual refresh
  const handleRefresh = useCallback(() => {
    refetchTables();
    refetchProcedures();
    refetchPendingFks();
  }, [refetchTables, refetchProcedures, refetchPendingFks]);

  // Sync URL params to selected entity state
  useEffect(() => {
    if (selectedEntityType && selectedEntityId && !isNaN(selectedEntityId)) {
      // Check if we need to update the selected entity
      const needsUpdate =
        !selectedEntity ||
        selectedEntity.entityId !== selectedEntityId ||
        selectedEntity.entityType !== selectedEntityType ||
        selectedEntity.entityName === "";

      if (needsUpdate) {
        // Try to resolve entity name from loaded data
        let foundName = "";
        let foundSchema = "";

        if (selectedEntityType === "TABLE" && tablesData) {
          const found = tablesData.find((t) => t.tableId === selectedEntityId);
          if (found) {
            foundName = found.tableName || "";
            foundSchema = found.schemaName || "";
          }
        } else if (selectedEntityType === "SP" && proceduresData) {
          const found = proceduresData.find(
            (sp) => sp.spId === selectedEntityId,
          );
          if (found) {
            foundName = found.procedureName || "";
            foundSchema = found.schemaName || "";
          }
        }

        // Update state
        setSelectedEntity({
          entityType: selectedEntityType,
          entityId: selectedEntityId,
          entityName: foundName, // Empty string triggers skeleton loading
          schemaName: foundSchema,
        });
      }
    } else if (selectedEntity) {
      // URL cleared, deselect
      setSelectedEntity(null);
    }
  }, [selectedEntityType, selectedEntityId, tablesData, proceduresData]);

  // Clear stale selection when project changes
  useEffect(() => {
    setSelectedForResync({});
  }, [routeProjectId]);

  // Keyboard navigation
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Escape to close details panel
      if (e.key === "Escape" && selectedEntity) {
        handleCloseDetails();
      }

      // Ctrl+K to focus search (handled globally or by list panel, but keep safety here)
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        const searchInput = document.querySelector(
          'input[placeholder*="Search"]',
        ) as HTMLInputElement;
        if (searchInput) searchInput.focus();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [selectedEntity, handleCloseDetails]);

  if (hasInvalidRouteProjectId) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            The route includes an invalid project ID.
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button asChild variant="outline">
            <Link to="/projects">Back to Projects</Link>
          </Button>
        </div>
      </div>
    );
  }

  // No project selected
  if (!hasValidRouteProjectId && !hasProject) {
    return (
      <div className="space-y-6 p-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Please select a project to browse entities.
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button asChild>
            <Link to="/projects">Select Project</Link>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="h-[calc(100vh-114px)] flex flex-col p-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6 shrink-0">
        <div>
          <h1 className="text-3xl font-bold">Entity Explorer</h1>
          <p className="text-muted-foreground mt-1">
            Browse and manage database entities in{" "}
            <span className="font-medium">
              {routeProject?.projectName || "Loading project..."}
            </span>
          </p>
        </div>

        <div className="flex gap-2">
          {selectedResyncEntities.length > 0 ? (
            <Button variant="outline" onClick={clearResyncSelection}>
              <X className="w-4 h-4 mr-2" />
              Clear Selection ({selectedResyncEntities.length})
            </Button>
          ) : null}

          <ResyncEntityDialog
            projectId={effectiveProjectId}
            entities={resyncDialogEntities}
            onSuccess={clearResyncSelection}
            trigger={
              <Button
                variant="outline"
                disabled={
                  selectedResyncEntities.length === 0 || !effectiveProjectId
                }
              >
                <Database className="w-4 h-4 mr-2" />
                Resync Selected
                {selectedResyncEntities.length > 0
                  ? ` (${selectedResyncEntities.length})`
                  : ""}
              </Button>
            }
          />

          <Button asChild>
            <Link to="/">
              <ExternalLink className="w-4 h-4 mr-2" />
              Dashboard
            </Link>
          </Button>
        </div>
      </div>

      {/* Main Content - Split Panel */}
      <div className="flex-1 min-h-0 grid grid-cols-1 lg:grid-cols-[minmax(300px,40%)_1fr] gap-6">
        {/* Left Panel - Entity List */}
        <div className="overflow-hidden border rounded-lg p-4 bg-card">
          <EntityListPanel
            projectId={routeProjectId!}
            databaseType={routeProject?.databaseType}
            selectedEntityId={selectedEntityId}
            selectedEntityType={selectedEntityType}
            onSelectEntity={handleSelectEntity}
            selectedForResync={selectedForResyncKeys}
            onToggleResyncSelection={handleToggleResyncSelection}
            onToggleAllFilteredResync={handleToggleAllFilteredResync}
            viewMode={viewMode}
            onViewModeChange={setViewMode}
            keepSelectedVisible={true}
            tablesData={tablesData}
            proceduresData={proceduresData}
            pendingFkCounts={pendingFkMap}
            isLoadingTables={isLoadingTables}
            isLoadingSPs={isLoadingSPs}
            onRefresh={handleRefresh}
          />
        </div>

        {/* Right Panel - Details or Empty State */}
        <div className="overflow-hidden border rounded-lg bg-card">
          {selectedEntity ? (
            <EntityDetailsPanel
              projectId={routeProjectId!}
              entity={selectedEntity}
              activeTab={activeTab}
              onTabChange={handleTabChange}
              onClose={handleCloseDetails}
              isLoading={selectedEntity.entityName === ""}
            />
          ) : (
            <EmptyDetailsState />
          )}
        </div>
      </div>
    </div>
  );
}
