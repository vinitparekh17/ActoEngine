// components/context/ContextDashboard.tsx
import React, { useMemo, useState } from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { useBulkImportContext } from "@/hooks/useContext";
import { useConfirm } from "@/hooks/useConfirm";
import { Link } from "react-router-dom";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  FileText,
  AlertTriangle,
  TrendingUp,
  Users,
  Target,
  Clock,
  CheckCircle2,
  XCircle,
  Database,
  FileCode,
  Table as TableIcon,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Upload,
} from "lucide-react";
import { GridSkeleton, PageHeaderSkeleton } from "@/components/ui/skeletons";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import type { BulkContextEntry } from "@/types/context";
import { toast } from "sonner";

// Types
interface CoverageItem {
  entityType: "TABLE" | "COLUMN" | "SP";
  total: number;
  documented: number;
  coveragePercentage: number;
  avgCompleteness?: number;
}

interface CriticalGapItem {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  reason: string;
  referenceCount: number;
  /**
   * Optional field kept for compatibility with older payloads.
   * Prefer computing priority from referenceCount via getGapPriority().
   */
  priority?: "HIGH" | "MEDIUM" | "LOW";
}

/**
 * Derive a priority from referenceCount when the backend doesn't provide one.
 * Adjust thresholds as needed.
 */
function getGapPriority(item: CriticalGapItem): "HIGH" | "MEDIUM" | "LOW" {
  if (item.priority) return item.priority;
  if (item.referenceCount > 50) return "HIGH";
  if (item.referenceCount > 10) return "MEDIUM";
  return "LOW";
}

interface StaleItem {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  lastContextUpdate: string;
  daysSinceUpdate: number;
  schemaChanged: boolean;
}

interface WellDocumentedItem {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  businessDomain?: string;
  dataOwner?: string;
  completenessScore: number;
  expertCount: number;
}

interface DashboardData {
  coverage: CoverageItem[];
  staleEntities: StaleItem[];
  topDocumented: WellDocumentedItem[];
  criticalUndocumented: CriticalGapItem[];
  staleCount: number;
  lastUpdated: string;
  trends?: {
    coverageChange: number;
    period: string;
  };
}

/**
 * Main dashboard showing project-wide context coverage and insights
 * Endpoint: GET /api/projects/{projectId}/context/dashboard
 */
export const ContextDashboard: React.FC = () => {
  const { selectedProject, selectedProjectId, hasProject } = useProject();
  const [gapsPage, setGapsPage] = React.useState(1);
  const [stalePage, setStalePage] = React.useState(1);
  const [topPage, setTopPage] = React.useState(1);
  const [isBulkImportOpen, setIsBulkImportOpen] = useState(false);
  const [bulkImportJson, setBulkImportJson] = useState("");
  const pageSize = 10;

  // Fetch dashboard data
  const {
    data: dashboard,
    isLoading,
    error,
    refetch,
  } = useApi<DashboardData>(
    `/projects/${selectedProjectId}/context/statistics/dashboard`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 30 * 1000, // 30 seconds
      refetchInterval: 30 * 1000, // Refresh every 30 seconds
      retry: 2,
    },
  );

  // Bulk import
  const bulkImportMutation = useBulkImportContext((results) => {
    setIsBulkImportOpen(false);
    setBulkImportJson("");
    refetch();
  });

  const handleBulkImport = () => {
    try {
      const entries: BulkContextEntry[] = JSON.parse(bulkImportJson);
      if (!Array.isArray(entries) || entries.length === 0) {
        toast.error("Invalid JSON format. Expected an array of entries.");
        return;
      }

      const formattedEntries = entries.map((entry) => ({
        entityType: entry.entityType,
        entityId: entry.entityId,
        entityName: entry.entityName,
        context: entry.context,
      }));

      bulkImportMutation.mutate(formattedEntries);
    } catch (error: any) {
      toast.error(`Invalid JSON: ${error.message}`);
    }
  };

  // Extract data with defaults (must be before early returns for hooks rules)
  const coverage = dashboard?.coverage || [];
  const staleEntities = dashboard?.staleEntities || [];
  const topDocumented = dashboard?.topDocumented || [];
  const criticalUndocumented = dashboard?.criticalUndocumented || [];

  // Pagination for gaps (must be before early returns)
  const gapsTotalPages = Math.ceil(criticalUndocumented.length / pageSize);
  const paginatedGaps = useMemo(() => {
    const start = (gapsPage - 1) * pageSize;
    return criticalUndocumented.slice(start, start + pageSize);
  }, [criticalUndocumented, gapsPage, pageSize]);

  // Pagination for stale entities (must be before early returns)
  const staleTotalPages = Math.ceil(staleEntities.length / pageSize);
  const paginatedStale = useMemo(() => {
    const start = (stalePage - 1) * pageSize;
    return staleEntities.slice(start, start + pageSize);
  }, [staleEntities, stalePage, pageSize]);

  // Pagination for top documented (must be before early returns)
  const topTotalPages = Math.ceil(topDocumented.length / pageSize);
  const paginatedTop = useMemo(() => {
    const start = (topPage - 1) * pageSize;
    return topDocumented.slice(start, start + pageSize);
  }, [topDocumented, topPage, pageSize]);

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <PageHeaderSkeleton />
        <GridSkeleton count={4} className="grid gap-4 md:grid-cols-2 lg:grid-cols-4" />
        <div className="space-y-4">
          <div className="grid w-full grid-cols-4 gap-2">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="h-10 bg-muted/20 animate-pulse rounded" />
            ))}
          </div>
          <div className="h-[400px] w-full bg-muted/10 animate-pulse rounded-lg border border-neutral-200 dark:border-neutral-800" />
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load dashboard: {error.message}
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button onClick={() => refetch()} variant="outline">
            Try Again
          </Button>
        </div>
      </div>
    );
  }

  // No project selected
  if (!hasProject || !selectedProjectId) {
    return (
      <div className="space-y-6 p-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Please select a project to view the context dashboard.
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

  // At this point, we know selectedProjectId is defined due to the guard above
  // Create a type-safe variable with proper type assertion
  const projectId: number = selectedProjectId;
  // Calculate overall stats with weighted coverage
  const totalDocumented = coverage.reduce(
    (acc, item) => acc + (item.documented || 0),
    0,
  );
  const totalEntities = coverage.reduce(
    (acc, item) => acc + (item.total || 0),
    0,
  );
  const overallCoverage =
    totalEntities > 0 ? (totalDocumented / totalEntities) * 100 : 0;

  return (
    <div className="space-y-6 p-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between space-y-4 sm:space-y-0">
        <div>
          <h1 className="text-3xl font-bold">Context Dashboard</h1>
          <p className="text-muted-foreground mt-1">
            Documentation coverage and health for{" "}
            <span className="font-medium">{selectedProject?.projectName}</span>
          </p>
          {dashboard?.lastUpdated && (
            <p className="text-xs text-muted-foreground mt-1">
              Last updated: {formatDateTime(dashboard.lastUpdated)}
            </p>
          )}
        </div>
        <div className="flex space-x-2">
          <Button
            variant="outline"
            onClick={() => setIsBulkImportOpen(true)}
            title="Bulk import context entries"
          >
            <Upload className="w-4 h-4 mr-2" />
            Bulk Import
          </Button>
          <Button variant="outline" disabled title="Settings feature coming soon">
            Settings
          </Button>
        </div>
      </div>

      {/* Key Metrics Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {/* Overall Coverage */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">
              Overall Coverage
            </CardTitle>
            <Database className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline space-x-2">
              <div className="text-2xl font-bold">
                {Math.round(overallCoverage)}%
              </div>
              {dashboard?.trends?.coverageChange && (
                <div
                  className={`flex items-center text-xs ${dashboard.trends.coverageChange > 0
                    ? "text-green-600"
                    : "text-red-600"
                    }`}
                >
                  <TrendingUp className="h-3 w-3 mr-1" />
                  {dashboard.trends.coverageChange > 0 ? "+" : ""}
                  {dashboard.trends.coverageChange}%
                </div>
              )}
            </div>
            <Progress value={overallCoverage} className="mt-2 h-2" />
            <p className="text-xs text-muted-foreground mt-2">
              {totalDocumented} of {totalEntities} entities documented
            </p>
          </CardContent>
        </Card>

        {/* Stale Contexts */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Needs Review</CardTitle>
            <AlertTriangle className="h-4 w-4 text-orange-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-orange-500">
              {dashboard?.staleCount || 0}
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              Context marked as stale
            </p>
            {(dashboard?.staleCount || 0) > 0 && (
              <Button
                variant="link"
                size="sm"
                className="mt-2 h-auto p-0 text-orange-600"
                asChild
              >
                <Link to="#stale-section">View all →</Link>
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Critical Gaps */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Critical Gaps</CardTitle>
            <XCircle className="h-4 w-4 text-red-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-500">
              {criticalUndocumented.length}
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              High-priority items undocumented
            </p>
            {criticalUndocumented.length > 0 && (
              <Button
                variant="link"
                size="sm"
                className="mt-2 h-auto p-0 text-red-600"
                asChild
              >
                <Link to="#gaps-section">Document now →</Link>
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Top Contributors */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">
              Well Documented
            </CardTitle>
            <CheckCircle2 className="h-4 w-4 text-green-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-500">
              {topDocumented.length}
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              Entities with complete context
            </p>
            {topDocumented.length > 0 && (
              <Button
                variant="link"
                size="sm"
                className="mt-2 h-auto p-0 text-green-600"
                asChild
              >
                <Link to="#top-section">View examples →</Link>
              </Button>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Main Content Tabs */}
      <Tabs defaultValue="coverage" className="space-y-4">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="coverage">Coverage</TabsTrigger>
          <TabsTrigger value="gaps" className="relative">
            Critical Gaps
            {criticalUndocumented.length > 0 && (
              <Badge
                variant="destructive"
                className="absolute -top-2 -right-2 h-5 w-5 p-0 text-xs"
              >
                {criticalUndocumented.length}
              </Badge>
            )}
          </TabsTrigger>
          <TabsTrigger value="stale" className="relative">
            Needs Review
            {(dashboard?.staleCount || 0) > 0 && (
              <Badge
                variant="outline"
                className="absolute -top-2 -right-2 h-5 w-5 p-0 text-xs border-orange-500 text-orange-600"
              >
                {dashboard?.staleCount}
              </Badge>
            )}
          </TabsTrigger>
          <TabsTrigger value="top">Top Documented</TabsTrigger>
        </TabsList>

        {/* Coverage Tab */}
        <TabsContent value="coverage" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Documentation Coverage by Type</CardTitle>
              <CardDescription>
                Track how well different entity types are documented
              </CardDescription>
            </CardHeader>
            <CardContent>
              {coverage.length === 0 ? (
                <Alert>
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    No entities found in this project. Make sure your database
                    connection is working.
                  </AlertDescription>
                </Alert>
              ) : (
                <div className="space-y-4">
                  {coverage.map((item) => (
                    <div key={item.entityType} className="space-y-2">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          {getEntityIcon(item.entityType)}
                          <span className="font-medium">
                            {getEntityTypeLabel(item.entityType)}
                          </span>
                        </div>
                        <div className="flex items-center gap-4 text-sm">
                          <span className="text-muted-foreground">
                            {item.documented} / {item.total}
                          </span>
                          <span className="font-semibold min-w-[3rem] text-right">
                            {Math.round(item.coveragePercentage || 0)}%
                          </span>
                        </div>
                      </div>
                      <Progress
                        value={item.coveragePercentage || 0}
                        className="h-2"
                      />
                      {item.avgCompleteness && (
                        <p className="text-xs text-muted-foreground">
                          Average completeness:{" "}
                          {Math.round(item.avgCompleteness)}%
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Critical Gaps Tab */}
        <TabsContent value="gaps" id="gaps-section" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Critical Undocumented Items</CardTitle>
              <CardDescription>
                High-impact entities that need documentation urgently
              </CardDescription>
            </CardHeader>
            <CardContent>
              {criticalUndocumented.length === 0 ? (
                <Alert>
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>
                    Great work! All critical entities are documented.
                  </AlertDescription>
                </Alert>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginatedGaps.map((item: CriticalGapItem) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <Badge variant="outline">{item.entityType}</Badge>
                        </TableCell>
                        <TableCell className="font-medium">
                          {item.entityName}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {item.reason}
                        </TableCell>
                        <TableCell>
                          <Badge
                            variant={
                              getGapPriority(item) === "HIGH"
                                ? "destructive"
                                : getGapPriority(item) === "MEDIUM"
                                  ? "secondary"
                                  : "outline"
                            }
                          >
                            {getGapPriority(item)}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Button size="sm" variant="outline" asChild>
                            <Link
                              to={getEntityRoute(
                                item.entityType,
                                item.entityId,
                                projectId,
                              )}
                            >
                              Document
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}

              {/* Pagination for Gaps */}
              {criticalUndocumented.length > pageSize && (
                <div className="flex items-center justify-between mt-4">
                  <div className="text-sm text-muted-foreground">
                    Showing {(gapsPage - 1) * pageSize + 1} to{" "}
                    {Math.min(gapsPage * pageSize, criticalUndocumented.length)} of{" "}
                    {criticalUndocumented.length} items
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setGapsPage((p) => Math.max(1, p - 1))}
                      disabled={gapsPage === 1}
                    >
                      <ChevronLeft className="h-4 w-4" />
                    </Button>
                    <div className="text-sm border rounded px-3 py-1 bg-white dark:bg-zinc-900 flex items-center">
                      Page {gapsPage} of {gapsTotalPages}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        setGapsPage((p) => Math.min(gapsTotalPages, p + 1))
                      }
                      disabled={gapsPage === gapsTotalPages}
                    >
                      <ChevronRight className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Stale Contexts Tab */}
        <TabsContent value="stale" id="stale-section" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Stale Documentation</CardTitle>
              <CardDescription>
                Context that needs review due to schema changes
              </CardDescription>
            </CardHeader>
            <CardContent>
              {staleEntities.length === 0 ? (
                <Alert>
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>
                    All documentation is up to date!
                  </AlertDescription>
                </Alert>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Last Updated</TableHead>
                      <TableHead>Age</TableHead>
                      <TableHead>Schema Changed</TableHead>
                      <TableHead>Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginatedStale.map((item: StaleItem) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <Badge variant="outline">{item.entityType}</Badge>
                        </TableCell>
                        <TableCell className="font-medium">
                          {item.entityName}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(item.lastContextUpdate)}
                        </TableCell>
                        <TableCell>
                          <Badge
                            variant={
                              item.daysSinceUpdate > 30
                                ? "destructive"
                                : item.daysSinceUpdate > 14
                                  ? "secondary"
                                  : "outline"
                            }
                          >
                            {item.daysSinceUpdate} days
                          </Badge>
                        </TableCell>
                        <TableCell>
                          {item.schemaChanged ? (
                            <Badge variant="destructive">Yes</Badge>
                          ) : (
                            <Badge variant="outline">No</Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          <Button size="sm" variant="outline" asChild>
                            <Link
                              to={getEntityRoute(
                                item.entityType,
                                item.entityId,
                                projectId,
                              )}
                            >
                              Review
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}

              {/* Pagination for Stale */}
              {staleEntities.length > pageSize && (
                <div className="flex items-center justify-between mt-4">
                  <div className="text-sm text-muted-foreground">
                    Showing {(stalePage - 1) * pageSize + 1} to{" "}
                    {Math.min(stalePage * pageSize, staleEntities.length)} of{" "}
                    {staleEntities.length} items
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setStalePage((p) => Math.max(1, p - 1))}
                      disabled={stalePage === 1}
                    >
                      <ChevronLeft className="h-4 w-4" />
                    </Button>
                    <div className="text-sm border rounded px-3 py-1 bg-white dark:bg-zinc-900 flex items-center">
                      Page {stalePage} of {staleTotalPages}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        setStalePage((p) => Math.min(staleTotalPages, p + 1))
                      }
                      disabled={stalePage === staleTotalPages}
                    >
                      <ChevronRight className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Top Documented Tab */}
        <TabsContent value="top" id="top-section" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Best Documented Entities</CardTitle>
              <CardDescription>
                Examples of well-documented entities in your project
              </CardDescription>
            </CardHeader>
            <CardContent>
              {topDocumented.length === 0 ? (
                <Alert>
                  <AlertDescription>
                    Start documenting entities to see them here!
                  </AlertDescription>
                </Alert>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Domain</TableHead>
                      <TableHead>Owner</TableHead>
                      <TableHead>Completeness</TableHead>
                      <TableHead>Experts</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginatedTop.map((item: WellDocumentedItem) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <Badge variant="outline">{item.entityType}</Badge>
                        </TableCell>
                        <TableCell>
                          <Link
                            to={getEntityRoute(
                              item.entityType,
                              item.entityId,
                              projectId,
                            )}
                            className="font-medium hover:underline text-primary"
                          >
                            {item.entityName}
                          </Link>
                        </TableCell>
                        <TableCell>
                          <Badge variant="secondary">
                            {item.businessDomain || "N/A"}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-sm">
                          {item.dataOwner || "Unassigned"}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Progress
                              value={item.completenessScore}
                              className="h-2 w-20"
                            />
                            <span className="text-sm font-medium">
                              {item.completenessScore}%
                            </span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant="secondary">
                            {item.expertCount}{" "}
                            {item.expertCount === 1 ? "expert" : "experts"}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}

              {/* Pagination for Top Documented */}
              {topDocumented.length > pageSize && (
                <div className="flex items-center justify-between mt-4">
                  <div className="text-sm text-muted-foreground">
                    Showing {(topPage - 1) * pageSize + 1} to{" "}
                    {Math.min(topPage * pageSize, topDocumented.length)} of{" "}
                    {topDocumented.length} items
                  </div>
                  <div className="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setTopPage((p) => Math.max(1, p - 1))}
                      disabled={topPage === 1}
                    >
                      <ChevronLeft className="h-4 w-4" />
                    </Button>
                    <div className="text-sm border rounded px-3 py-1 bg-white dark:bg-zinc-900 flex items-center">
                      Page {topPage} of {topTotalPages}
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() =>
                        setTopPage((p) => Math.min(topTotalPages, p + 1))
                      }
                      disabled={topPage === topTotalPages}
                    >
                      <ChevronRight className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Quick Actions */}
      <Card>
        <CardHeader>
          <CardTitle>Quick Actions</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button variant="outline" asChild>
            <Link to={`/project/${projectId}/tables`}>
              <Database className="w-4 h-4 mr-2" />
              Browse Tables
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to={`/project/${projectId}/stored-procedures`}>
              <FileCode className="w-4 h-4 mr-2" />
              Browse SPs
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to={`/project/${projectId}/context/experts`}>
              <Users className="w-4 h-4 mr-2" />
              View Experts
            </Link>
          </Button>
          <Button
            variant="outline"
            onClick={() => setIsBulkImportOpen(true)}
            title="Bulk import context entries"
          >
            <Upload className="w-4 h-4 mr-2" />
            Bulk Import
          </Button>
        </CardContent>
      </Card>

      {/* Bulk Import Dialog */}
      <Dialog open={isBulkImportOpen} onOpenChange={setIsBulkImportOpen}>
        <DialogContent className="sm:max-w-[700px] max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Bulk Import Context</DialogTitle>
            <DialogDescription>
              Import multiple context entries at once. Paste JSON array of context entries.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="bulkJson">JSON Array of Context Entries</Label>
              <Textarea
                id="bulkJson"
                value={bulkImportJson}
                onChange={(e) => setBulkImportJson(e.target.value)}
                placeholder={`[\n  {\n    "entityType": "TABLE",\n    "entityId": 1,\n    "entityName": "Users",\n    "context": {\n      "businessDomain": "User Management",\n      "description": "Stores user information"\n    }\n  }\n]`}
                className="font-mono text-sm min-h-[300px]"
              />
              <p className="text-xs text-muted-foreground">
                Format: Array of objects with entityType, entityId, entityName, and context fields
              </p>
            </div>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setIsBulkImportOpen(false);
                setBulkImportJson("");
              }}
            >
              Cancel
            </Button>
            <Button
              onClick={handleBulkImport}
              disabled={bulkImportMutation.isPending || !bulkImportJson.trim()}
            >
              {bulkImportMutation.isPending ? "Importing..." : "Import"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

// Helper functions
function getEntityIcon(entityType: string) {
  switch (entityType) {
    case "TABLE":
      return <TableIcon className="w-4 h-4 text-blue-500" />;
    case "COLUMN":
      return <FileText className="w-4 h-4 text-green-500" />;
    case "SP":
      return <FileCode className="w-4 h-4 text-purple-500" />;
    default:
      return <Database className="w-4 h-4" />;
  }
}

function getEntityTypeLabel(entityType: string): string {
  switch (entityType) {
    case "TABLE":
      return "Tables";
    case "COLUMN":
      return "Columns";
    case "SP":
      return "Stored Procedures";
    default:
      return entityType;
  }
}

function getEntityRoute(
  entityType: string,
  entityId: number,
  projectId: number,
): string {
  switch (entityType) {
    case "TABLE":
      return `/project/${projectId}/tables/${entityId}`;
    case "SP":
      return `/project/${projectId}/stored-procedures/${entityId}`;
    case "COLUMN":
      return `/project/${projectId}/columns/${entityId}`;
    default:
      return `/project/${projectId}`;
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString();
}

function formatDateTime(dateString: string): string {
  return new Date(dateString).toLocaleString();
}
