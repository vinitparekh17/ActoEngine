// pages/context/ContextBrowse.tsx
import React, { useState, useMemo } from "react";
import { format, parseISO, isValid } from "date-fns";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { useContextBatch } from "@/hooks/useContext";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import TreeView, { type TreeNode } from "@/components/database/TreeView";
import { InlineContextBadge } from "@/components/context/InlineContextBadge";
import { ContextCoverageWidget } from "@/components/context/ContextCoverageWidget";
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
  ArrowUpDown,
  ExternalLink,
  RefreshCw,
  Funnel,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import { Link } from "react-router-dom";
import {
  TableMetadataDto,
  StoredProcedureMetadataDto,
  ColumnMetadataDto,
} from "@/types/context";
import { PageHeaderSkeleton, Skeleton, TableSkeleton } from "@/components/ui/skeletons";

// Type aliases for cleaner code
type TableMetadata = TableMetadataDto;
type StoredProcedureMetadata = StoredProcedureMetadataDto;
type ColumnMetadata = ColumnMetadataDto;

type EntityType = "TABLE" | "SP" | "COLUMN";
type FilterType = "ALL" | EntityType;
type SortField = "name" | "schema" | "modified";
type SortOrder = "asc" | "desc";

interface UnifiedEntity {
  entityType: EntityType;
  entityId: number;
  entityName: string;
  schemaName?: string;
  description?: string;
  modifiedDate?: string;
}

/**
 * Render the Browse Context page for inspecting database entities and their documentation.
 *
 * Displays tables, stored procedures, and a hierarchical database tree for the selected project.
 * Provides list and tree views, client-side search, type filters, sorting (name/schema/modified), and a refresh action.
 *
 * @returns The JSX element for the Browse Context page.
 */
export default function ContextBrowse() {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  // State
  const [view, setView] = useState<"tree" | "list">("list");
  const [searchQuery, setSearchQuery] = useState("");
  const [showFilters, setShowFilters] = useState(false);
  const [filterType, setFilterType] = useState<FilterType>("ALL");
  const [sortBy, setSortBy] = useState<SortField>("name");
  const [sortOrder, setSortOrder] = useState<SortOrder>("asc");
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 50;

  // Fetch tables
  const {
    data: tablesData,
    isLoading: isLoadingTables,
    error: tablesError,
    refetch: refetchTables,
  } = useApi<TableMetadata[]>(
    `/DatabaseBrowser/projects/${selectedProjectId}/tables`,
    {
      enabled:
        hasProject &&
        !!selectedProjectId &&
        (filterType === "ALL" || filterType === "TABLE"),
      staleTime: 5 * 60 * 1000, // 5 minutes - tables don't change often
      retry: 2,
    },
  );

  // Fetch stored procedures
  const {
    data: proceduresData,
    isLoading: isLoadingSPs,
    error: proceduresError,
    refetch: refetchProcedures,
  } = useApi<StoredProcedureMetadata[]>(
    `/DatabaseBrowser/projects/${selectedProjectId}/sp-metadata`,
    {
      enabled:
        hasProject &&
        !!selectedProjectId &&
        (filterType === "ALL" || filterType === "SP"),
      staleTime: 5 * 60 * 1000, // 5 minutes - stored procedures don't change often
      retry: 2,
    },
  );

  // Fetch tree data for tree view
  const {
    data: treeDataResponse,
    isLoading: isLoadingTree,
    error: treeError,
    refetch: refetchTree,
  } = useApi<TreeNode>(`/DatabaseBrowser/projects/${selectedProjectId}/tree`, {
    enabled: hasProject && !!selectedProjectId && view === "tree",
    staleTime: 5 * 60 * 1000,
    retry: 2,
  });

  const treeData = treeDataResponse ? [treeDataResponse] : undefined;
  const isLoading =
    isLoadingTables || isLoadingSPs || (view === "tree" && isLoadingTree);
  const hasErrors =
    tablesError || proceduresError || (view === "tree" && treeError);

  // Transform data to unified format
  const allEntities = useMemo(() => {
    const entities: UnifiedEntity[] = [];

    // Add tables
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

    // Add stored procedures
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
        case "modified":
          const aRaw = a.modifiedDate ? Date.parse(a.modifiedDate) : 0;
          const bRaw = b.modifiedDate ? Date.parse(b.modifiedDate) : 0;
          const aTime = Number.isNaN(aRaw) ? 0 : aRaw;
          const bTime = Number.isNaN(bRaw) ? 0 : bRaw;
          comparison = aTime - bTime;
          break;
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
  ]);

  // Pagination Logic
  const totalPages = Math.ceil(filteredEntities.length / pageSize);
  const paginatedEntities = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredEntities.slice(start, start + pageSize);
  }, [filteredEntities, currentPage, pageSize]);

  // Reset page when filters change and clamp to valid range when data changes
  React.useEffect(() => {
    // Clamp currentPage to valid range (1 to totalPages)
    // This handles cases where:
    // 1. Filters change and reduce the result set
    // 2. Refresh occurs and data size changes
    // 3. Current page becomes out of range
    const validPage = totalPages > 0 ? Math.min(currentPage, totalPages) : 1;
    if (validPage !== currentPage) {
      setCurrentPage(validPage);
    }
  }, [searchQuery, filterType, sortBy, sortOrder, totalPages]);

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

  const getEntityRoute = (entity: UnifiedEntity) => {
    switch (entity.entityType) {
      case "TABLE":
        return `/project/${selectedProjectId}/tables/${entity.entityId}`;
      case "SP":
        return `/project/${selectedProjectId}/stored-procedures/${entity.entityId}`;
      case "COLUMN":
        return `/project/${selectedProjectId}/columns/${entity.entityId}`;
      default:
        return `/project/${selectedProjectId}`;
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

  // Database-aware default schema helper
  const getDefaultSchema = (dbType?: string) => {
    const t = (dbType || "").toLowerCase();
    switch (t) {
      case "sqlserver":
      case "mssql":
      case "azure-sql":
        return "dbo";
      case "postgres":
      case "postgresql":
        return "public";
      case "mysql":
      case "mariadb":
      case "sqlite":
      case "oracle":
        return "";
      default:
        return "";
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return "N/A";
    const parsed = parseISO(dateString);
    const d = isValid(parsed) ? parsed : new Date(dateString);
    if (!isValid(d)) return "N/A";
    return format(d, "P");
  };

  const handleRefresh = () => {
    if (filterType === "ALL" || filterType === "TABLE") {
      refetchTables();
    }
    if (filterType === "ALL" || filterType === "SP") {
      refetchProcedures();
    }
    if (view === "tree") {
      refetchTree();
    }
  };

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <div className="flex justify-between items-center">
          <div className="space-y-2">
            <Skeleton className="h-8 w-64" />
            <Skeleton className="h-4 w-96" />
          </div>
          <div className="flex gap-2">
            <Skeleton className="h-9 w-24" />
            <Skeleton className="h-9 w-24" />
            <Skeleton className="h-9 w-24" />
          </div>
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
          <div className="lg:col-span-1">
            <Skeleton className="h-[300px] w-full rounded-lg" />
          </div>
          <div className="lg:col-span-3">
            <TableSkeleton columns={6} rows={8} />
          </div>
        </div>
      </div>
    );
  }

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

  // Error state
  if (hasErrors) {
    return (
      <div className="space-y-6 p-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold">Browse Context</h1>
            <p className="text-muted-foreground mt-1">
              Explore database entities and their documentation
            </p>
          </div>
        </div>

        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load database entities. Please check your connection and
            try again.
          </AlertDescription>
        </Alert>

        <div className="flex justify-center">
          <Button onClick={handleRefresh} variant="outline">
            <RefreshCw className="w-4 h-4 mr-2" />
            Try Again
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Browse Context</h1>
          <p className="text-muted-foreground mt-1">
            Explore all database entities and their documentation in{" "}
            <span className="font-medium">{selectedProject?.projectName}</span>
          </p>
        </div>

        <div className="flex gap-2">
          {/* Filter Toggle */}
          <Button
            onClick={() => setShowFilters((prev) => !prev)}
            variant={showFilters ? "default" : "outline"}
            size="sm"
            aria-label="Toggle filters"
          >
            <Funnel className="w-4 h-4 mr-2" />
            Filters
          </Button>

          <Button onClick={handleRefresh} variant="outline" size="sm">
            <RefreshCw className="w-4 h-4 mr-2" />
            Refresh
          </Button>

          <Button asChild>
            <Link to="/">
              <ExternalLink className="w-4 h-4 mr-2" />
              Dashboard
            </Link>
          </Button>
        </div>
      </div>

      {/* Sidebar + Main */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        <div className="lg:col-span-1">
          <ContextCoverageWidget />
        </div>

        <div className="lg:col-span-3 space-y-4">
          {/* Filters (conditionally rendered) */}
          {showFilters && (
            <Card className="animate-in fade-in slide-in-from-top-3 duration-200">
              <CardHeader>
                <CardTitle className="text-lg">Filters</CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Search */}
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search by name, schema, or description..."
                    className="pl-10"
                  />
                </div>

                {/* Filter Row */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div className="space-y-2">
                    <label className="text-sm font-medium">Type</label>
                    <Select
                      value={filterType}
                      onValueChange={(v) => setFilterType(v as FilterType)}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="All Types" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="ALL">All Types</SelectItem>
                        <SelectItem value="TABLE">Tables</SelectItem>
                        <SelectItem value="SP">Stored Procedures</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <label className="text-sm font-medium">Sort by</label>
                    <Select
                      value={sortBy}
                      onValueChange={(v) => setSortBy(v as SortField)}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select field" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="name">Name</SelectItem>
                        <SelectItem value="schema">Schema</SelectItem>
                        <SelectItem value="modified">Modified Date</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-2">
                    <label className="text-sm font-medium">Order</label>
                    <Select
                      value={sortOrder}
                      onValueChange={(v) => setSortOrder(v as SortOrder)}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select order" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="asc">Ascending</SelectItem>
                        <SelectItem value="desc">Descending</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                {/* Summary + View Toggle */}
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 pt-2 border-t">
                  <p className="text-sm text-muted-foreground">
                    {filteredEntities.length}{" "}
                    {filteredEntities.length === 1 ? "entity" : "entities"}{" "}
                    found
                    {searchQuery && ` matching "${searchQuery}"`}
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant={view === "list" ? "default" : "outline"}
                      size="sm"
                      onClick={() => setView("list")}
                    >
                      List View
                    </Button>
                    <Button
                      variant={view === "tree" ? "default" : "outline"}
                      size="sm"
                      onClick={() => setView("tree")}
                    >
                      Tree View
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Pagination Controls (Top) */}
          {view === "list" && filteredEntities.length > pageSize && (
            <div className="flex justify-between items-center px-2">
              <span className="text-sm text-muted-foreground">
                Showing {((currentPage - 1) * pageSize) + 1} to{" "}
                {Math.min(currentPage * pageSize, filteredEntities.length)} of{" "}
                {filteredEntities.length} entities
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
                <div className="text-sm border rounded px-3 py-1 bg-white dark:bg-zinc-900 flex items-center">
                  Page {currentPage} of {totalPages}
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setCurrentPage((p) => Math.min(totalPages, p + 1))
                  }
                  disabled={currentPage === totalPages}
                >
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          )}

          {/* Content - List or Tree View */}
          {view === "list" ? (
            <Card>
              <CardHeader>
                <CardTitle>Database Entities</CardTitle>
                <CardDescription>
                  Click on any entity to view details and manage documentation
                </CardDescription>
              </CardHeader>
              <CardContent>
                {filteredEntities.length === 0 ? (
                  <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>
                      {searchQuery
                        ? "No entities found matching your search criteria."
                        : "No entities found. Make sure your database connection is working."}
                    </AlertDescription>
                  </Alert>
                ) : (
                  <div className="border rounded-lg overflow-hidden">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Entity</TableHead>
                          <TableHead>Type</TableHead>
                          <TableHead>Schema</TableHead>
                          <TableHead>Modified</TableHead>
                          <TableHead>Context</TableHead>
                          <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {paginatedEntities.map((entity) => (
                          <TableRow
                            key={`${entity.entityType}-${entity.entityId}`}
                          >
                            <TableCell className="font-medium">
                              <div className="flex items-center gap-2">
                                {getEntityIcon(entity.entityType)}
                                <div className="flex flex-col">
                                  <span>{entity.entityName}</span>
                                  {entity.description && (
                                    <span className="text-xs text-muted-foreground truncate max-w-[200px]">
                                      {entity.description}
                                    </span>
                                  )}
                                </div>
                              </div>
                            </TableCell>
                            <TableCell>
                              <Badge variant="outline">
                                {getEntityTypeLabel(entity.entityType)}
                              </Badge>
                            </TableCell>
                            <TableCell className="text-muted-foreground">
                              {entity.schemaName ||
                                getDefaultSchema(
                                  selectedProject?.databaseType,
                                ) ||
                                ""}
                            </TableCell>
                            <TableCell className="text-muted-foreground text-sm">
                              {formatDate(entity.modifiedDate)}
                            </TableCell>
                            <TableCell>
                              <InlineContextBadge
                                entityType={entity.entityType}
                                entityId={entity.entityId}
                                entityName={entity.entityName}
                                variant="minimal"
                                allowQuickEdit={true}
                                preloadedContext={
                                  contextMap[
                                  `${entity.entityType}:${entity.entityId}`
                                  ]
                                }
                                disableFetch={true}
                                loading={isBatchLoading}
                                onEditSuccess={() => {
                                  // Optionally refetch data or show a toast
                                }}
                              />
                            </TableCell>
                            <TableCell className="text-right">
                              <Button variant="ghost" size="sm" asChild>
                                <Link to={getEntityRoute(entity)}>
                                  <ExternalLink className="w-3 h-3 mr-1" />
                                  View
                                </Link>
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                )}
              </CardContent>
            </Card>
          ) : (
            <Card>
              <CardHeader>
                <CardTitle>Database Tree</CardTitle>
                <CardDescription>
                  Navigate through your database structure hierarchically
                </CardDescription>
              </CardHeader>
              <CardContent>
                {isLoadingTree ? (
                  <div className="flex flex-col space-y-2 p-4">
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
                      Failed to load database tree. Please try refreshing.
                    </AlertDescription>
                  </Alert>
                ) : treeData ? (
                  <TreeView
                    treeData={treeData}
                    searchQuery={searchQuery}
                    onSearchChange={setSearchQuery}
                    onSelectNode={(_node) => {
                      // console.log("Selected node:", node);
                    }}
                  />
                ) : (
                  <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>
                      No database structure found. The database may be empty or
                      there may be connection issues.
                    </AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}
