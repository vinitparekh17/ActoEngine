import React from "react";
import { useParams, Link, useLocation } from "react-router-dom";
import { useApi } from "@/hooks/useApi";
import { formatRelativeTime } from "@/lib/utils";
import {
    Database,
    Table as TableIcon,
    Code2,
    Copy,
    User as UserIcon,
    Info,
    Users,
    FileText,
    Network,
    Sparkles,
    Mail,
    Shield,
    Layers,
    BookOpen,
    AlertCircle,
    Calendar,
    ArrowLeft,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
    CardDescription,
} from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeletons";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table";
import {
    Tooltip,
    TooltipContent,
    TooltipProvider,
    TooltipTrigger,
} from "@/components/ui/tooltip";
import { toast } from "sonner";
import Editor from "@monaco-editor/react";
import {
    Breadcrumb,
    BreadcrumbItem,
    BreadcrumbLink,
    BreadcrumbList,
    BreadcrumbPage,
    BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";

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
    spId: number;
    procedureName: string;
    schemaName?: string;
    definition?: string;
    createdDate?: string;
    modifiedDate?: string;
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
    assignedAt: string;
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
        dataOwner?: string;
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
    if (!count) return "—";
    if (count >= 1_000_000) return `${(count / 1_000_000).toFixed(1)}M`;
    if (count >= 1_000) return `${(count / 1_000).toFixed(1)}K`;
    return count.toLocaleString();
}

const EXPERTISE_CONFIG = {
    OWNER: {
        label: "Owner",
        variant: "default" as const,
        icon: Shield,
    },
    EXPERT: {
        label: "Expert",
        variant: "secondary" as const,
        icon: Sparkles,
    },
    FAMILIAR: {
        label: "Familiar",
        variant: "outline" as const,
        icon: UserIcon,
    },
    CONTRIBUTOR: {
        label: "Contributor",
        variant: "outline" as const,
        icon: Code2,
    },
};

const CRITICALITY_CONFIG: Record<
    number,
    { label: string; variant: "default" | "secondary" | "destructive" | "outline" }
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
    <div className="flex flex-col items-center px-6 py-3">
        <span className="text-xs text-muted-foreground uppercase font-medium tracking-wide mb-1 flex items-center gap-1.5">
            {icon}
            {label}
        </span>
        <span className="text-xl font-semibold text-foreground">{value}</span>
    </div>
);

const ExpertCard: React.FC<{ expert: Expert }> = ({ expert }) => {
    const config = EXPERTISE_CONFIG[expert.expertiseLevel] || EXPERTISE_CONFIG.FAMILIAR;
    const Icon = config.icon;
    const displayName = expert.user.fullName || expert.user.username;

    return (
        <Card className="hover:shadow-md transition-shadow">
            <CardContent className="p-4">
                <div className="flex items-start gap-3">
                    <Avatar className="h-10 w-10">
                        <AvatarFallback className="bg-muted text-muted-foreground font-medium text-sm">
                            {getInitials(displayName)}
                        </AvatarFallback>
                    </Avatar>
                    <div className="flex-1 min-w-0 space-y-1">
                        <div className="flex items-center justify-between gap-2">
                            <h4 className="font-medium text-foreground truncate text-sm">
                                {displayName}
                            </h4>
                            <Badge variant={config.variant} className="text-xs shrink-0">
                                <Icon className="h-3 w-3 mr-1" />
                                {config.label}
                            </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground truncate">
                            {expert.user.email}
                        </p>
                        {expert.notes && (
                            <p className="text-xs text-muted-foreground italic line-clamp-2 pt-1">
                                "{expert.notes}"
                            </p>
                        )}
                        <div className="flex items-center justify-between pt-2 mt-2 border-t">
                            <span className="text-xs text-muted-foreground">
                                {formatRelativeTime(expert.assignedAt, "recently")}
                            </span>
                            <Button variant="ghost" size="sm" className="h-6 text-xs px-2" asChild>
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

const EmptyExpertsState: React.FC<{ entityName: string }> = ({ entityName }) => {
    const handleAssignExpert = () => {
        toast.info("Expert assignment feature - please use the Context Editor to add experts");
    };

    return (
        <Card className="border-dashed">
            <CardContent className="flex flex-col items-center justify-center py-16 text-center">
                <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mb-4">
                    <Users className="h-6 w-6 text-muted-foreground" />
                </div>
                <h3 className="text-base font-semibold text-foreground mb-2">
                    No Experts Assigned
                </h3>
                <p className="text-sm text-muted-foreground max-w-sm mb-4">
                    Help your team by identifying who knows about{" "}
                    <span className="font-medium">{entityName}</span>.
                </p>
                <Button variant="outline" size="sm" onClick={handleAssignExpert}>
                    <UserIcon className="h-4 w-4 mr-2" />
                    Assign Expert
                </Button>
            </CardContent>
        </Card>
    );
};

const DocumentationEmptyState: React.FC<{ entityName: string }> = ({
    entityName,
}) => {
    const handleAddDocumentation = () => {
        toast.info("Documentation feature - please use the Context Editor to add documentation");
    };

    return (
        <Card className="border-dashed">
            <CardContent className="flex flex-col items-center justify-center py-16 text-center">
                <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mb-4">
                    <BookOpen className="h-6 w-6 text-muted-foreground" />
                </div>
                <h3 className="text-base font-semibold text-foreground mb-2">
                    No Documentation Yet
                </h3>
                <p className="text-sm text-muted-foreground max-w-sm mb-4">
                    Document the business purpose and usage patterns for{" "}
                    <span className="font-medium">{entityName}</span>.
                </p>
                <Button size="sm" onClick={handleAddDocumentation}>
                    <FileText className="h-4 w-4 mr-2" />
                    Add Documentation
                </Button>
            </CardContent>
        </Card>
    );
};

// ============================================================================
// MAIN COMPONENT
// ============================================================================

const EntityDetailPage: React.FC = () => {
    const { projectId, entityId } = useParams<{
        projectId: string;
        entityId: string;
    }>();
    const { pathname } = useLocation();
    const isTable = pathname.includes("/tables/");
    const entityType = isTable ? "TABLE" : "SP";

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
            { staleTime: 30 * 1000, retry: 1 }
        );

    // Derived values
    const name = isTable
        ? (entityData as TableDetails)?.tableName
        : (entityData as SPDetails)?.procedureName;
    const schema = entityData?.schemaName || "dbo";
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

    const copyToClipboard = (text: string) => {
        navigator.clipboard.writeText(text);
        toast.success("Copied to clipboard", { duration: 1500 });
    };

    // Loading State
    if (isLoadingStructure) {
        return (
            <div className="flex flex-col h-full bg-background">
                <div className="px-6 py-3 border-b">
                    <Skeleton className="h-5 w-48" />
                </div>
                <div className="px-6 py-6 border-b">
                    <div className="flex items-center gap-4 mb-6">
                        <Skeleton className="h-12 w-12 rounded-lg" />
                        <div className="space-y-2">
                            <Skeleton className="h-7 w-56" />
                            <Skeleton className="h-4 w-32" />
                        </div>
                    </div>
                    <Skeleton className="h-14 w-full rounded-lg" />
                </div>
                <div className="flex-1 p-6">
                    <Skeleton className="h-10 w-80 mb-6" />
                    <Skeleton className="h-96 w-full rounded-lg" />
                </div>
            </div>
        );
    }

    // Error State
    if (structureError || !entityData) {
        return (
            <div className="flex flex-col items-center justify-center h-full p-6 text-center">
                <div className="w-16 h-16 rounded-full bg-destructive/10 flex items-center justify-center mb-4">
                    <AlertCircle className="h-8 w-8 text-destructive" />
                </div>
                <h2 className="text-xl font-semibold text-foreground mb-2">
                    Failed to Load Entity
                </h2>
                <p className="text-sm text-muted-foreground mb-4 max-w-md">
                    {structureError?.message ||
                        "The requested entity could not be found."}
                </p>
                <Button asChild variant="outline" size="sm">
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
            <div className="flex flex-col h-full bg-background">
                {/* Breadcrumb Bar */}
                <div className="px-6 py-3 border-b flex items-center justify-between bg-background sticky top-0 z-20">
                    <Breadcrumb>
                        <BreadcrumbList>
                            <BreadcrumbItem>
                                <BreadcrumbLink href={`/project/${projectId}/entities`}>Explorer</BreadcrumbLink>
                            </BreadcrumbItem>
                            <BreadcrumbSeparator />
                            <BreadcrumbItem>
                                <BreadcrumbPage>{fullName}</BreadcrumbPage>
                            </BreadcrumbItem>
                        </BreadcrumbList>
                    </Breadcrumb>
                    <div className="flex items-center gap-2">
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => copyToClipboard(fullName)}
                                >
                                    <Copy className="h-4 w-4" />
                                </Button>
                            </TooltipTrigger>
                            <TooltipContent>Copy name</TooltipContent>
                        </Tooltip>
                        <Button size="sm" asChild>
                            <Link to={`/project/${projectId}/impact/${entityType}/${entityId}`}>
                                <Network className="h-4 w-4 mr-1.5" />
                                Impact Analysis
                            </Link>
                        </Button>
                    </div>
                </div>


                {/* Hero Section */}
                <header className="px-6 py-6 border-b bg-muted/30">
                    <div className="flex items-start justify-between gap-6">
                        {/* Identity */}
                        <div className="flex items-start gap-4">
                            <div
                                className={`p-3 rounded-lg border ${isTable
                                    ? "bg-emerald-50 text-emerald-600 border-emerald-200"
                                    : "bg-violet-50 text-violet-600 border-violet-200"
                                    }`}
                            >
                                {isTable ? (
                                    <TableIcon className="h-6 w-6" />
                                ) : (
                                    <Code2 className="h-6 w-6" />
                                )}
                            </div>
                            <div>
                                <h1 className="text-2xl font-semibold text-foreground tracking-tight">
                                    {name}
                                </h1>
                                <div className="flex items-center gap-2 mt-2">
                                    <Badge variant="outline" className="font-mono text-xs">
                                        {schema}
                                    </Badge>
                                    <Badge variant="secondary" className="text-xs">
                                        {isTable ? "Table" : "Stored Procedure"}
                                    </Badge>
                                    {criticalityConfig && (
                                        <Badge variant={criticalityConfig.variant} className="text-xs">
                                            {criticalityConfig.label}
                                        </Badge>
                                    )}
                                </div>
                            </div>
                        </div>

                        {/* Stats Bar */}
                        <Card className="mt-6">
                            <CardContent className="p-0 flex divide-x">
                                <StatItem
                                    label={isTable ? "Columns" : "Parameters"}
                                    value={isTable ? (columns?.length ?? 0) : (parameters?.length ?? 0)}
                                    icon={<Layers className="h-3 w-3" />}
                                />
                                {isTable && (
                                    <StatItem
                                        label="Est. Rows"
                                        value={formatRowCount(rowCount)}
                                        icon={<Database className="h-3 w-3" />}
                                    />
                                )}
                                <StatItem
                                    label="Experts"
                                    value={experts.length}
                                    icon={<Users className="h-3 w-3" />}
                                />
                                <StatItem
                                    label="Context"
                                    value={`${contextData?.completenessScore ?? 0}%`}
                                    icon={<Sparkles className="h-3 w-3" />}
                                />
                                <StatItem
                                    label="Created Date"
                                    value={formatRelativeTime(entityData.createdDate || "", "Unknown")}
                                    icon={<Calendar className="h-3 w-3" />}
                                />
                            </CardContent>
                        </Card>
                    </div>


                </header>

                {/* Tabs */}
                <div className="flex-1 overflow-hidden flex flex-col mt-6">
                    <Tabs defaultValue="structure" className="flex-1 flex flex-col">
                        <div className="px-6 border-b bg-background">
                            <TabsList className="h-11 bg-transparent p-0 gap-0">
                                {[
                                    { value: "structure", icon: Layers, label: "Structure" },
                                    {
                                        value: "experts",
                                        icon: Users,
                                        label: `Experts (${experts.length})`,
                                    },
                                    { value: "documentation", icon: FileText, label: "Documentation" },
                                ].map((tab) => (
                                    <TabsTrigger
                                        key={tab.value}
                                        value={tab.value}
                                        className="h-full border-b-2 border-transparent data-[state=active]:border-primary data-[state=active]:bg-transparent px-4 gap-2"
                                    >
                                        <tab.icon className="h-4 w-4" />
                                        {tab.label}
                                    </TabsTrigger>
                                ))}
                            </TabsList>
                        </div>

                        <div className="flex-1 overflow-auto">
                            {/* Structure Tab */}
                            <TabsContent value="structure" className="m-0 p-6">
                                <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
                                    <div className="xl:col-span-2 space-y-6">
                                        {/* Schema Table */}
                                        <Card>
                                            <CardHeader className="pb-3">
                                                <CardTitle className="text-base">
                                                    {isTable ? "Column Schema" : "Parameters"}
                                                </CardTitle>
                                                <CardDescription>
                                                    {isTable
                                                        ? `${columns?.length ?? 0} columns`
                                                        : `${parameters?.length ?? 0} parameters`}
                                                </CardDescription>
                                            </CardHeader>
                                            <CardContent className="p-0">
                                                <Table>
                                                    <TableHeader>
                                                        <TableRow>
                                                            <TableHead className="w-[200px]">Name</TableHead>
                                                            <TableHead>Type</TableHead>
                                                            <TableHead>
                                                                {isTable ? "Nullable" : "Direction"}
                                                            </TableHead>
                                                            {isTable && <TableHead>Constraints</TableHead>}
                                                        </TableRow>
                                                    </TableHeader>
                                                    <TableBody>
                                                        {(isTable ? columns : parameters)?.map(
                                                            (item: any, idx: number) => (
                                                                <TableRow key={item.name || idx}>
                                                                    <TableCell className="font-mono font-medium">
                                                                        {item.name}
                                                                    </TableCell>
                                                                    <TableCell>
                                                                        <code className="text-xs text-muted-foreground bg-muted px-1.5 py-0.5 rounded">
                                                                            {item.dataType}
                                                                        </code>
                                                                    </TableCell>
                                                                    <TableCell>
                                                                        {isTable ? (
                                                                            <span
                                                                                className={
                                                                                    item.isNullable
                                                                                        ? "text-muted-foreground"
                                                                                        : "font-medium"
                                                                                }
                                                                            >
                                                                                {item.isNullable ? "NULL" : "NOT NULL"}
                                                                            </span>
                                                                        ) : (
                                                                            <Badge variant="outline" className="text-xs">
                                                                                {item.direction}
                                                                            </Badge>
                                                                        )}
                                                                    </TableCell>
                                                                    {isTable && (
                                                                        <TableCell>
                                                                            <div className="flex gap-1">
                                                                                {item.isPrimaryKey && (
                                                                                    <Badge
                                                                                        variant="secondary"
                                                                                        className="text-xs"
                                                                                    >
                                                                                        PK
                                                                                    </Badge>
                                                                                )}
                                                                                {item.isForeignKey && (
                                                                                    <Badge variant="outline" className="text-xs">
                                                                                        FK
                                                                                    </Badge>
                                                                                )}
                                                                            </div>
                                                                        </TableCell>
                                                                    )}
                                                                </TableRow>
                                                            )
                                                        )}
                                                    </TableBody>
                                                </Table>
                                            </CardContent>
                                        </Card>

                                        {/* SQL Definition */}
                                        <Card>
                                            <CardHeader className="flex flex-row items-center justify-between pb-3">
                                                <div>
                                                    <CardTitle className="text-base">
                                                        {isTable ? "DDL" : "Definition"}
                                                    </CardTitle>
                                                    <CardDescription>SQL source code</CardDescription>
                                                </div>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() =>
                                                        copyToClipboard(isTable ? (ddl || "") : (definition || ""))
                                                    }
                                                >
                                                    <Copy className="h-4 w-4 mr-1.5" />
                                                    Copy
                                                </Button>
                                            </CardHeader>
                                            <div className="border-t">
                                                <div className="h-[400px]">
                                                    <Editor
                                                        height="100%"
                                                        defaultLanguage="sql"
                                                        theme="vs-dark"
                                                        value={
                                                            isTable
                                                                ? ddl || "-- DDL not available"
                                                                : definition || "-- Definition not available"
                                                        }
                                                        options={{
                                                            readOnly: true,
                                                            minimap: { enabled: false },
                                                            fontSize: 13,
                                                            fontFamily: "'JetBrains Mono', monospace",
                                                            scrollBeyondLastLine: false,
                                                            automaticLayout: true,
                                                            padding: { top: 16, bottom: 16 },
                                                            lineNumbers: "on",
                                                            folding: true,
                                                            renderLineHighlight: "none",
                                                        }}
                                                    />
                                                </div>
                                            </div>
                                        </Card>
                                    </div>

                                    {/* Sidebar */}
                                    <div className="space-y-6">
                                        <Card>
                                            <CardHeader className="pb-3">
                                                <CardTitle className="text-sm flex items-center gap-2">
                                                    <Info className="h-4 w-4 text-muted-foreground" />
                                                    Quick Info
                                                </CardTitle>
                                            </CardHeader>
                                            <CardContent className="space-y-4">
                                                <div>
                                                    <p className="text-xs text-muted-foreground uppercase font-medium">
                                                        Last Modified
                                                    </p>
                                                    <p className="text-sm mt-0.5">
                                                        {entityData.modifiedDate
                                                            ? new Date(entityData.modifiedDate).toLocaleDateString()
                                                            : "Never"}
                                                    </p>
                                                </div>
                                                {context?.purpose && (
                                                    <>
                                                        <Separator />
                                                        <div>
                                                            <p className="text-xs text-muted-foreground uppercase font-medium">
                                                                Purpose
                                                            </p>
                                                            <p className="text-sm mt-0.5">{context.purpose}</p>
                                                        </div>
                                                    </>
                                                )}
                                                {context?.businessDomain && (
                                                    <>
                                                        <Separator />
                                                        <div className="flex items-center justify-between">
                                                            <p className="text-xs text-muted-foreground uppercase font-medium">
                                                                Domain
                                                            </p>
                                                            <Badge variant="secondary" className="text-xs">
                                                                {context.businessDomain}
                                                            </Badge>
                                                        </div>
                                                    </>
                                                )}
                                            </CardContent>
                                        </Card>
                                    </div>
                                </div>
                            </TabsContent>

                            {/* Experts Tab */}
                            <TabsContent value="experts" className="m-0 p-6">
                                {isLoadingContext ? (
                                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                                        {[1, 2, 3].map((i) => (
                                            <Card key={i}>
                                                <CardContent className="p-4">
                                                    <div className="flex items-start gap-3">
                                                        <Skeleton className="h-10 w-10 rounded-full" />
                                                        <div className="flex-1 space-y-2">
                                                            <Skeleton className="h-4 w-28" />
                                                            <Skeleton className="h-3 w-40" />
                                                        </div>
                                                    </div>
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
                            <TabsContent value="documentation" className="m-0 p-6">
                                {context?.purpose || context?.businessImpact ? (
                                    <div className="max-w-2xl space-y-6">
                                        {context.purpose && (
                                            <Card>
                                                <CardHeader className="pb-2">
                                                    <CardTitle className="text-base flex items-center gap-2">
                                                        <Sparkles className="h-4 w-4 text-primary" />
                                                        Purpose
                                                    </CardTitle>
                                                </CardHeader>
                                                <CardContent>
                                                    <p className="text-sm text-foreground leading-relaxed">
                                                        {context.purpose}
                                                    </p>
                                                </CardContent>
                                            </Card>
                                        )}
                                        {context.businessImpact && (
                                            <Card>
                                                <CardHeader className="pb-2">
                                                    <CardTitle className="text-base flex items-center gap-2">
                                                        <AlertCircle className="h-4 w-4 text-orange-500" />
                                                        Business Impact
                                                    </CardTitle>
                                                </CardHeader>
                                                <CardContent>
                                                    <p className="text-sm text-foreground leading-relaxed">
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
                        </div>
                    </Tabs>
                </div>
            </div>
        </TooltipProvider>
    );
};

export default EntityDetailPage;
