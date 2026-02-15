import { Badge } from "@/components/ui/badge";
import {
  Breadcrumb,
  BreadcrumbList,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbSeparator,
  BreadcrumbPage,
} from "@/components/ui/breadcrumb";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  TableHeader,
  TableRow,
  TableHead,
  TableBody,
  TableCell,
  Table,
} from "@/components/ui/table";
import { useApi } from "@/hooks/useApi";
import { formatRelativeTime } from "@/lib/utils";
import { getDefaultSchema } from "@/lib/schema-utils";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Separator } from "@/components/ui/separator";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import {
  TooltipProvider,
  Tooltip,
  TooltipTrigger,
  TooltipContent,
} from "@/components/ui/tooltip";
import {
  AlertCircle,
  ArrowLeft,
  BookOpen,
  Calendar,
  Code2,
  Copy,
  Database,
  FileText,
  Info,
  Layers,
  Mail,
  Network,
  Shield,
  Sparkles,
  Table as TableIcon,
  UserIcon,
  Users,
  Clock,
  Link2,
} from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { toast } from "sonner";
import React, { lazy } from "react";
import { useProject } from "@/hooks/useProject";
import LogicalFkPanel from "@/components/er-diagram/LogicalFkPanel";

const Editor = lazy(() => import("@monaco-editor/react"));

// ============================================================================
// TYPES
// ============================================================================
interface TableDetails {
  tableId: number;
  tableName: string;
  schemaName?: string;
  description?: string;
  rowCount?: number;
  createdDate?: string;
  modifiedDate?: string;
  columns: Array<{
    columnId?: number;
    name: string;
    dataType: string;
    maxLength?: number;
    isNullable: boolean;
    isPrimaryKey?: boolean;
    isForeignKey?: boolean;
    defaultValue?: string;
    description?: string;
  }>;
  ddl?: string;
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

interface Expert {
  userId: number;
  expertiseLevel: "OWNER" | "EXPERT" | "FAMILIAR" | "CONTRIBUTOR";
  notes?: string;
  addedAt: string;
  user: {
    userId: number;
    fullName?: string;
    username: string;
    email: string;
  };
}

interface ContextResponse {
  context: {
    purpose?: string;
    businessImpact?: string;
    criticalityLevel?: number;
    businessDomain?: string;
  };
  experts: Expert[];
  completenessScore: number;
  isStale: boolean;
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================
function getInitials(name?: string): string {
  if (!name) return "?";
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return parts.length === 1
    ? parts[0][0].toUpperCase()
    : `${parts[0][0]}${parts[1][0]}`.toUpperCase();
}

function formatRowCount(count?: number): string {
  if (count == null) return "—";
  if (count >= 1_000_000) return `${(count / 1_000_000).toFixed(1)}M`;
  if (count >= 1_000) return `${(count / 1_000).toFixed(1)}K`;
  return count.toLocaleString();
}

const EXPERTISE_CONFIG = {
  OWNER: { label: "Owner", variant: "default" as const, icon: Shield },
  EXPERT: { label: "Expert", variant: "secondary" as const, icon: Sparkles },
  FAMILIAR: { label: "Familiar", variant: "outline" as const, icon: UserIcon },
  CONTRIBUTOR: {
    label: "Contributor",
    variant: "outline" as const,
    icon: Code2,
  },
};

const CRITICALITY_CONFIG: Record<
  number,
  {
    label: string;
    variant: "default" | "secondary" | "destructive" | "outline";
  }
> = {
  1: { label: "Low", variant: "outline" },
  2: { label: "Moderate", variant: "outline" },
  3: { label: "Standard", variant: "secondary" },
  4: { label: "High", variant: "default" },
  5: { label: "Critical", variant: "destructive" },
};

// ============================================================================
// SUB-COMPONENTS
// ============================================================================
const StatItem: React.FC<{
  label: string;
  value: string | number;
  icon?: React.ReactNode;
}> = ({ label, value, icon }) => (
  <div className="flex flex-col gap-1">
    <span className="text-[10px] text-muted-foreground uppercase font-semibold tracking-wider flex items-center gap-1.5">
      {icon}
      {label}
    </span>
    <span className="text-lg font-semibold text-foreground tracking-tight">
      {value}
    </span>
  </div>
);

const ExpertCard: React.FC<{ expert: Expert }> = ({ expert }) => {
  const config =
    EXPERTISE_CONFIG[expert.expertiseLevel] || EXPERTISE_CONFIG.FAMILIAR;
  const Icon = config.icon;
  const displayName = expert.user.fullName || expert.user.username;

  return (
    <Card className="group hover:shadow-md transition-all duration-200 border-border/60">
      <CardContent className="p-4">
        <div className="flex items-start gap-3">
          <Avatar className="h-10 w-10 border">
            <AvatarFallback className="bg-muted text-muted-foreground font-medium text-xs">
              {getInitials(displayName)}
            </AvatarFallback>
          </Avatar>
          <div className="flex-1 min-w-0">
            <div className="flex items-center justify-between gap-2 mb-1">
              <h4 className="font-medium text-foreground truncate text-sm">
                {displayName}
              </h4>
              <Badge
                variant={config.variant}
                className="text-[10px] h-5 px-1.5"
              >
                <Icon className="h-3 w-3 mr-1" />
                {config.label}
              </Badge>
            </div>
            <p className="text-xs text-muted-foreground truncate mb-2">
              {expert.user.email}
            </p>

            {expert.notes && (
              <div className="bg-muted/50 p-2 rounded text-xs text-muted-foreground italic mb-3 line-clamp-2">
                "{expert.notes}"
              </div>
            )}

            <div className="flex items-center justify-between pt-2 border-t border-border/50">
              <span className="text-[10px] text-muted-foreground flex items-center gap-1">
                <Clock className="h-3 w-3" />
                {formatRelativeTime(expert.addedAt, "recently")}
              </span>
              <Button
                variant="ghost"
                size="sm"
                className="h-6 text-xs px-2 opacity-0 group-hover:opacity-100 transition-opacity"
                asChild
              >
                <a href={`mailto:${expert.user.email}`}>
                  <Mail className="h-3 w-3 mr-1" />
                  Contact
                </a>
              </Button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
};

export const EmptyExpertsState: React.FC<{ entityName: string }> = ({
  entityName,
}) => (
  <div className="flex flex-col items-center justify-center py-12 text-center border-2 border-dashed rounded-lg bg-muted/10">
    <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mb-4">
      <Users className="h-6 w-6 text-muted-foreground/50" />
    </div>
    <h3 className="text-base font-medium text-foreground mb-1">
      No Experts Assigned
    </h3>
    <p className="text-sm text-muted-foreground max-w-sm mb-4">
      Help your team by identifying who knows about{" "}
      <span className="font-medium text-foreground">{entityName}</span>.
    </p>
    <Button variant="outline" size="sm">
      <UserIcon className="h-4 w-4 mr-2" />
      Assign Expert
    </Button>
  </div>
);

const DocumentationEmptyState: React.FC<{ entityName: string }> = ({
  entityName,
}) => (
  <div className="flex flex-col items-center justify-center py-12 text-center border-2 border-dashed rounded-lg bg-muted/10">
    <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mb-4">
      <BookOpen className="h-6 w-6 text-muted-foreground/50" />
    </div>
    <h3 className="text-base font-medium text-foreground mb-1">
      No Documentation Yet
    </h3>
    <p className="text-sm text-muted-foreground max-w-sm mb-4">
      Document the business purpose and usage patterns for{" "}
      <span className="font-medium text-foreground">{entityName}</span>.
    </p>
    <Button size="sm">
      <FileText className="h-4 w-4 mr-2" />
      Add Documentation
    </Button>
  </div>
);

// ============================================================================
// MAIN COMPONENT
// ============================================================================
const EntityDetailPage: React.FC = () => {
  const { projectId, entityId, entityType: entityTypeParam } = useParams<{
    projectId: string;
    entityId: string;
    entityType: string;
  }>();

  // Normalize entityType from route param, defaulting to "SP" if missing
  const entityType = entityTypeParam?.toUpperCase() === "TABLES"
    ? "TABLE"
    : entityTypeParam?.toUpperCase() === "STORED-PROCEDURES" || entityTypeParam?.toUpperCase() === "SP"
      ? "SP"
      : "SP"; // Default fallback

  const isTable = entityType === "TABLE";

  // Fetch Entity Structure
  const structureEndpoint = isTable
    ? `/DatabaseBrowser/projects/${projectId}/tables/${entityId}`
    : `/DatabaseBrowser/projects/${projectId}/stored-procedures/${entityId}`;

  const {
    data: entityData,
    isLoading: isLoadingStructure,
    error: structureError,
  } = useApi<TableDetails | SPDetails>(structureEndpoint);

  // Fetch Context & Experts
  const { data: contextData, isLoading: isLoadingContext } =
    useApi<ContextResponse>(
      `/projects/${projectId}/context/${entityType}/${entityId}`,
      { staleTime: 30 * 1000, retry: 1 },
    );

  const { selectedProject } = useProject();

  // Derived values
  const name = isTable
    ? (entityData as TableDetails)?.tableName
    : (entityData as SPDetails)?.procedureName;

  const schema = entityData?.schemaName || getDefaultSchema(selectedProject?.databaseType);
  const fullName = name ? `${schema}.${name}` : "Loading...";
  const columns = isTable ? (entityData as TableDetails)?.columns : undefined;
  const parameters = !isTable
    ? (entityData as SPDetails)?.parameters
    : undefined;
  const definition = !isTable
    ? (entityData as SPDetails)?.definition
    : undefined;
  const ddl = isTable ? (entityData as TableDetails)?.ddl : undefined;
  const rowCount = isTable ? (entityData as TableDetails)?.rowCount : undefined;
  const experts = contextData?.experts || [];
  const context = contextData?.context;
  const criticalityConfig = CRITICALITY_CONFIG[context?.criticalityLevel || 3];

  const copyToClipboard = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success("Copied to clipboard", { duration: 1500 });
    } catch (error) {
      toast.error("Failed to copy to clipboard");
    }
  };

  // Loading State
  if (isLoadingStructure) {
    return (
      <div className="flex flex-col h-screen bg-background animate-pulse">
        <div className="px-6 py-4 border-b space-y-2">
          <Skeleton className="h-4 w-32" />
        </div>
        <div className="px-8 py-8 border-b space-y-6">
          <div className="flex items-center gap-4">
            <Skeleton className="h-16 w-16 rounded-xl" />
            <div className="space-y-2">
              <Skeleton className="h-8 w-64" />
              <Skeleton className="h-4 w-40" />
            </div>
          </div>
        </div>
        <div className="p-8 space-y-6">
          <Skeleton className="h-10 w-96 rounded-lg" />
          <Skeleton className="h-[400px] w-full rounded-xl" />
        </div>
      </div>
    );
  }

  // Error State
  if (structureError || !entityData) {
    return (
      <div className="flex flex-col items-center justify-center h-screen p-6 text-center bg-background">
        <div className="w-16 h-16 rounded-full bg-destructive/10 flex items-center justify-center mb-6">
          <AlertCircle className="h-8 w-8 text-destructive" />
        </div>
        <h2 className="text-2xl font-semibold text-foreground mb-2">
          Failed to Load Entity
        </h2>
        <p className="text-muted-foreground mb-6 max-w-md">
          {structureError?.message ||
            "The requested entity could not be found or you don't have permission to view it."}
        </p>
        <Button asChild variant="outline">
          <Link to={`/project/${projectId}/entities`}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Explorer
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <TooltipProvider>
      <div className="flex flex-col h-[calc(100vh-114px)] bg-background overflow-hidden">
        {/* 1. Sticky Top Bar */}
        <div className="px-6 py-3 border-b flex items-center justify-between bg-background/95 backdrop-blur z-30 sticky top-0 supports-[backdrop-filter]:bg-background/60">
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbLink href={`/project/${projectId}/entities`}>
                  Explorer
                </BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                <BreadcrumbPage className="font-medium">
                  {fullName}
                </BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
          <div className="flex items-center gap-2">
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8"
                  onClick={() => copyToClipboard(fullName)}
                >
                  <Copy className="h-4 w-4 text-muted-foreground" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>Copy name</TooltipContent>
            </Tooltip>
            <Separator orientation="vertical" className="h-4 mx-1" />
            <Button size="sm" variant="default" asChild className="h-8">
              <Link
                to={`/project/${projectId}/impact/${entityType}/${entityId}`}
              >
                <Network className="h-3.5 w-3.5 mr-2" />
                Impact Analysis
              </Link>
            </Button>
          </div>
        </div>

        {/* Scrollable Content Area */}
        <div className="flex-1 overflow-y-auto">
          {/* 2. Hero Header */}
          <header className="px-8 py-8 border-b bg-gradient-to-b from-muted/20 to-background">
            <div className="flex flex-col xl:flex-row xl:items-start justify-between gap-8">
              {/* Identity Section */}
              <div className="flex items-start gap-5">
                <div
                  className={`p-4 rounded-xl border shadow-sm ${isTable
                    ? "bg-emerald-50 text-emerald-600 border-emerald-100 dark:bg-emerald-950/30 dark:border-emerald-900"
                    : "bg-violet-50 text-violet-600 border-violet-100 dark:bg-violet-950/30 dark:border-violet-900"
                    }`}
                >
                  {isTable ? (
                    <TableIcon className="h-8 w-8" />
                  ) : (
                    <Code2 className="h-8 w-8" />
                  )}
                </div>
                <div className="space-y-1.5">
                  <h1 className="text-2xl font-bold text-foreground tracking-tight">
                    {name}
                  </h1>
                  <div className="flex items-center flex-wrap gap-2">
                    <Badge
                      variant="outline"
                      className="font-mono text-xs bg-background"
                    >
                      {schema}
                    </Badge>
                    <Badge variant="secondary" className="text-xs">
                      {isTable ? "Table" : "Stored Procedure"}
                    </Badge>
                    {criticalityConfig && (
                      <Badge
                        variant={criticalityConfig.variant}
                        className="text-xs"
                      >
                        {criticalityConfig.label} Impact
                      </Badge>
                    )}
                  </div>
                </div>
              </div>

              {/* Stats Section - De-carded for cleaner look */}
              <div className="flex items-center divide-x border rounded-lg bg-background shadow-sm overflow-hidden">
                <div className="px-6 py-3 hover:bg-muted/50 transition-colors">
                  <StatItem
                    label={isTable ? "Columns" : "Params"}
                    value={
                      isTable
                        ? (columns?.length ?? 0)
                        : (parameters?.length ?? 0)
                    }
                    icon={<Layers className="h-3 w-3" />}
                  />
                </div>
                {isTable && (
                  <div className="px-6 py-3 hover:bg-muted/50 transition-colors">
                    <StatItem
                      label="Rows"
                      value={formatRowCount(rowCount)}
                      icon={<Database className="h-3 w-3" />}
                    />
                  </div>
                )}
                <div className="px-6 py-3 hover:bg-muted/50 transition-colors">
                  <StatItem
                    label="Experts"
                    value={experts.length}
                    icon={<Users className="h-3 w-3" />}
                  />
                </div>
                <div className="px-6 py-3 hover:bg-muted/50 transition-colors">
                  <StatItem
                    label="Docs"
                    value={`${contextData?.completenessScore ?? 0}%`}
                    icon={<Sparkles className="h-3 w-3" />}
                  />
                </div>
              </div>
            </div>
          </header>

          {/* 3. Main Content & Tabs */}
          <div className="p-8 max-w-[1920px] mx-auto">
            <Tabs defaultValue="structure" className="w-full space-y-6">
              <div className="flex items-center justify-between">
                <TabsList className="h-10 bg-muted/50 p-1">
                  <TabsTrigger value="structure" className="gap-2 text-xs">
                    <Layers className="h-3.5 w-3.5" /> Structure
                  </TabsTrigger>
                  {isTable && (
                    <TabsTrigger value="relationships" className="gap-2 text-xs">
                      <Link2 className="h-3.5 w-3.5" /> Relationships
                    </TabsTrigger>
                  )}
                  <TabsTrigger value="experts" className="gap-2 text-xs">
                    <Users className="h-3.5 w-3.5" /> Experts
                    <Badge
                      variant="secondary"
                      className="ml-1 h-5 px-1.5 min-w-[1.25rem]"
                    >
                      {experts.length}
                    </Badge>
                  </TabsTrigger>
                  <TabsTrigger value="documentation" className="gap-2 text-xs">
                    <FileText className="h-3.5 w-3.5" /> Documentation
                  </TabsTrigger>
                </TabsList>
                <span className="text-xs text-muted-foreground flex items-center gap-1.5">
                  <Calendar className="h-3.5 w-3.5" />
                  Created{" "}
                  {formatRelativeTime(entityData.createdDate || "", "Unknown")}
                </span>
              </div>

              {/* Structure Tab */}
              <TabsContent
                value="structure"
                className="mt-0 focus-visible:outline-none"
              >
                <div className="grid grid-cols-1 xl:grid-cols-4 gap-6">
                  {/* Left Column: Schema & Code (3 spans) */}
                  <div className="xl:col-span-3 space-y-6">
                    {/* Schema Table */}
                    <Card className="overflow-hidden border-border/60 shadow-sm">
                      <CardHeader className="bg-muted/30 pb-3 border-b">
                        <div className="flex items-center justify-between">
                          <div className="space-y-0.5">
                            <CardTitle className="text-sm font-semibold">
                              {isTable ? "Column Schema" : "Input Parameters"}
                            </CardTitle>
                            <CardDescription className="text-xs">
                              {isTable
                                ? `Defines structure for ${columns?.length ?? 0} columns`
                                : `Inputs required for execution`}
                            </CardDescription>
                          </div>
                        </div>
                      </CardHeader>
                      <div className="p-0">
                        <Table>
                          <TableHeader className="bg-muted/10">
                            <TableRow className="hover:bg-transparent">
                              <TableHead className="w-[250px] text-xs uppercase h-9">
                                Name
                              </TableHead>
                              <TableHead className="text-xs uppercase h-9">
                                Type
                              </TableHead>
                              <TableHead className="text-xs uppercase h-9">
                                {isTable ? "Nullable" : "Direction"}
                              </TableHead>
                              {isTable && (
                                <TableHead className="text-xs uppercase h-9">
                                  Attributes
                                </TableHead>
                              )}
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {(isTable ? columns : parameters)?.map(
                              (item: any, idx: number) => (
                                <TableRow
                                  key={item.name || idx}
                                  className="h-10"
                                >
                                  <TableCell className="font-mono text-sm font-medium text-foreground py-2">
                                    {item.name}
                                  </TableCell>
                                  <TableCell className="py-2">
                                    <code className="text-[11px] text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/40 px-1.5 py-0.5 rounded border border-blue-100 dark:border-blue-900">
                                      {item.dataType}
                                      {item.maxLength
                                        ? `(${item.maxLength})`
                                        : ""}
                                    </code>
                                  </TableCell>
                                  <TableCell className="py-2">
                                    {isTable ? (
                                      <span
                                        className={`text-xs ${item.isNullable ? "text-muted-foreground" : "font-medium text-foreground"}`}
                                      >
                                        {item.isNullable ? "NULL" : "NOT NULL"}
                                      </span>
                                    ) : (
                                      <Badge
                                        variant="outline"
                                        className="text-[10px] font-normal"
                                      >
                                        {item.direction}
                                      </Badge>
                                    )}
                                  </TableCell>
                                  {isTable && (
                                    <TableCell className="py-2">
                                      <div className="flex gap-1.5">
                                        {item.isPrimaryKey && (
                                          <Badge
                                            variant="default"
                                            className="text-[10px] h-5 px-1.5"
                                          >
                                            PK
                                          </Badge>
                                        )}
                                        {item.isForeignKey && (
                                          <Badge
                                            variant="secondary"
                                            className="text-[10px] h-5 px-1.5 text-muted-foreground border"
                                          >
                                            FK
                                          </Badge>
                                        )}
                                        {item.defaultValue && (
                                          <Badge
                                            variant="outline"
                                            className="text-[10px] h-5 px-1.5 font-mono text-muted-foreground"
                                          >
                                            def: {item.defaultValue}
                                          </Badge>
                                        )}
                                      </div>
                                    </TableCell>
                                  )}
                                </TableRow>
                              ),
                            )}
                          </TableBody>
                        </Table>
                      </div>
                    </Card>


                  </div>

                  {/* Right Column: Meta Info (1 span) */}
                  <div className="space-y-6">
                    <Card className="shadow-sm border-border/60">
                      <CardHeader className="pb-3 border-b bg-muted/10">
                        <CardTitle className="text-sm flex items-center gap-2">
                          <Info className="h-4 w-4 text-primary" />
                          Metadata
                        </CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-5 pt-5">
                        <div>
                          <p className="text-[10px] text-muted-foreground uppercase font-semibold tracking-wider mb-1">
                            Last Modified
                          </p>
                          <div className="flex items-center gap-2 text-sm text-foreground">
                            <Clock className="h-3.5 w-3.5 text-muted-foreground" />
                            {entityData.modifiedDate
                              ? new Date(
                                entityData.modifiedDate,
                              ).toLocaleDateString()
                              : "Never"}
                          </div>
                        </div>

                        <Separator />

                        <div>
                          <p className="text-[10px] text-muted-foreground uppercase font-semibold tracking-wider mb-1">
                            Domain
                          </p>
                          {context?.businessDomain ? (
                            <Badge
                              variant="secondary"
                              className="rounded-sm font-normal"
                            >
                              {context.businessDomain}
                            </Badge>
                          ) : (
                            <span className="text-sm text-muted-foreground italic">
                              Unassigned
                            </span>
                          )}
                        </div>

                        <Separator />

                        <div>
                          <p className="text-[10px] text-muted-foreground uppercase font-semibold tracking-wider mb-1">
                            Purpose Summary
                          </p>
                          <p className="text-sm text-muted-foreground leading-relaxed">
                            {context?.purpose || "No summary available."}
                          </p>
                        </div>
                      </CardContent>
                    </Card>
                  </div>
                </div>
              </TabsContent>

              {/* Relationships Tab (Tables only) */}
              {isTable && (
                <TabsContent
                  value="relationships"
                  className="mt-0 focus-visible:outline-none"
                >
                  <LogicalFkPanel
                    projectId={Number(projectId)}
                    tableId={Number(entityId)}
                    tableName={name || ""}
                    columns={
                      (columns ?? []).map((c) => ({
                        columnId: c.columnId ?? 0,
                        columnName: c.name,
                        dataType: c.dataType,
                        isPrimaryKey: c.isPrimaryKey,
                        isForeignKey: c.isForeignKey,
                      }))
                    }
                  />
                </TabsContent>
              )}

              {/* Experts Tab */}
              <TabsContent
                value="experts"
                className="mt-0 focus-visible:outline-none"
              >
                {isLoadingContext ? (
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {[1, 2, 3].map((i) => (
                      <Card key={i}>
                        <CardContent className="p-4">
                          <Skeleton className="h-20 w-full" />
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                ) : experts.length > 0 ? (
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {experts.map((expert) => (
                      <ExpertCard key={expert.userId} expert={expert} />
                    ))}
                  </div>
                ) : (
                  <EmptyExpertsState entityName={name || "this entity"} />
                )}
              </TabsContent>

              {/* Documentation Tab */}
              <TabsContent
                value="documentation"
                className="mt-0 focus-visible:outline-none"
              >
                {context?.purpose || context?.businessImpact ? (
                  <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    {context.purpose && (
                      <Card className="h-full border-l-4 border-l-primary shadow-sm">
                        <CardHeader>
                          <CardTitle className="text-base flex items-center gap-2">
                            <Sparkles className="h-4 w-4 text-primary" />{" "}
                            Business Purpose
                          </CardTitle>
                        </CardHeader>
                        <CardContent>
                          <p className="text-sm text-muted-foreground leading-7">
                            {context.purpose}
                          </p>
                        </CardContent>
                      </Card>
                    )}
                    {context.businessImpact && (
                      <Card className="h-full border-l-4 border-l-orange-500 shadow-sm">
                        <CardHeader>
                          <CardTitle className="text-base flex items-center gap-2">
                            <AlertCircle className="h-4 w-4 text-orange-500" />{" "}
                            Operational Impact
                          </CardTitle>
                        </CardHeader>
                        <CardContent>
                          <p className="text-sm text-muted-foreground leading-7">
                            {context.businessImpact}
                          </p>
                        </CardContent>
                      </Card>
                    )}
                  </div>
                ) : (
                  <DocumentationEmptyState entityName={name || "this entity"} />
                )}
              </TabsContent>
            </Tabs>
          </div>
        </div>
      </div>
    </TooltipProvider>
  );
};

export default EntityDetailPage;
