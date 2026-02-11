// components/explorer/EntityDetailsPanel.tsx
import { useProject } from "@/hooks/useProject";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  X,
  Database,
  Table as TableIcon,
  Code2,
  Users,
  FileText,
} from "lucide-react";
import { Skeleton } from "@/components/ui/skeletons";
import { ExpertManagement } from "@/components/context/ExpertManagement";
import { ContextEditor } from "@/components/context/ContextEditorPanel";
import { EntityOverviewTab } from "./EntityOverviewTab";
import type { UnifiedEntity, EntityType } from "./EntityListPanel";

export type EntityTab = "overview" | "experts" | "documentation";

interface EntityDetailsPanelProps {
  entity: UnifiedEntity;
  activeTab: EntityTab;
  onTabChange: (tab: EntityTab) => void;
  onClose: () => void;
  isLoading?: boolean;
}

export function EntityDetailsPanel({
  entity,
  activeTab,
  onTabChange,
  onClose,
  isLoading = false,
}: EntityDetailsPanelProps) {
  const { selectedProjectId } = useProject();

  const getEntityIcon = (type: EntityType) => {
    switch (type) {
      case "TABLE":
        return <TableIcon className="h-5 w-5 text-green-600" />;
      case "SP":
        return <Code2 className="h-5 w-5 text-indigo-600" />;
      case "COLUMN":
        return <Database className="h-5 w-5 text-blue-600" />;
      default:
        return <Database className="h-5 w-5" />;
    }
  };

  const getEntityTypeLabel = (type: EntityType) => {
    switch (type) {
      case "TABLE":
        return "Table";
      case "SP":
        return "Stored Procedure";
      case "COLUMN":
        return "Column";
      default:
        return type;
    }
  };

  if (isLoading) {
    return (
      <div className="h-full flex flex-col bg-background border-l">
        {/* Header skeleton */}
        <div className="border-b p-4">
          <div className="flex items-start justify-between">
            <div className="space-y-2">
              <Skeleton className="h-6 w-48" />
              <Skeleton className="h-4 w-24" />
            </div>
            <Skeleton className="h-8 w-8 rounded" />
          </div>
        </div>
        {/* Content skeleton */}
        <div className="flex-1 p-4 space-y-4">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-48 w-full" />
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-background border-l animate-in slide-in-from-right-5 duration-200">
      {/* Header */}
      <div className="border-b p-4 shrink-0">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            {getEntityIcon(entity.entityType)}
            <div>
              <h2 className="text-lg font-semibold">{entity.entityName}</h2>
              <div className="flex items-center gap-2 mt-1">
                <Badge variant="outline" className="text-xs">
                  {getEntityTypeLabel(entity.entityType)}
                </Badge>
                <span className="text-sm text-muted-foreground">
                  {entity.schemaName || "dbo"}
                </span>
              </div>
            </div>
          </div>
          <Button
            variant="ghost"
            size="icon"
            onClick={onClose}
            className="shrink-0"
            aria-label="Close details"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex-1 overflow-hidden">
        <Tabs
          value={activeTab}
          onValueChange={(v) => onTabChange(v as EntityTab)}
          className="h-full flex flex-col"
        >
          <TabsList className="mx-4 mt-4 shrink-0">
            <TabsTrigger value="overview" className="gap-2">
              <Database className="h-3.5 w-3.5" />
              Overview
            </TabsTrigger>
            <TabsTrigger value="experts" className="gap-2">
              <Users className="h-3.5 w-3.5" />
              Experts
            </TabsTrigger>
            <TabsTrigger value="documentation" className="gap-2">
              <FileText className="h-3.5 w-3.5" />
              Documentation
            </TabsTrigger>
          </TabsList>

          <div className="flex-1 overflow-auto p-4">
            <TabsContent value="overview" className="mt-0 h-full">
              <EntityOverviewTab entity={entity} />
            </TabsContent>

            <TabsContent value="experts" className="mt-0 h-full">
              <ExpertManagement
                entityType={entity.entityType}
                entityId={entity.entityId}
                entityName={entity.entityName}
              />
            </TabsContent>

            <TabsContent value="documentation" className="mt-0 h-full">
              {selectedProjectId ? (
                <ContextEditor
                  projectId={selectedProjectId}
                  entityType={entity.entityType}
                  entityId={entity.entityId}
                  entityName={entity.entityName}
                />
              ) : (
                <div className="flex items-center justify-center h-full text-center p-8">
                  <div className="space-y-2">
                    <p className="text-muted-foreground">No project selected</p>
                    <p className="text-sm text-muted-foreground">
                      Select a project to view and edit documentation
                    </p>
                  </div>
                </div>
              )}
            </TabsContent>
          </div>
        </Tabs>
      </div>
    </div>
  );
}
