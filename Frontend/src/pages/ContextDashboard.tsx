import React, { useMemo, useState } from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import {
  useBulkImportContext,
} from "@/hooks/useContext";
import { utcToLocal } from "@/lib/utils";
import { Link, useSearchParams } from "react-router-dom";
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
  TrendingUp,
  Users,
  CheckCircle2,
  XCircle,
  Database,
  FileCode,
  Table as TableIcon,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Upload,
  Crown,
  Star,
  LayoutDashboard,
  Calendar,
  ArrowUpRight,
  Sparkles,
} from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { ExpertSummary } from "@/types/context";
import { GridSkeleton } from "@/components/ui/skeletons";
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
import { Separator } from "@/components/ui/separator";
import { ScrollArea } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";
import type { BulkContextEntry } from "@/types/context";
import { toast } from "sonner";
import { useAuthorization } from "@/hooks/useAuth";

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
  priority?: "HIGH" | "MEDIUM" | "LOW";
}

function getGapPriority(item: CriticalGapItem): "HIGH" | "MEDIUM" | "LOW" {
  if (item.priority) return item.priority;
  if (item.referenceCount > 50) return "HIGH";
  if (item.referenceCount > 10) return "MEDIUM";
  return "LOW";
}

interface WellDocumentedItem {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  businessDomain?: string;
  completenessScore: number;
  expertCount: number;
}

interface DashboardData {
  coverage: CoverageItem[];
  topDocumented: WellDocumentedItem[];
  criticalUndocumented: CriticalGapItem[];
  lastUpdated: string;
  trends?: {
    coverageChange: number;
    period: string;
  };
}

// --- SUB-COMPONENTS ---

const DashboardMetric = ({
  title,
  value,
  description,
  icon: Icon,
  trend,
  colorClass,
  bgClass,
}: {
  title: string;
  value: string | number;
  description?: string;
  icon: any;
  trend?: number;
  colorClass: string;
  bgClass: string;
}) => (
  <Card className="border-border/60 shadow-sm relative overflow-hidden hover:border-border transition-all">
    <CardContent className="p-5">
      <div className="flex justify-between items-start mb-2">
        {/* Title */}
        <h3 className="font-medium text-xs text-muted-foreground uppercase tracking-wider pt-1">
          {title}
        </h3>
        {/* Compact Icon Top-Right */}
        <div className={cn("p-1.5 rounded-md shrink-0", bgClass, colorClass)}>
          <Icon className="w-4 h-4" />
        </div>
      </div>

      <div className="space-y-1">
        <div className="flex items-baseline gap-2">
          {/* Main Value */}
          <span className="text-2xl font-bold text-foreground tracking-tight">
            {value}
          </span>

          {/* Trend Badge */}
          {trend !== undefined && (
            <span
              className={cn(
                "text-[10px] font-medium flex items-center px-1.5 py-0.5 rounded-full",
                trend > 0
                  ? "text-emerald-700 bg-emerald-100/20 dark:text-emerald-400 dark:bg-emerald-900/40"
                  : "text-rose-700 bg-rose-100/20 dark:text-rose-400 dark:bg-rose-900/40",
              )}
            >
              {trend > 0 ? "+" : ""}
              {trend}%
              <TrendingUp
                className={cn("w-3 h-3 ml-0.5", trend < 0 && "rotate-180")}
              />
            </span>
          )}
        </div>

        {/* Description */}
        {description && (
          <p className="text-xs text-muted-foreground truncate">
            {description}
          </p>
        )}
      </div>
    </CardContent>
  </Card>
);

const EmptyState = ({
  icon: Icon,
  title,
  description,
  actionLabel,
  onAction,
}: {
  icon: any;
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
}) => (
  <div className="flex flex-col items-center justify-center py-12 text-center border-2 border-dashed rounded-xl bg-muted/10">
    <div className="w-12 h-12 rounded-full bg-background flex items-center justify-center mb-4 shadow-sm border">
      <Icon className="h-6 w-6 text-muted-foreground/70" />
    </div>
    <h3 className="text-base font-medium text-foreground mb-1">{title}</h3>
    <p className="text-sm text-muted-foreground max-w-sm mb-6 leading-relaxed">
      {description}
    </p>
    {actionLabel && onAction && (
      <Button variant="outline" size="sm" onClick={onAction}>
        {actionLabel}
      </Button>
    )}
  </div>
);

// --- MAIN COMPONENT ---

export const ContextDashboard: React.FC = () => {
  const { selectedProject, selectedProjectId, hasProject } = useProject();
  const [searchParams, setSearchParams] = useSearchParams();
  const [gapsPage, setGapsPage] = React.useState(1);
  const [topPage, setTopPage] = React.useState(1);
  const [isBulkImportOpen, setIsBulkImportOpen] = useState(false);
  const [bulkImportJson, setBulkImportJson] = useState("");
  const pageSize = 10;
  const [activeTab, setActiveTab] = useState(() =>
    "coverage"
  );

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
      staleTime: 30 * 1000,
      refetchInterval: 30 * 1000,
      retry: 2,
    },
  );

  // Fetch expert summary
  const { data: expertSummary, isLoading: isLoadingExperts } = useApi<
    ExpertSummary[]
  >(`/projects/${selectedProjectId}/context/experts/summary`, {
    enabled: hasProject && !!selectedProjectId,
    staleTime: 2 * 60 * 1000,
    retry: 1,
    showErrorToast: false,
  });

  // Bulk import
  const bulkImportMutation = useBulkImportContext((results) => {
    setIsBulkImportOpen(false);
    setBulkImportJson("");
    refetch();
    toast.success("Bulk context import completed successfully");
  });

  React.useEffect(() => {
    if (activeTab === "experts" && (!expertSummary || expertSummary.length === 0)) {
      setActiveTab("coverage");
    }
  }, [activeTab, expertSummary]);

  const handleTabChange = (value: string) => {
    setActiveTab(value);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.delete("tab");
    setSearchParams(nextParams, { replace: true });
  };

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

  const coverage = dashboard?.coverage || [];
  const topDocumented = dashboard?.topDocumented || [];
  const criticalUndocumented = dashboard?.criticalUndocumented || [];

  const gapsTotalPages = Math.ceil(criticalUndocumented.length / pageSize);
  const paginatedGaps = useMemo(() => {
    const start = (gapsPage - 1) * pageSize;
    return criticalUndocumented.slice(start, start + pageSize);
  }, [criticalUndocumented, gapsPage, pageSize]);

  const topTotalPages = Math.ceil(topDocumented.length / pageSize);
  const paginatedTop = useMemo(() => {
    const start = (topPage - 1) * pageSize;
    return topDocumented.slice(start, start + pageSize);
  }, [topDocumented, topPage, pageSize]);

  // Loading state
  if (isLoading) {
    return (
      <div className="flex flex-col h-screen bg-background animate-pulse">
        <div className="h-14 border-b bg-muted/20" />
        <div className="p-8 space-y-8">
          <div className="space-y-2">
            <div className="h-8 w-64 bg-muted rounded" />
            <div className="h-4 w-48 bg-muted/60 rounded" />
          </div>
          <GridSkeleton count={4} className="grid-cols-4 gap-6" />
          <div className="h-[400px] bg-muted/10 rounded-xl border border-dashed border-neutral-200 dark:border-neutral-800" />
        </div>
      </div>
    );
  }

  // Error state
  if (error || !hasProject || !selectedProjectId) {
    return (
      <div className="flex flex-col items-center justify-center h-screen p-6 text-center">
        <div className="w-16 h-16 rounded-full bg-destructive/10 flex items-center justify-center mb-6">
          <AlertCircle className="h-8 w-8 text-destructive" />
        </div>
        <h2 className="text-xl font-semibold mb-2">
          {!hasProject ? "No Project Selected" : "Failed to Load Dashboard"}
        </h2>
        <p className="text-muted-foreground mb-6 max-w-md">
          {!hasProject
            ? "Please select a project to view context insights."
            : error?.message}
        </p>
        <Button
          asChild={!hasProject}
          onClick={hasProject ? () => refetch() : undefined}
          variant="outline"
        >
          {!hasProject ? (
            <Link to="/projects">Select Project</Link>
          ) : (
            "Try Again"
          )}
        </Button>
      </div>
    );
  }

  const projectId: number = selectedProjectId;
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
    <div className="flex flex-col bg-background overflow-hidden">
      {/* Sticky Header */}
      <div className="px-6 py-3 border-b flex items-center justify-between bg-background/95 backdrop-blur z-30 sticky top-0 supports-[backdrop-filter]:bg-background/60">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
            <LayoutDashboard className="w-4 h-4 text-primary" />
            Context Dashboard
          </div>
          <Separator orientation="vertical" className="h-4" />
          <Badge variant="outline" className="font-normal text-xs">
            {selectedProject?.projectName}
          </Badge>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setIsBulkImportOpen(true)}
          >
            <Upload className="w-4 h-4 mr-2" />
            Bulk Import
          </Button>
        </div>
      </div>

      {/* Scrollable Content */}
      <ScrollArea className="flex-1">
        <div className="flex flex-col min-h-full pb-20">
          {/* Hero Stats Section */}
          <div className="px-8 py-8 border-b bg-gradient-to-b from-muted/20 to-background">
            <div className="max-w-[1920px] mx-auto w-full space-y-8">
              <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
                <div>
                  <h1 className="text-2xl font-bold tracking-tight">
                    Knowledge Base Health
                  </h1>
                  <p className="text-muted-foreground mt-1 text-sm">
                    Overview of documentation coverage and completeness.
                  </p>
                </div>
                {dashboard?.lastUpdated && (
                  <div className="text-xs text-muted-foreground flex items-center bg-background border px-3 py-1.5 rounded-full shadow-sm">
                    <Calendar className="w-3.5 h-3.5 mr-2" />
                    Last synced: {utcToLocal(dashboard.lastUpdated, "MMM d, HH:mm")}
                  </div>
                )}
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">
                <DashboardMetric
                  title="Overall Coverage"
                  value={`${Math.round(overallCoverage)}%`}
                  description={`${totalDocumented}/${totalEntities} entities documented`}
                  icon={Database}
                  trend={dashboard?.trends?.coverageChange}
                  colorClass="text-blue-600 dark:text-blue-400"
                  bgClass="bg-blue-50 dark:bg-blue-950/40"
                />
                <DashboardMetric
                  title="Critical Gaps"
                  value={criticalUndocumented.length}
                  description="High-priority undocumented"
                  icon={XCircle}
                  colorClass="text-rose-600 dark:text-rose-400"
                  bgClass="bg-rose-50 dark:bg-rose-950/40"
                />
                <DashboardMetric
                  title="Well Documented"
                  value={topDocumented.length}
                  description="Complete context & ownership"
                  icon={CheckCircle2}
                  colorClass="text-emerald-600 dark:text-emerald-400"
                  bgClass="bg-emerald-50 dark:bg-emerald-950/40"
                />
                <DashboardMetric
                  title="Total Experts"
                  value={expertSummary?.length || 0}
                  description="Assigned subject matter experts"
                  icon={Users}
                  colorClass="text-indigo-600 dark:text-indigo-400"
                  bgClass="bg-indigo-50 dark:bg-indigo-950/40"
                />
              </div>
            </div>
          </div>

          {/* Main Tabs Section */}
          <div className="p-8 max-w-[1920px] mx-auto w-full">
            <Tabs
              value={activeTab}
              onValueChange={handleTabChange}
              className="space-y-6"
            >
              <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <TabsList className="bg-muted/50 p-1 h-11">
                  <TabsTrigger value="coverage" className="gap-2 text-xs">
                    <Sparkles className="w-3.5 h-3.5" /> Coverage Analysis
                  </TabsTrigger>
                  <TabsTrigger value="gaps" className="gap-2 text-xs">
                    <AlertCircle className="w-3.5 h-3.5" /> Critical Gaps
                    {criticalUndocumented.length > 0 && (
                      <Badge variant="destructive" className="ml-1 h-5 px-1.5">
                        {criticalUndocumented.length}
                      </Badge>
                    )}
                  </TabsTrigger>
                  <TabsTrigger value="top" className="gap-2 text-xs">
                    <Crown className="w-3.5 h-3.5" /> Top Documented
                  </TabsTrigger>
                  {expertSummary && expertSummary.length > 0 && (
                    <TabsTrigger value="experts" className="gap-2 text-xs">
                      <Users className="w-3.5 h-3.5" /> Experts
                    </TabsTrigger>
                  )}
                </TabsList>
              </div>

              {/* Coverage Tab */}
              <TabsContent
                value="coverage"
                className="mt-0 focus-visible:outline-none animate-in fade-in slide-in-from-bottom-2 duration-500"
              >
                <Card className="border-border/60">
                  <CardHeader className="border-b bg-muted/20 pb-4">
                    <div className="flex items-center justify-between">
                      <div className="space-y-1">
                        <CardTitle className="text-base">
                          Coverage by Entity Type
                        </CardTitle>
                        <CardDescription>
                          Breakdown of documentation progress across the schema.
                        </CardDescription>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="p-6">
                    {coverage.length === 0 ? (
                      <EmptyState
                        icon={Database}
                        title="No Entities Found"
                        description="No entities found in this project. Make sure your database connection is working."
                      />
                    ) : (
                      <div className="rounded-lg border divide-y bg-card">
                        {coverage.map((item) => (
                          <div
                            key={item.entityType}
                            className="flex flex-col sm:flex-row sm:items-center justify-between p-4 gap-4 hover:bg-muted/30 transition-colors group"
                          >
                            {/* 1. Identity (Icon + Name + Counts) */}
                            <div className="flex items-center gap-3 min-w-[200px]">
                              <div className="p-2 bg-background border rounded-md shadow-sm group-hover:border-primary/30 transition-colors">
                                {getEntityIcon(item.entityType)}
                              </div>
                              <div>
                                <p className="font-medium text-sm text-foreground">
                                  {getEntityTypeLabel(item.entityType)}
                                </p>
                                <p className="text-xs text-muted-foreground">
                                  {item.documented} / {item.total} items
                                </p>
                              </div>
                            </div>

                            {/* 2. Progress Bar Area */}
                            <div className="flex-1 flex flex-col justify-center gap-1.5 max-w-md w-full">
                              <div className="flex justify-between text-xs">
                                <span className="text-muted-foreground">
                                  Coverage
                                </span>
                                {item.avgCompleteness && (
                                  <span className="flex items-center gap-1 text-muted-foreground">
                                    <Sparkles className="w-3 h-3 text-amber-500" />
                                    Quality:{" "}
                                    <span className="font-medium text-foreground">
                                      {Math.round(item.avgCompleteness)}%
                                    </span>
                                  </span>
                                )}
                              </div>
                              <Progress
                                value={item.coveragePercentage || 0}
                                className="h-2"
                              />
                            </div>

                            {/* 3. Big Percentage Stats */}
                            <div className="flex items-center justify-end min-w-[80px]">
                              <span className="text-xl font-bold text-foreground">
                                {Math.round(item.coveragePercentage || 0)}%
                              </span>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </CardContent>
                </Card>
              </TabsContent>

              {/* Gaps Tab */}
              <TabsContent
                value="gaps"
                className="mt-0 focus-visible:outline-none animate-in fade-in slide-in-from-bottom-2 duration-500"
              >
                <Card className="border-border/60">
                  <CardHeader className="border-b bg-muted/20 pb-4">
                    <CardTitle className="text-base text-rose-600 flex items-center gap-2">
                      <AlertCircle className="w-4 h-4" /> Action Required:
                      Critical Gaps
                    </CardTitle>
                    <CardDescription>
                      High-usage entities missing context.
                    </CardDescription>
                  </CardHeader>
                  <div className="p-0">
                    {criticalUndocumented.length === 0 ? (
                      <div className="p-6">
                        <EmptyState
                          icon={CheckCircle2}
                          title="All Clear!"
                          description="Great work! All critical entities are documented."
                        />
                      </div>
                    ) : (
                      <>
                        <Table>
                          <TableHeader className="bg-muted/10">
                            <TableRow>
                              <TableHead className="w-[100px]">Type</TableHead>
                              <TableHead>Entity Name</TableHead>
                              <TableHead>Gap Reason</TableHead>
                              <TableHead>Priority</TableHead>
                              <TableHead className="text-right">
                                Action
                              </TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {paginatedGaps.map((item) => (
                              <TableRow
                                key={`${item.entityType}-${item.entityId}`}
                                className="group hover:bg-muted/30"
                              >
                                <TableCell>
                                  <Badge
                                    variant="outline"
                                    className="font-mono text-[10px] bg-background"
                                  >
                                    {item.entityType}
                                  </Badge>
                                </TableCell>
                                <TableCell className="font-medium">
                                  <span className="group-hover:text-primary transition-colors">
                                    {item.entityName}
                                  </span>
                                </TableCell>
                                <TableCell className="text-muted-foreground text-sm">
                                  {item.reason}
                                </TableCell>
                                <TableCell>
                                  {(() => {
                                    const priority = getGapPriority(item);
                                    return (
                                      <Badge
                                        variant={
                                          priority === "HIGH"
                                            ? "destructive"
                                            : priority === "MEDIUM"
                                              ? "secondary"
                                              : "outline"
                                        }
                                        className="text-[10px]"
                                      >
                                        {priority}
                                      </Badge>
                                    );
                                  })()}
                                </TableCell>
                                <TableCell className="text-right">
                                  <Button
                                    size="sm"
                                    variant="ghost"
                                    className="h-8 text-xs hover:text-primary hover:bg-primary/10"
                                    asChild
                                  >
                                    <Link
                                      to={getEntityRoute(
                                        item.entityType,
                                        item.entityId,
                                        projectId,
                                      )}
                                    >
                                      Document{" "}
                                      <ArrowUpRight className="ml-1 w-3 h-3" />
                                    </Link>
                                  </Button>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                        {criticalUndocumented.length > pageSize && (
                          <div className="flex items-center justify-between p-4 border-t">
                            <div className="text-xs text-muted-foreground">
                              Showing {(gapsPage - 1) * pageSize + 1}-
                              {Math.min(
                                gapsPage * pageSize,
                                criticalUndocumented.length,
                              )}{" "}
                              of {criticalUndocumented.length}
                            </div>
                            <div className="flex gap-2">
                              <Button
                                variant="outline"
                                size="icon"
                                className="h-8 w-8"
                                onClick={() =>
                                  setGapsPage((p) => Math.max(1, p - 1))
                                }
                                disabled={gapsPage === 1}
                              >
                                <ChevronLeft className="w-4 h-4" />
                              </Button>
                              <Button
                                variant="outline"
                                size="icon"
                                className="h-8 w-8"
                                onClick={() =>
                                  setGapsPage((p) =>
                                    Math.min(gapsTotalPages, p + 1),
                                  )
                                }
                                disabled={gapsPage === gapsTotalPages}
                              >
                                <ChevronRight className="w-4 h-4" />
                              </Button>
                            </div>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </Card>
              </TabsContent>

              {/* Top Documented Tab */}
              <TabsContent
                value="top"
                className="mt-0 focus-visible:outline-none animate-in fade-in slide-in-from-bottom-2 duration-500"
              >
                <Card className="border-border/60">
                  <CardHeader className="border-b bg-muted/20 pb-4">
                    <CardTitle className="text-base text-emerald-600 flex items-center gap-2">
                      <Crown className="w-4 h-4" /> Gold Standard Examples
                    </CardTitle>
                    <CardDescription>
                      Best documented entities to use as references.
                    </CardDescription>
                  </CardHeader>
                  <div className="p-0">
                    {topDocumented.length === 0 ? (
                      <div className="p-6">
                        <EmptyState
                          icon={Star}
                          title="No Top Entities"
                          description="Start documenting to see your best work here."
                        />
                      </div>
                    ) : (
                      <>
                        <Table>
                          <TableHeader className="bg-muted/10">
                            <TableRow>
                              <TableHead>Entity</TableHead>
                              <TableHead>Domain</TableHead>
                              <TableHead>Completeness</TableHead>
                              <TableHead>Experts</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {paginatedTop.map((item) => (
                              <TableRow
                                key={item.entityId}
                                className="hover:bg-muted/30"
                              >
                                <TableCell>
                                  <div className="flex flex-col">
                                    <Link
                                      to={getEntityRoute(
                                        item.entityType,
                                        item.entityId,
                                        projectId,
                                      )}
                                      className="font-medium hover:underline text-primary text-sm"
                                    >
                                      {item.entityName}
                                    </Link>
                                    <span className="text-[10px] text-muted-foreground uppercase">
                                      {item.entityType}
                                    </span>
                                  </div>
                                </TableCell>
                                <TableCell>
                                  <Badge
                                    variant="secondary"
                                    className="font-normal text-xs"
                                  >
                                    {item.businessDomain || "N/A"}
                                  </Badge>
                                </TableCell>
                                <TableCell>
                                  <div className="flex items-center gap-2">
                                    <Progress
                                      value={item.completenessScore}
                                      className="w-16 h-2"
                                    />
                                    <span className="text-xs font-medium">
                                      {item.completenessScore}%
                                    </span>
                                  </div>
                                </TableCell>
                                <TableCell>
                                  <div className="flex -space-x-2">
                                    {[
                                      ...Array(Math.min(3, item.expertCount)),
                                    ].map((_, i) => (
                                      <div
                                        key={i}
                                        className="w-6 h-6 rounded-full bg-muted border-2 border-background flex items-center justify-center text-[8px]"
                                      >
                                        <Users className="w-3 h-3 text-muted-foreground" />
                                      </div>
                                    ))}
                                    {item.expertCount > 3 && (
                                      <div className="w-6 h-6 rounded-full bg-muted border-2 border-background flex items-center justify-center text-[8px]">
                                        +{item.expertCount - 3}
                                      </div>
                                    )}
                                  </div>
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                        {topDocumented.length > pageSize && (
                          <div className="flex items-center justify-between p-4 border-t">
                            <div className="text-xs text-muted-foreground">
                              Showing {(topPage - 1) * pageSize + 1}-
                              {Math.min(topPage * pageSize, topDocumented.length)}{" "}
                              of {topDocumented.length}
                            </div>
                            <div className="flex gap-2">
                              <Button
                                variant="outline"
                                size="icon"
                                className="h-8 w-8"
                                onClick={() =>
                                  setTopPage((p) => Math.max(1, p - 1))
                                }
                                disabled={topPage === 1}
                              >
                                <ChevronLeft className="w-4 h-4" />
                              </Button>
                              <Button
                                variant="outline"
                                size="icon"
                                className="h-8 w-8"
                                onClick={() =>
                                  setTopPage((p) =>
                                    Math.min(topTotalPages, p + 1),
                                  )
                                }
                                disabled={topPage === topTotalPages}
                              >
                                <ChevronRight className="w-4 h-4" />
                              </Button>
                            </div>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </Card>
              </TabsContent>

              {/* Experts Tab */}
              {expertSummary && expertSummary.length > 0 && (
                <TabsContent
                  value="experts"
                  className="mt-0 focus-visible:outline-none animate-in fade-in slide-in-from-bottom-2 duration-500"
                >
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {expertSummary.map((expert) => (
                      <Card
                        key={expert.userId}
                        className="hover:shadow-md transition-shadow group"
                      >
                        <CardContent className="p-6">
                          <div className="flex items-start justify-between mb-4">
                            <div className="flex items-center gap-3">
                              <Avatar className="h-10 w-10 border">
                                <AvatarFallback className="bg-primary/10 text-primary font-medium text-xs">
                                  {expert.user.fullName
                                    ?.substring(0, 2)
                                    .toUpperCase() || "EX"}
                                </AvatarFallback>
                              </Avatar>
                              <div>
                                <h4 className="font-medium text-sm">
                                  {expert.user.fullName || expert.user.username}
                                </h4>
                                <p className="text-xs text-muted-foreground">
                                  {expert.user.email}
                                </p>
                              </div>
                            </div>
                            <Badge variant="outline" className="bg-primary/5">
                              {expert.entityCount} entities
                            </Badge>
                          </div>
                          <Separator className="mb-4" />
                          <div className="space-y-3">
                            <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                              Expertise Areas
                            </p>
                            <div className="flex flex-wrap gap-2">
                              {expert.expertiseBreakdown.OWNER > 0 && (
                                <Badge
                                  variant="default"
                                  className="text-[10px]"
                                >
                                  Owner ({expert.expertiseBreakdown.OWNER})
                                </Badge>
                              )}
                              {expert.expertiseBreakdown.EXPERT > 0 && (
                                <Badge
                                  variant="secondary"
                                  className="text-[10px]"
                                >
                                  Expert ({expert.expertiseBreakdown.EXPERT})
                                </Badge>
                              )}
                              {expert.expertiseBreakdown.CONTRIBUTOR > 0 && (
                                <Badge
                                  variant="outline"
                                  className="text-[10px]"
                                >
                                  Contrib (
                                  {expert.expertiseBreakdown.CONTRIBUTOR})
                                </Badge>
                              )}
                            </div>
                          </div>
                          <div className="mt-4 pt-4 border-t flex justify-end">
                            <Button
                              variant="ghost"
                              size="sm"
                              className="h-7 text-xs opacity-0 group-hover:opacity-100 transition-opacity"
                              asChild
                            >
                              <a href={`mailto:${expert.user.email}`}>
                                Contact Expert
                              </a>
                            </Button>
                          </div>
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                </TabsContent>
              )}
            </Tabs>
          </div>
        </div>
      </ScrollArea>

      {/* Bulk Import Dialog */}
      <Dialog open={isBulkImportOpen} onOpenChange={setIsBulkImportOpen}>
        <DialogContent className="sm:max-w-[700px] max-h-[85vh] overflow-hidden flex flex-col">
          <DialogHeader>
            <DialogTitle>Bulk Import Context</DialogTitle>
            <DialogDescription>
              Import multiple context entries via JSON. Existing entries will be
              updated.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4 flex-1 overflow-hidden">
            <div className="space-y-2 h-full flex flex-col">
              <Label htmlFor="bulkJson">JSON Payload</Label>
              <div className="flex-1 border rounded-md relative">
                <Textarea
                  id="bulkJson"
                  value={bulkImportJson}
                  onChange={(e) => setBulkImportJson(e.target.value)}
                  placeholder={`[\n  {\n    "entityType": "TABLE",\n    "entityId": 1,\n    "entityName": "Users",\n    "context": {\n      "businessDomain": "User Management",\n      "description": "Stores user information"\n    }\n  }\n]`}
                  className="font-mono text-sm resize-none border-0 focus-visible:ring-0 h-[300px] bg-muted/20"
                />
              </div>
              <p className="text-xs text-muted-foreground">
                Required fields:{" "}
                <code className="bg-muted px-1 rounded">entityType</code>,{" "}
                <code className="bg-muted px-1 rounded">entityId</code>,{" "}
                <code className="bg-muted px-1 rounded">context</code>
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
              {bulkImportMutation.isPending ? "Importing..." : "Import Context"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

// Helper functions (kept same logic, polished return types if needed)
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
  const typeSlug = entityType.toLowerCase();
  return `/project/${projectId}/entities/${typeSlug}/${entityId}/overview`;
}
