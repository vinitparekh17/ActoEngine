// pages/EntityExplorer.tsx
import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useProject } from "@/hooks/useProject";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
    AlertCircle,
    ExternalLink,
} from "lucide-react";
import {
    EntityListPanel,
    EntityDetailsPanel,
    EmptyDetailsState,
    type UnifiedEntity,
    type EntityType,
    type EntityTab,
} from "@/components/explorer";
import { useApi } from "@/hooks/useApi";
import { TableMetadataDto, StoredProcedureMetadataDto } from "@/types/context";

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
    const { projectId, entityType: entityTypeSlug, entityId: entityIdParam, tab } = useParams<{
        projectId: string;
        entityType?: EntityTypeSlug;
        entityId?: string;
        tab?: EntityTab;
    }>();
    const navigate = useNavigate();
    const { selectedProject, selectedProjectId, hasProject, selectProject } = useProject();

    // View mode state
    const [viewMode, setViewMode] = useState<"tree" | "list">("list");

    // Sync route projectId with app state
    useEffect(() => {
        if (projectId && hasProject) {
            const projectIdNum = parseInt(projectId, 10);
            if (!isNaN(projectIdNum) && projectIdNum !== selectedProjectId) {
                // URL has a different project than currently selected, sync it
                selectProject(projectIdNum);
            }
        }
    }, [projectId, selectedProjectId, hasProject, selectProject]);

    // Parse URL params to state
    const selectedEntityType = entityTypeSlug ? slugToType[entityTypeSlug] : undefined;
    const selectedEntityId = entityIdParam ? parseInt(entityIdParam, 10) : undefined;

    // Validate activeTab against allowed EntityTab values
    const validTabs: EntityTab[] = ["overview", "experts", "documentation"];
    const activeTab: EntityTab = tab && validTabs.includes(tab as EntityTab)
        ? (tab as EntityTab)
        : "overview";

    // Selected entity (will be populated when list loads and matches URL)
    const [selectedEntity, setSelectedEntity] = useState<UnifiedEntity | null>(null);

    // Handle entity selection from list
    const handleSelectEntity = useCallback((entity: UnifiedEntity | null) => {
        setSelectedEntity(entity);
        if (entity) {
            navigate(`/project/${projectId}/entities/${typeToSlug[entity.entityType]}/${entity.entityId}/overview`);
        } else {
            navigate(`/project/${projectId}/entities`);
        }
    }, [navigate, projectId]);

    // Handle tab change
    const handleTabChange = useCallback((newTab: EntityTab) => {
        if (selectedEntity) {
            navigate(`/project/${projectId}/entities/${typeToSlug[selectedEntity.entityType]}/${selectedEntity.entityId}/${newTab}`);
        }
    }, [navigate, projectId, selectedEntity]);

    // Handle close details panel
    const handleCloseDetails = useCallback(() => {
        setSelectedEntity(null);
        navigate(`/project/${projectId}/entities`);
    }, [navigate, projectId]);

    // Fetch tables
    const {
        data: tablesData,
        isLoading: isLoadingTables,
        refetch: refetchTables,
    } = useApi<TableMetadataDto[]>(
        `/DatabaseBrowser/projects/${selectedProjectId}/tables`,
        {
            enabled: hasProject && !!selectedProjectId,
            staleTime: 5 * 60 * 1000,
            retry: 2,
        }
    );

    // Fetch stored procedures
    const {
        data: proceduresData,
        isLoading: isLoadingSPs,
        refetch: refetchProcedures,
    } = useApi<StoredProcedureMetadataDto[]>(
        `/DatabaseBrowser/projects/${selectedProjectId}/sp-metadata`,
        {
            enabled: hasProject && !!selectedProjectId,
            staleTime: 5 * 60 * 1000,
            retry: 2,
        }
    );

    // Handle manual refresh
    const handleRefresh = useCallback(() => {
        refetchTables();
        refetchProcedures();
    }, [refetchTables, refetchProcedures]);

    // Sync URL params to selected entity state
    useEffect(() => {
        if (selectedEntityType && selectedEntityId && !isNaN(selectedEntityId)) {
            // Check if we need to update the selected entity
            const needsUpdate = !selectedEntity ||
                selectedEntity.entityId !== selectedEntityId ||
                selectedEntity.entityType !== selectedEntityType ||
                selectedEntity.entityName === "";

            if (needsUpdate) {
                // Try to resolve entity name from loaded data
                let foundName = "";
                let foundSchema = "";

                if (selectedEntityType === "TABLE" && tablesData) {
                    const found = tablesData.find(t => t.tableId === selectedEntityId);
                    if (found) {
                        foundName = found.tableName || "";
                        foundSchema = found.schemaName || "";
                    }
                } else if (selectedEntityType === "SP" && proceduresData) {
                    const found = proceduresData.find(sp => sp.spId === selectedEntityId);
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
                const searchInput = document.querySelector('input[placeholder*="Search"]') as HTMLInputElement;
                if (searchInput) searchInput.focus();
            }
        };

        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, [selectedEntity, handleCloseDetails]);

    // No project selected
    if (!hasProject) {
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
        <div className="h-[calc(100vh-4rem)] flex flex-col p-6">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6 shrink-0">
                <div>
                    <h1 className="text-3xl font-bold">Entity Explorer</h1>
                    <p className="text-muted-foreground mt-1">
                        Browse and manage database entities in{" "}
                        <span className="font-medium">{selectedProject?.projectName}</span>
                    </p>
                </div>

                <div className="flex gap-2">
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
                        selectedEntityId={selectedEntityId}
                        selectedEntityType={selectedEntityType}
                        onSelectEntity={handleSelectEntity}
                        viewMode={viewMode}
                        onViewModeChange={setViewMode}
                        keepSelectedVisible={true}
                        tablesData={tablesData}
                        proceduresData={proceduresData}
                        isLoadingTables={isLoadingTables}
                        isLoadingSPs={isLoadingSPs}
                        onRefresh={handleRefresh}
                    />
                </div>

                {/* Right Panel - Details or Empty State */}
                <div className="overflow-hidden border rounded-lg bg-card">
                    {selectedEntity ? (
                        <EntityDetailsPanel
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
