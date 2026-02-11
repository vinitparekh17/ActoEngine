// components/explorer/EntityOverviewTab.tsx
import React from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { Link } from "react-router-dom";
import { getDefaultSchema } from "@/lib/schema-utils";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Database,
  Table as TableIcon,
  Code2,
  Key,
  ExternalLink,
  Network,
} from "lucide-react";
import { Skeleton } from "@/components/ui/skeletons";
import type { UnifiedEntity } from "./EntityListPanel";

interface TableDetails {
  tableId: number;
  tableName: string;
  schemaName?: string;
  columns: Array<{
    name: string;
    dataType: string;
    constraints?: string[];
  }>;
  primaryKeys?: string[];
}

interface SPDetails {
  storedProcedureId: number;
  procedureName: string;
  schemaName?: string;
  definition?: string;
  createdDate?: string;
  modifiedDate?: string;
  description?: string;
  parameters?: Array<{
    name: string;
    dataType: string;
    direction: string;
    defaultValue?: string;
  }>;
}

interface EntityOverviewTabProps {
  entity: UnifiedEntity;
}

export function EntityOverviewTab({ entity }: EntityOverviewTabProps) {
  const { selectedProject, selectedProjectId, hasProject } = useProject();



  // Fetch table details
  const { data: tableDetails, isLoading: isLoadingTable } =
    useApi<TableDetails>(
      `/DatabaseBrowser/projects/${selectedProjectId}/tables/${entity.entityId}`,
      {
        enabled:
          hasProject && !!selectedProjectId && entity.entityType === "TABLE",
        staleTime: 60 * 1000,
      },
    );

  // Fetch SP details
  const { data: spDetails, isLoading: isLoadingSP } = useApi<SPDetails>(
    `/DatabaseBrowser/projects/${selectedProjectId}/stored-procedures/${entity.entityId}`,
    {
      enabled: hasProject && !!selectedProjectId && entity.entityType === "SP",
      staleTime: 60 * 1000,
    },
  );

  const isLoading =
    entity.entityType === "TABLE"
      ? isLoadingTable
      : entity.entityType === "SP"
        ? isLoadingSP
        : false;

  const getEntityIcon = () => {
    switch (entity.entityType) {
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

  const getDetailRoute = () => {
    const entityTypeSlug = entity.entityType === "TABLE" ? "tables" : "stored-procedures";
    return `/project/${selectedProjectId}/${entityTypeSlug}/${entity.entityId}/detail`;
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
        </div>
        <Skeleton className="h-48" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Quick Stats */}
      <div className="grid grid-cols-2 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium">Type</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center gap-2">
              {getEntityIcon()}
              <span className="font-semibold">
                {entity.entityType === "TABLE"
                  ? "Table"
                  : entity.entityType === "SP"
                    ? "Stored Procedure"
                    : entity.entityType === "COLUMN"
                      ? "Column"
                      : "Entity"}
              </span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium">Schema</CardTitle>
          </CardHeader>
          <CardContent>
            <span className="font-semibold font-mono">
              {entity.schemaName || getDefaultSchema(selectedProject?.databaseType)}
            </span>
          </CardContent>
        </Card>

        {entity.entityType === "TABLE" && tableDetails && (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">Columns</CardTitle>
            </CardHeader>
            <CardContent>
              <span className="text-2xl font-semibold">
                {tableDetails.columns?.length || 0}
              </span>
            </CardContent>
          </Card>
        )}

        {entity.entityType === "TABLE" && tableDetails?.primaryKeys && (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">
                Primary Keys
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-2">
                <Key className="h-4 w-4 text-yellow-600" />
                <span className="font-semibold">
                  {tableDetails.primaryKeys.length}
                </span>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Columns Preview (for tables) */}
      {entity.entityType === "TABLE" && tableDetails?.columns && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Columns Preview</CardTitle>
            <CardDescription>
              First 5 columns of {tableDetails.columns.length} total
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="border rounded-lg overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Column</TableHead>
                    <TableHead>Type</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {tableDetails.columns.slice(0, 5).map((col) => (
                    <TableRow key={col.name}>
                      <TableCell className="font-medium font-mono text-sm">
                        {col.name}
                      </TableCell>
                      <TableCell className="text-muted-foreground text-sm">
                        {col.dataType}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
            {tableDetails.columns.length > 5 && (
              <p className="text-xs text-muted-foreground mt-2">
                ... and {tableDetails.columns.length - 5} more columns
              </p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Parameters (for SPs) */}
      {entity.entityType === "SP" &&
        spDetails?.parameters &&
        spDetails.parameters.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Parameters</CardTitle>
              <CardDescription>
                {spDetails.parameters.length} parameter(s)
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="border rounded-lg overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Parameter</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Direction</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {spDetails.parameters.slice(0, 5).map((param) => (
                      <TableRow key={param.name}>
                        <TableCell className="font-medium font-mono text-sm">
                          {param.name}
                        </TableCell>
                        <TableCell className="text-muted-foreground text-sm">
                          {param.dataType}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className="text-xs">
                            {param.direction}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        )}

      {/* Quick Actions */}
      <div className="flex gap-2 pt-4 border-t">
        <Button asChild variant="outline">
          <Link to={getDetailRoute()}>
            <ExternalLink className="h-4 w-4 mr-2" />
            Full Details
          </Link>
        </Button>
        <Button asChild variant="outline">
          <Link
            to={`/project/${selectedProjectId}/impact/${entity.entityType}/${entity.entityId}`}
          >
            <Network className="h-4 w-4 mr-2" />
            Impact Analysis
          </Link>
        </Button>
      </div>
    </div>
  );
}
