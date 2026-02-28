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
import { formatRelativeTime, utcToLocal } from "@/lib/utils";
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
  Maximize2,
  Minimize2,
} from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { toast } from "sonner";
import React, { lazy } from "react";
import { useProject } from "@/hooks/useProject";
import { useFullscreen } from "@/hooks/useFullscreen";
import LogicalFkPanel from "@/components/er-diagram/LogicalFkPanel";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Sheet, SheetContent } from "@/components/ui/sheet";
import { ExpertManagement } from "@/components/context/ExpertManagement";
import { ContextEditor } from "@/components/context/ContextEditorPanel";

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

type EntityType = "TABLE" | "SP";
type EntityTypeRouteSlug = "tables" | "stored-procedures";

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

const ROUTE_TO_ENTITY_TYPE: Record<EntityTypeRouteSlug, EntityType> = {
  tables: "TABLE",
  "stored-procedures": "SP",
};

function normalizeEntityType(entityTypeParam?: string): EntityType | null {
  if (!entityTypeParam) return null;
  return (
    ROUTE_TO_ENTITY_TYPE[entityTypeParam.toLowerCase() as EntityTypeRouteSlug] ??
    null
  );
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

export const EmptyExpertsState: React.FC<{ entityName: string, onAddClick?: () => void }> = ({
  entityName,
  onAddClick,
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
    {onAddClick && (
      <Button variant="outline" size="sm" onClick={onAddClick}>
        <UserIcon className="h-4 w-4 mr-2" />
        Assign Expert
      </Button>
    )}
  </div>
);

const DocumentationEmptyState: React.FC<{ entityName: string, onAddClick?: () => void }> = ({
  entityName,
  onAddClick,
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
    {onAddClick && (
      <Button size="sm" onClick={onAddClick}>
        <FileText className="h-4 w-4 mr-2" />
        Add Documentation
      </Button>
    )}
  </div>
);

// ============================================================================
// MAIN COMPONENT
// ============================================================================
const EntityDetailPage: React.FC = () => {
  const { projectId: projectIdParam, entityId: entityIdParam, entityType: entityTypeParam } = useParams<{
    projectId: string;
    entityId: string;
    entityType: string;
  }>();

  const { isFullscreen, toggleFullscreen } = useFullscreen();

  const [isExpertSheetOpen, setIsExpertSheetOpen] = React.useState(false);
  const [isDocSheetOpen, setIsDocSheetOpen] = React.useState(false);
  const [isEditorFullscreen, setIsEditorFullscreen] = React.useState(false);
  const toggleEditorFullscreen = React.useCallback(() => {
    setIsEditorFullscreen((prev) => !prev);
  }, []);

  const parsedProjectId = parseInt(projectIdParam ?? "", 10);
  const parsedEntityId = parseInt(entityIdParam ?? "", 10);
  const validIds = Number.isFinite(parsedProjectId) && Number.isFinite(parsedEntityId);

  const entityType = normalizeEntityType(entityTypeParam);
  const isTable = entityType === "TABLE";

  // Fetch Entity Structure
  const structureEndpoint = entityType
    ? isTable
      ? `/DatabaseBrowser/projects/${parsedProjectId}/tables/${parsedEntityId}`
      : `/DatabaseBrowser/projects/${parsedProjectId}/stored-procedures/${parsedEntityId}`
    : "";

  const {
    data: entityData,
    isLoading: isLoadingStructure,
    error: structureError,
  } = useApi<TableDetails | SPDetails>(structureEndpoint);

  // Fetch Context & Experts
  const { data: contextData, isLoading: isLoadingContext, refetch: refetchContext } =
    useApi<ContextResponse>(
      entityType ? `/projects/${parsedProjectId}/context/${entityType}/${parsedEntityId}` : "",
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

  if (!validIds || !entityType) {
    return (
      <div className="flex flex-col items-center justify-center h-screen p-6 text-center bg-background">
        <div className="w-16 h-16 rounded-full bg-destructive/10 flex items-center justify-center mb-6">
          <AlertCircle className="h-8 w-8 text-destructive" />
        </div>
        <h2 className="text-2xl font-semibold text-foreground mb-2">
          {!validIds ? "Invalid IDs" : "Invalid Entity Type"}
        </h2>
        <p className="text-muted-foreground mb-6 max-w-md">
          {!validIds
            ? "The route includes an invalid project ID or entity ID."
            : (
              <>
                The route includes an unsupported entity type:{" "}
                <code>{entityTypeParam || "(missing)"}</code>.
              </>
            )}
        </p>
        <Button asChild variant="outline">
          <Link to={validIds ? `/project/${parsedProjectId}/entities` : "/"}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            {validIds ? "Back to Explorer" : "Back to Home"}
          </Link>
        </Button>
      </div>
    );
  }

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
          <Link to={`/project/${parsedProjectId}/entities`}>
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Explorer
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <TooltipProvider>
      <div className={`flex flex-col overflow-hidden bg-background ${isFullscreen ? "h-screen" : "h-[calc(100vh-114px)]"}`}>
        {/* 1. Sticky Top Bar */}
        <div className="px-6 py-3 border-b flex items-center justify-between bg-background/95 backdrop-blur z-30 sticky top-0 supports-[backdrop-filter]:bg-background/60">
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbLink>
                  <Link to={`/project/${parsedProjectId}/entities`}>
                    Explorer
                  </Link>
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
                  aria-label="Copy full name"
                  title="Copy full name"
                >
                  <Copy className="h-4 w-4 text-muted-foreground" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>Copy name</TooltipContent>
            </Tooltip>

            {isFullscreen && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8"
                    onClick={toggleFullscreen}
                    aria-label="Exit fullscreen"
                    title="Exit fullscreen"
                  >
                    <Minimize2 className="h-4 w-4 text-muted-foreground" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Exit fullscreen</TooltipContent>
              </Tooltip>
            )}

            <Separator orientation="vertical" className="h-4 mx-1" />

            <Button size="sm" variant="default" asChild className="h-8">
              <Link
                to={`/project/${parsedProjectId}/impact/${entityType}/${parsedEntityId}`}
              >
                <Network className="h-3.5 w-3.5 mr-2" />
                Impact Analysis
              </Link>
            </Button>
          </div>
        </div>

        {/* Scrollable Content Area */}
        <ScrollArea className="flex-1 overflow-y-auto [&_[data-radix-scroll-area-viewport]>div]:!block">

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
          <div className="min-h-screen bg-background/50 p-4 sm:p-6 lg:p-8">
            <div className="max-w-7xl mx-auto space-y-8">

              <Tabs defaultValue="structure" className="w-full">
                {/* Header Area: Tabs on left, Date on right */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
                  <TabsList className="h-9 bg-muted/60 p-1 w-fit">
                    <TabsTrigger value="structure" className="gap-2 text-xs data-[state=active]:bg-background data-[state=active]:shadow-sm">
                      <Layers className="h-3.5 w-3.5" /> Structure
                    </TabsTrigger>
                    {isTable && (
                      <TabsTrigger value="relationships" className="gap-2 text-xs data-[state=active]:bg-background data-[state=active]:shadow-sm">
                        <Link2 className="h-3.5 w-3.5" /> Relationships
                      </TabsTrigger>
                    )}
                    <TabsTrigger value="experts" className="gap-2 text-xs data-[state=active]:bg-background data-[state=active]:shadow-sm">
                      <Users className="h-3.5 w-3.5" /> Experts
                      <Badge
                        variant="secondary"
                        className="ml-1 h-5 px-1.5 min-w-[1.25rem] text-[10px] bg-muted-foreground/10 text-muted-foreground"
                      >
                        {experts.length}
                      </Badge>
                    </TabsTrigger>
                    <TabsTrigger value="documentation" className="gap-2 text-xs data-[state=active]:bg-background data-[state=active]:shadow-sm">
                      <FileText className="h-3.5 w-3.5" /> Documentation
                    </TabsTrigger>
                  </TabsList>

                  <span className="text-xs font-medium text-muted-foreground flex items-center gap-2 bg-muted/30 px-3 py-1.5 rounded-full border border-border/40">
                    <Calendar className="h-3.5 w-3.5" />
                    Created {formatRelativeTime(entityData.createdDate || "", "Unknown")}
                  </span>
                </div>

                {/* Structure Tab */}
                <TabsContent value="structure" className="mt-0 focus-visible:outline-none">
                  <div className="grid grid-cols-1 xl:grid-cols-12 gap-6">

                    {/* Main Column: Schema & Code (9 cols) */}
                    <div className="xl:col-span-9 space-y-6">

                      {/* Schema Table */}
                      <Card className="border border-border/60 shadow-sm bg-card">
                        <CardHeader className="py-4 px-5 border-b bg-muted/5">
                          <div className="flex items-center justify-between">
                            <div className="space-y-1">
                              <CardTitle className="text-sm font-semibold tracking-tight">
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
                            <TableHeader>
                              <TableRow className="hover:bg-transparent border-b border-border/60">
                                <TableHead className="w-[250px] text-xs font-semibold text-muted-foreground h-10 pl-5">Name</TableHead>
                                <TableHead className="text-xs font-semibold text-muted-foreground h-10">Type</TableHead>
                                <TableHead className="text-xs font-semibold text-muted-foreground h-10">{isTable ? "Nullable" : "Direction"}</TableHead>
                                {isTable && <TableHead className="text-xs font-semibold text-muted-foreground h-10">Attributes</TableHead>}
                              </TableRow>
                            </TableHeader>
                            <TableBody>
                              {(isTable ? columns : parameters)?.map((item: any, idx: number) => (
                                <TableRow key={item.name || idx} className="h-11 border-b border-border/40 hover:bg-muted/30 transition-colors">
                                  <TableCell className="font-mono text-sm font-medium text-foreground py-2 pl-5">
                                    {item.name}
                                  </TableCell>
                                  <TableCell className="py-2">
                                    <code className="text-[11px] font-medium text-primary bg-primary/10 px-2 py-0.5 rounded-md border border-primary/20">
                                      {item.dataType}
                                      {item.maxLength ? `(${item.maxLength})` : ""}
                                    </code>
                                  </TableCell>
                                  <TableCell className="py-2">
                                    {isTable ? (
                                      <span className={`text-xs inline-flex items-center ${item.isNullable ? "text-muted-foreground" : "font-medium text-foreground"}`}>
                                        {item.isNullable ? "NULL" : "NOT NULL"}
                                      </span>
                                    ) : (
                                      <Badge variant="outline" className="text-[10px] font-normal bg-background">
                                        {item.direction}
                                      </Badge>
                                    )}
                                  </TableCell>
                                  {isTable && (
                                    <TableCell className="py-2">
                                      <div className="flex flex-wrap gap-2">
                                        {item.isPrimaryKey && (
                                          <Badge variant="default" className="text-[10px] h-5 px-1.5 bg-primary/90 hover:bg-primary">PK</Badge>
                                        )}
                                        {item.isForeignKey && (
                                          <Badge variant="secondary" className="text-[10px] h-5 px-1.5 text-foreground bg-muted hover:bg-muted/80 border border-border/50">FK</Badge>
                                        )}
                                        {item.defaultValue && (
                                          <Badge variant="outline" className="text-[10px] h-5 px-1.5 font-mono text-muted-foreground bg-transparent">
                                            def: {item.defaultValue}
                                          </Badge>
                                        )}
                                      </div>
                                    </TableCell>
                                  )}
                                </TableRow>
                              ))}
                            </TableBody>
                          </Table>
                        </div>
                      </Card>

                      {!isTable && (
                        <Card className="border border-border/60 shadow-sm overflow-hidden">
                          <CardHeader className="py-3 px-5 border-b bg-muted/5 flex flex-row items-center justify-between space-y-0">
                            <div className="space-y-1">
                              <CardTitle className="text-sm font-semibold">Source Definition</CardTitle>
                              <CardDescription className="text-xs">SQL used to create this entity</CardDescription>
                            </div>
                            <div className="flex items-center gap-2">
                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <Button
                                    variant="outline"
                                    size="icon"
                                    className="h-7 w-7 text-xs bg-background hover:bg-accent hover:text-accent-foreground"
                                    onClick={toggleEditorFullscreen}
                                    aria-label={isEditorFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
                                    title={isEditorFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
                                  >
                                    {isEditorFullscreen ? (
                                      <Minimize2 className="h-3.5 w-3.5" />
                                    ) : (
                                      <Maximize2 className="h-3.5 w-3.5" />
                                    )}
                                  </Button>
                                </TooltipTrigger>
                                <TooltipContent>
                                  {isEditorFullscreen ? "Exit fullscreen" : "Enter fullscreen"}
                                </TooltipContent>
                              </Tooltip>

                              <Tooltip>
                                <TooltipTrigger asChild>
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    className="h-7 text-xs bg-background hover:bg-accent hover:text-accent-foreground"
                                    onClick={() => copyToClipboard(definition || "")}
                                    aria-label="Copy SQL"
                                    title="Copy SQL"
                                  >
                                    <Copy className="h-3.5 w-3.5 mr-1.5" />
                                  </Button>
                                </TooltipTrigger>
                                <TooltipContent>Copy SQL</TooltipContent>
                              </Tooltip>
                            </div>
                          </CardHeader>
                          <div className={isEditorFullscreen ? "fixed inset-0 z-[100] bg-background flex flex-col" : "bg-[#1e1e1e] p-0"}>
                            {isEditorFullscreen && (
                              <div className="py-3 px-5 border-b bg-muted/5 flex flex-row items-center justify-between">
                                <div className="space-y-1">
                                  <h3 className="text-sm font-semibold">Source Definition: {name}</h3>
                                </div>
                                <div className="flex items-center gap-2">
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    className="h-7 text-xs bg-background hover:bg-accent hover:text-accent-foreground"
                                    onClick={() => copyToClipboard(definition || "")}
                                    aria-label="Copy SQL"
                                    title="Copy SQL"
                                  >
                                    <Copy className="h-3.5 w-3.5 mr-1.5" />
                                  </Button>

                                  <Button
                                    variant="outline"
                                    size="sm"
                                    className="h-7 text-xs bg-background hover:bg-accent hover:text-accent-foreground"
                                    onClick={toggleEditorFullscreen}
                                    aria-label="Exit fullscreen"
                                    title="Exit fullscreen"
                                  >
                                    <Minimize2 className="h-3.5 w-3.5 mr-1.5" />
                                  </Button>
                                </div>
                              </div>
                            )}
                            <div className={isEditorFullscreen ? "flex-1 w-full" : "h-[500px] w-full"}>
                              <Editor
                                height="100%"
                                defaultLanguage="sql"
                                theme="vs-dark"
                                value={definition || "-- Definition not available"}
                                options={{
                                  readOnly: true,
                                  minimap: { enabled: false },
                                  fontSize: 13,
                                  fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
                                  scrollBeyondLastLine: false,
                                  automaticLayout: true,
                                  padding: { top: 20, bottom: 20 },
                                  lineNumbers: "on",
                                  renderLineHighlight: "none",
                                  overviewRulerBorder: false,
                                  matchBrackets: "never",
                                }}
                              />
                            </div>
                          </div>
                        </Card>
                      )}
                    </div>

                    {/* Right Column: Meta Info (3 cols) */}
                    <div className="xl:col-span-3 space-y-6">
                      <Card className="border border-border/60 shadow-sm sticky top-6">
                        <CardHeader className="py-4 border-b bg-muted/10">
                          <CardTitle className="text-sm font-semibold flex items-center gap-2">
                            <Info className="h-4 w-4 text-primary" />
                            Metadata
                          </CardTitle>
                        </CardHeader>
                        <CardContent className="p-5 space-y-6">

                          <div className="space-y-2">
                            <p className="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">
                              Last Modified
                            </p>
                            <div className="flex items-center gap-2 p-2 rounded-md bg-muted/40 border border-border/50">
                              <Clock className="h-4 w-4 text-muted-foreground" />
                              <span className="text-sm font-medium">
                                {utcToLocal(entityData.modifiedDate, "PPP", "Never")}
                              </span>
                            </div>
                          </div>

                          <div className="space-y-2">
                            <p className="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">
                              Domain
                            </p>
                            <div>
                              {context?.businessDomain ? (
                                <Badge variant="secondary" className="rounded-md px-2 py-1 font-medium bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300 border-blue-100 dark:border-blue-800">
                                  {context.businessDomain}
                                </Badge>
                              ) : (
                                <span className="text-sm text-muted-foreground italic px-2">Unassigned</span>
                              )}
                            </div>
                          </div>

                          <div className="space-y-2">
                            <p className="text-[10px] text-muted-foreground uppercase font-bold tracking-wider">
                              Purpose Summary
                            </p>
                            <div className="text-sm text-muted-foreground leading-relaxed p-2">
                              {context?.purpose || "No summary available."}
                            </div>
                          </div>

                        </CardContent>
                      </Card>
                    </div>
                  </div>
                </TabsContent>

                {/* Relationships Tab */}
                {isTable && (
                  <TabsContent value="relationships" className="mt-0 focus-visible:outline-none">
                    <LogicalFkPanel
                      projectId={parsedProjectId}
                      tableId={parsedEntityId}
                      tableName={name || ""}
                      columns={(columns ?? [])
                        .filter((c) => c.columnId != null)
                        .map((c) => ({
                          columnId: c.columnId!,
                          columnName: c.name,
                          dataType: c.dataType,
                          isPrimaryKey: c.isPrimaryKey,
                          isForeignKey: c.isForeignKey,
                        }))}
                    />
                  </TabsContent>
                )}

                {/* Experts Tab */}
                <TabsContent value="experts" className="mt-0 focus-visible:outline-none">
                  {isLoadingContext ? (
                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                      {[1, 2, 3].map((i) => (
                        <Card key={i} className="border-border/40 shadow-sm">
                          <CardContent className="p-6">
                            <Skeleton className="h-20 w-full" />
                          </CardContent>
                        </Card>
                      ))}
                    </div>
                  ) : experts.length > 0 ? (
                    <div className="space-y-6">
                      <div className="flex items-center justify-between">
                        <p className="text-sm text-muted-foreground">
                          {experts.length} expert{experts.length !== 1 && "s"} assigned to this entity.
                        </p>
                        <Button variant="outline" size="sm" onClick={() => setIsExpertSheetOpen(true)}>
                          <UserIcon className="h-4 w-4 mr-2" />
                          Manage Experts
                        </Button>
                      </div>
                      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                        {experts.map((expert) => (
                          <ExpertCard key={expert.userId} expert={expert} />
                        ))}
                      </div>
                    </div>
                  ) : (
                    <EmptyExpertsState entityName={name || "this entity"} onAddClick={() => setIsExpertSheetOpen(true)} />
                  )}
                </TabsContent>

                {/* Documentation Tab */}
                <TabsContent value="documentation" className="mt-0 focus-visible:outline-none">
                  {context?.purpose || context?.businessImpact ? (
                    <div className="space-y-6">
                      <div className="flex items-center justify-between">
                        <p className="text-sm text-muted-foreground">
                          Context and documentation for this entity.
                        </p>
                        <Button variant="outline" size="sm" onClick={() => setIsDocSheetOpen(true)}>
                          <FileText className="h-4 w-4 mr-2" />
                          Edit Documentation
                        </Button>
                      </div>
                      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        {context.purpose && (
                          <Card className="h-full border-l-4 border-l-primary shadow-sm hover:shadow-md transition-shadow">
                            <CardHeader className="pb-3">
                              <CardTitle className="text-base flex items-center gap-2 text-primary">
                                <Sparkles className="h-4 w-4" /> Business Purpose
                              </CardTitle>
                            </CardHeader>
                            <CardContent>
                              <p className="text-sm text-muted-foreground leading-7">{context.purpose}</p>
                            </CardContent>
                          </Card>
                        )}
                        {context.businessImpact && (
                          <Card className="h-full border-l-4 border-l-orange-500 shadow-sm hover:shadow-md transition-shadow">
                            <CardHeader className="pb-3">
                              <CardTitle className="text-base flex items-center gap-2 text-orange-600 dark:text-orange-400">
                                <AlertCircle className="h-4 w-4" /> Operational Impact
                              </CardTitle>
                            </CardHeader>
                            <CardContent>
                              <p className="text-sm text-muted-foreground leading-7">{context.businessImpact}</p>
                            </CardContent>
                          </Card>
                        )}
                      </div>
                    </div>
                  ) : (
                    <DocumentationEmptyState entityName={name || "this entity"} onAddClick={() => setIsDocSheetOpen(true)} />
                  )}
                </TabsContent>
              </Tabs>
            </div>
          </div>

        </ScrollArea>
      </div>

      {/* Sheets for pop-out editing */}
      <Sheet open={isExpertSheetOpen} onOpenChange={setIsExpertSheetOpen}>
        <SheetContent className="sm:max-w-md p-0 overflow-y-auto w-full">
          <ExpertManagement
            entityType={entityType}
            entityId={parsedEntityId}
            entityName={name || ""}
          />
        </SheetContent>
      </Sheet>

      <Sheet open={isDocSheetOpen} onOpenChange={setIsDocSheetOpen}>
        <SheetContent className="sm:max-w-xl md:max-w-2xl p-0 overflow-y-auto w-full">
          <ContextEditor
            projectId={parsedProjectId}
            entityType={entityType}
            entityId={parsedEntityId}
            entityName={name || ""}
            onSave={() => refetchContext()}
          />
        </SheetContent>
      </Sheet>
    </TooltipProvider>
  );
};

export default EntityDetailPage;
