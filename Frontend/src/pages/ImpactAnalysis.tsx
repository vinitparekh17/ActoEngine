import React from "react";
import { useParams, Link } from "react-router-dom";
import {
  AlertTriangle,
  CheckCircle2,
  ShieldAlert,
  FileCode,
  ArrowRight,
  Activity,
  Server,
  ChevronRight,
  Info,
  GitPullRequest,
  Search,
  AlertCircle,
  Table2,
  Database,
  Calendar,
  Layers,
  LayoutDashboard,
} from "lucide-react";
import * as Icons from "lucide-react";
import { cn, utcToLocal } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import { ScrollArea } from "@/components/ui/scroll-area";
import { useApi } from "@/hooks/useApi";
import { useProject } from "@/hooks/useProject";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

import { ImpactDecisionResponse, ImpactPath } from "@/types/impact-analysis";

// --- HELPER FUNCTIONS ---

function parseStableKey(
  stableKey: string,
): { type: string; id: number } | null {
  const match = stableKey.match(/^([A-Za-z]+):([0-9]+)$/);
  if (!match) return null;
  return { type: match[1].toUpperCase(), id: parseInt(match[2], 10) };
}

function getEntityRoute(
  projectId: string | undefined,
  type: string,
  id: number,
): string {
  if (!projectId) return "#";
  const entityTypeSlug = type === "TABLE" ? "tables" : "stored-procedures";
  return `/project/${projectId}/${entityTypeSlug}/${id}/detail`;
}

function getEntityTypeLabel(type: string): string {
  switch (type) {
    case "SP":
      return "Procedure";
    case "TABLE":
      return "Table";
    default:
      return type;
  }
}

function getDependencyTypeLabel(depType: number | string): string {
  const labels: Record<number, string> = {
    0: "Unknown",
    1: "SELECT",
    2: "INSERT",
    3: "UPDATE",
    4: "DELETE",
    5: "Schema",
    6: "API",
  };
  const typeNum = typeof depType === "string" ? parseInt(depType, 10) : depType;
  return labels[typeNum] || `Type ${depType}`;
}

const EntityTypeIcon = ({
  type,
  className,
}: {
  type: string;
  className?: string;
}) => {
  const iconMap: Record<string, React.ComponentType<{ className?: string }>> = {
    SP: Database,
    TABLE: Table2,
  };
  const Icon = iconMap[type] || FileCode;
  return <Icon className={className} />;
};

// --- DOMAIN CONFIGURATION ---

const RISK_CONFIG: Record<
  number,
  {
    label: string;
    color: string;
    icon: React.ComponentType<{ className?: string }>;
    barColor: string;
    borderColor: string;
    bgGradient: string;
  }
> = {
  1: {
    label: "Low Risk",
    color:
      "text-emerald-700 bg-emerald-50 border-emerald-200 dark:text-emerald-400 dark:bg-emerald-950/50 dark:border-emerald-900",
    icon: CheckCircle2,
    barColor: "bg-emerald-500",
    borderColor: "border-l-emerald-500",
    bgGradient: "from-emerald-50/50 to-background dark:from-emerald-950/20",
  },
  2: {
    label: "Medium Risk",
    color:
      "text-amber-700 bg-amber-50 border-amber-200 dark:text-amber-400 dark:bg-amber-950/50 dark:border-amber-900",
    icon: AlertTriangle,
    barColor: "bg-amber-500",
    borderColor: "border-l-amber-500",
    bgGradient: "from-amber-50/50 to-background dark:from-amber-950/20",
  },
  3: {
    label: "Critical Risk",
    color:
      "text-red-700 bg-red-50 border-red-200 dark:text-red-400 dark:bg-red-950/50 dark:border-red-900",
    icon: ShieldAlert,
    barColor: "bg-red-600",
    borderColor: "border-l-red-600",
    bgGradient: "from-red-50/50 to-background dark:from-red-950/20",
  },
};

const IMPACT_LEVEL_CONFIG: Record<
  number,
  { label: string; color: string; bg: string }
> = {
  1: {
    label: "Low",
    color: "bg-slate-300 dark:bg-slate-700",
    bg: "text-slate-600 bg-slate-100",
  },
  2: {
    label: "High",
    color: "bg-amber-500",
    bg: "text-amber-700 bg-amber-100",
  },
  3: { label: "Severe", color: "bg-red-600", bg: "text-red-700 bg-red-100" },
};

// --- SUB-COMPONENTS ---

const RiskBadge = ({
  riskLevel,
  score,
}: {
  riskLevel: number;
  score: number;
}) => {
  const config = RISK_CONFIG[riskLevel] || RISK_CONFIG[1];
  const Icon = config.icon;

  return (
    <div
      className={cn(
        "inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-medium border shadow-sm",
        config.color,
      )}
    >
      <Icon className="w-4 h-4" />
      {config.label}
      <Separator
        orientation="vertical"
        className="h-4 mx-1 bg-current opacity-20"
      />
      <span className="opacity-80 font-mono text-xs">Score: {score}</span>
    </div>
  );
};

const ImpactMeter = ({ level }: { level: number }) => {
  return (
    <div
      className="flex gap-1 items-end h-5"
      aria-label={`Impact Level ${level}`}
    >
      {[1, 2, 3].map((step) => {
        const isActive = step <= level;
        const activeColor = IMPACT_LEVEL_CONFIG[level]?.color || "bg-slate-400";
        return (
          <div
            key={step}
            className={cn(
              "w-1.5 rounded-sm transition-all duration-300",
              isActive ? activeColor : "bg-muted/60",
              step === 1 ? "h-2" : step === 2 ? "h-3" : "h-4",
            )}
          />
        );
      })}
    </div>
  );
};

const DependencyTrace = ({
  paths,
  riskScore,
}: {
  paths: ImpactPath[];
  riskScore: number;
}) => {
  if (!paths || paths.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center space-y-3 bg-muted/20 rounded-xl border border-dashed">
        <Activity className="w-10 h-10 text-muted-foreground/30" />
        <p className="text-sm text-muted-foreground">
          No direct dependency path data available.
        </p>
      </div>
    );
  }

  const primaryPath = paths[0];
  const sourceNode = primaryPath?.nodes?.[0];
  const targetNode = primaryPath?.nodes?.[1];

  if (!sourceNode || !targetNode) return null;

  return (
    <div className="space-y-8 pr-4 py-4">
      {/* Changed space-y-10 to space-y-24 for more height */}
      <div className="relative pl-8 border-l-2 border-dashed border-border/60 space-y-24 ml-3">
        {/* Source Node */}
        <div className="relative">
          <div className="absolute -left-[43px] top-0 bg-blue-50 dark:bg-blue-900/40 text-blue-600 dark:text-blue-400 p-2 rounded-lg border border-blue-200 dark:border-blue-800 shadow-sm z-10">
            {sourceNode.iconName && (Icons as any)[sourceNode.iconName] ? (
              React.createElement((Icons as any)[sourceNode.iconName], {
                className: "w-5 h-5",
              })
            ) : (
              <Server className="w-5 h-5" />
            )}
          </div>
          <div className="space-y-2">
            <div className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
              Source (Trigger)
            </div>
            <div className="p-4 bg-background border rounded-lg shadow-sm">
              <div className="font-semibold text-sm">
                {sourceNode.name || "Source Entity"}
              </div>
              <div className="text-xs text-muted-foreground mt-1">
                Initiates the change
              </div>
            </div>
          </div>
        </div>

        {/* Connection/Edge - Centered Vertically */}
        <div className="absolute left-[-12px] top-1/2 -translate-y-1/2 bg-background border rounded-full p-1 text-muted-foreground z-10">
          <ArrowRight className="w-3 h-3" />
        </div>

        {/* Connection Label - Centered Vertically */}
        <div className="absolute left-4 top-1/2 -translate-y-1/2">
          {primaryPath?.edges?.[0] != null && (
            <Badge
              variant="outline"
              className="text-[10px] font-medium bg-muted/50"
            >
              {getDependencyTypeLabel(primaryPath.edges[0])}
            </Badge>
          )}
        </div>

        {/* Target Node */}
        <div className="relative">
          <div className="absolute -left-[43px] top-0 bg-amber-50 dark:bg-amber-900/40 text-amber-600 dark:text-amber-400 p-2 rounded-lg border border-amber-200 dark:border-amber-800 shadow-sm z-10">
            {targetNode.iconName && (Icons as any)[targetNode.iconName] ? (
              React.createElement((Icons as any)[targetNode.iconName], {
                className: "w-5 h-5",
              })
            ) : (
              <FileCode className="w-5 h-5" />
            )}
          </div>
          <div className="space-y-2">
            <div className="text-xs font-bold text-muted-foreground uppercase tracking-wider">
              Target (Impacted)
            </div>
            <div className="p-4 bg-amber-50/30 dark:bg-amber-950/10 border border-amber-100 dark:border-amber-900/50 rounded-lg shadow-sm">
              <div className="font-semibold text-sm">
                {targetNode.name || "Target Entity"}
              </div>
              <div className="flex items-center gap-2 mt-3">
                <Badge
                  variant="secondary"
                  className="text-[10px] h-5 px-1.5 bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-800"
                >
                  Risk Score: {riskScore}
                </Badge>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

// --- MAIN PAGE COMPONENT ---

export default function ImpactReportPage() {
  const { projectId, entityType, entityId } = useParams<{
    projectId: string;
    entityType: string;
    entityId: string;
  }>();
  const { hasProject } = useProject();

  const {
    data: response,
    isLoading,
    error,
    refetch,
  } = useApi<ImpactDecisionResponse>(
    `/projects/${projectId}/impact/${entityType}/${entityId}?changeType=MODIFY`,
    { enabled: !!projectId && !!entityType && !!entityId && hasProject },
  );

  if (isLoading) {
    return (
      <div className="flex flex-col h-[calc(100vh-114px)] bg-background animate-pulse">
        <div className="h-14 border-b bg-muted/20" />
        <div className="h-48 bg-muted/10 m-6 rounded-xl" />
        <div className="flex-1 p-6 space-y-4">
          <div className="h-10 w-64 bg-muted rounded" />
          <div className="h-96 w-full bg-muted/20 rounded-xl" />
        </div>
      </div>
    );
  }

  if (error || !response) {
    return (
      <div className="flex flex-col items-center justify-center h-[calc(100vh-114px)] p-6">
        <div className="w-16 h-16 rounded-full bg-destructive/10 flex items-center justify-center mb-4">
          <AlertCircle className="h-8 w-8 text-destructive" />
        </div>
        <h2 className="text-xl font-semibold mb-2">Impact Analysis Failed</h2>
        <p className="text-muted-foreground mb-6 text-center max-w-md">
          {error?.message || "Unable to retrieve impact data."}
        </p>
        <Button onClick={() => refetch()} variant="outline">
          Try Again
        </Button>
      </div>
    );
  }

  const { verdict, summary, entities } = response;
  const verdictStyle = RISK_CONFIG[verdict.risk] || RISK_CONFIG[1];
  const isWhatIf = summary.analysisType === "What-If analysis";

  return (
    <div className="flex flex-col h-auto bg-background overflow-hidden">
      {/* 1. Sticky Header */}
      <div className="px-6 py-3 border-b flex items-center justify-between bg-background/95 backdrop-blur z-30 sticky top-0 supports-[backdrop-filter]:bg-background/60">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            asChild
            className="-ml-2 text-muted-foreground"
          >
            <Link to={`/project/${projectId}/entities`}>
              <LayoutDashboard className="w-4 h-4 mr-2" />
              Explorer
            </Link>
          </Button>
          <Separator orientation="vertical" className="h-4" />
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold">
              {summary.rootEntity?.name}
            </span>
            <Badge variant="outline" className="text-[10px] h-5 px-1.5">
              {entityType?.toUpperCase()}
            </Badge>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <div className="text-xs text-muted-foreground flex items-center gap-1.5 hidden md:flex">
            <Calendar className="w-3.5 h-3.5" />
            {utcToLocal(verdict.generatedAt, "PPP")}
          </div>
          <Separator orientation="vertical" className="h-4 hidden md:block" />
          <div
            className={cn(
              "flex items-center gap-1.5 text-xs font-medium px-2 py-1 rounded-md bg-muted/50",
              isWhatIf ? "text-blue-600 dark:text-blue-400" : "text-primary",
            )}
          >
            {isWhatIf ? (
              <Search className="w-3.5 h-3.5" />
            ) : (
              <GitPullRequest className="w-3.5 h-3.5" />
            )}
            {summary.analysisType}
          </div>
        </div>
      </div>

      {/* 2. Scrollable Content */}
      <ScrollArea className="flex-1">
        <div className="flex flex-col min-h-full pb-20">
          {/* Hero Section */}
          <header
            className={cn(
              "px-8 py-10 border-b bg-gradient-to-b",
              verdictStyle.bgGradient,
            )}
          >
            <div className="max-w-6xl mx-auto w-full">
              {isWhatIf && (
                <Alert className="mb-6 bg-blue-50/50 border-blue-200 text-blue-900 dark:bg-blue-950/30 dark:border-blue-900 dark:text-blue-100">
                  <Info className="h-4 w-4" />
                  <AlertTitle>Simulation Mode</AlertTitle>
                  <AlertDescription>
                    This is a "What-If" analysis. No actual system changes have
                    been committed.
                  </AlertDescription>
                </Alert>
              )}

              <div className="flex flex-col md:flex-row md:items-start justify-between gap-6">
                <div className="space-y-4">
                  <div className="space-y-2">
                    <h1 className="text-3xl font-bold tracking-tight text-foreground">
                      {verdict.summary}
                    </h1>
                    <p className="text-muted-foreground text-lg max-w-2xl leading-relaxed">
                      The proposed change affects{" "}
                      <span className="font-medium text-foreground">
                        {entities.length} downstream entities
                      </span>
                      . Review the reasoning below before proceeding.
                    </p>
                  </div>
                  <div className="flex items-center gap-3 pt-2">
                    <div
                      className={cn(
                        "flex items-center text-xs font-medium px-2.5 py-1 rounded border",
                        verdict.requiresApproval
                          ? "bg-red-50 text-red-700 border-red-200"
                          : "bg-green-50 text-green-700 border-green-200",
                      )}
                    >
                      {verdict.requiresApproval ? (
                        <ShieldAlert className="w-3.5 h-3.5 mr-1.5" />
                      ) : (
                        <CheckCircle2 className="w-3.5 h-3.5 mr-1.5" />
                      )}
                      {verdict.requiresApproval
                        ? "Approval Required"
                        : "Auto-Approval Eligible"}
                    </div>
                    <span className="text-xs text-muted-foreground">
                      ID: #AE-{projectId}-{entityId}
                    </span>
                  </div>
                </div>

                <div className="flex-shrink-0">
                  <div className="bg-background/80 backdrop-blur p-1 rounded-full border shadow-sm">
                    <RiskBadge
                      riskLevel={verdict.risk}
                      score={
                        verdict.reasons.reduce(
                          (acc, r) => acc + r.priority,
                          0,
                        ) * 8
                      }
                    />
                  </div>
                </div>
              </div>
            </div>
          </header>

          <div className="max-w-6xl mx-auto w-full px-8 py-8 space-y-10">
            {/* Analysis Section */}
            <section className="space-y-4">
              <div className="flex items-center gap-2 mb-2">
                <Layers className="w-5 h-5 text-primary" />
                <h3 className="text-lg font-semibold tracking-tight">
                  Analysis & Reasoning
                </h3>
              </div>

              <div className="grid gap-3">
                <Accordion
                  type="single"
                  collapsible
                  className="w-full space-y-3"
                >
                  {verdict.reasons.map((reason, idx) => (
                    <AccordionItem
                      key={idx}
                      value={`item-${idx}`}
                      className="border rounded-xl bg-card px-2 shadow-sm"
                    >
                      <AccordionTrigger className="hover:no-underline py-4 px-4">
                        <div className="flex flex-col md:flex-row md:items-center gap-4 w-full text-left pr-4">
                          <span className="font-medium flex-1 text-sm md:text-base">
                            {reason.statement}
                          </span>
                          <div className="flex items-center gap-2 text-xs bg-muted/50 text-muted-foreground px-3 py-1.5 rounded-full whitespace-nowrap border">
                            <span className="font-semibold text-foreground">
                              Implication:
                            </span>
                            <span className="truncate max-w-[150px] md:max-w-[250px]">
                              {reason.implication}
                            </span>
                          </div>
                        </div>
                      </AccordionTrigger>
                      <AccordionContent className="px-4 pb-4">
                        <div className="pt-2 pl-4 border-l-2 border-muted ml-1 space-y-3">
                          <h4 className="text-[10px] uppercase tracking-wider font-bold text-muted-foreground">
                            Supporting Evidence
                          </h4>
                          {reason.evidence && reason.evidence.length > 0 ? (
                            <div className="flex flex-wrap gap-2">
                              {reason.evidence.map((ev) => {
                                const parsed = parseStableKey(ev);
                                if (!parsed)
                                  return (
                                    <Badge
                                      key={ev}
                                      variant="outline"
                                      className="font-mono text-xs"
                                    >
                                      {ev}
                                    </Badge>
                                  );

                                const matchingEntity = entities.find(
                                  (e) => e.entity.stableKey === ev,
                                );
                                const entityName = matchingEntity?.entity.name;
                                const displayType = getEntityTypeLabel(
                                  parsed.type,
                                );

                                return (
                                  <Link
                                    key={ev}
                                    to={getEntityRoute(
                                      projectId,
                                      parsed.type,
                                      parsed.id,
                                    )}
                                  >
                                    <Badge
                                      variant="secondary"
                                      className="gap-1.5 hover:bg-primary/10 transition-colors py-1 pl-2 pr-3 cursor-pointer border-transparent hover:border-primary/20 border"
                                    >
                                      <EntityTypeIcon
                                        type={parsed.type}
                                        className="w-3 h-3 text-muted-foreground"
                                      />
                                      {entityName ||
                                        `${displayType} #${parsed.id}`}
                                    </Badge>
                                  </Link>
                                );
                              })}
                            </div>
                          ) : (
                            <p className="text-sm text-muted-foreground italic">
                              Based on system heuristics and rule engine logic.
                            </p>
                          )}
                        </div>
                      </AccordionContent>
                    </AccordionItem>
                  ))}
                </Accordion>
              </div>
            </section>

            {/* Entities Table Section */}
            <section className="space-y-4">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Activity className="w-5 h-5 text-primary" />
                  <h3 className="text-lg font-semibold tracking-tight">
                    Affected Downstream Entities
                  </h3>
                  <Badge variant="secondary" className="ml-2">
                    {entities.length}
                  </Badge>
                </div>
              </div>

              <Card className="overflow-hidden border-border/60 shadow-sm">
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader className="bg-muted/30">
                      <TableRow className="hover:bg-transparent border-b-border/60">
                        <TableHead className="w-[80px] text-center text-xs uppercase h-10">
                          Type
                        </TableHead>
                        <TableHead className="text-xs uppercase h-10 min-w-[200px]">
                          Entity Name
                        </TableHead>
                        <TableHead className="text-xs uppercase h-10">
                          Operation
                        </TableHead>
                        <TableHead className="text-xs uppercase h-10">
                          Impact Severity
                        </TableHead>
                        <TableHead className="text-right text-xs uppercase h-10 pr-6">
                          Trace Analysis
                        </TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {entities.map((item, idx) => (
                        <TableRow
                          key={idx}
                          className="group h-12 border-b-border/40 hover:bg-muted/20"
                        >
                          <TableCell className="text-center py-3">
                            <div className="inline-flex items-center justify-center w-8 h-8 rounded-full bg-background border shadow-sm">
                              <EntityTypeIcon
                                type={
                                  parseStableKey(item.entity.stableKey)?.type ||
                                  "FILE"
                                }
                                className="w-4 h-4 text-muted-foreground"
                              />
                            </div>
                          </TableCell>
                          <TableCell className="font-medium py-3">
                            {(() => {
                              const parsed = parseStableKey(
                                item.entity.stableKey,
                              );
                              if (!parsed)
                                return (
                                  <span className="text-sm">
                                    {item.entity.name || "Unknown"}
                                  </span>
                                );
                              return (
                                <Link
                                  to={getEntityRoute(
                                    projectId,
                                    parsed.type,
                                    parsed.id,
                                  )}
                                  className="text-sm hover:text-primary transition-colors hover:underline underline-offset-4 decoration-primary/30"
                                >
                                  {item.entity.name || `Entity #${parsed.id}`}
                                </Link>
                              );
                            })()}
                          </TableCell>
                          <TableCell className="py-3">
                            <Badge
                              variant="outline"
                              className="font-mono text-[10px] uppercase bg-slate-50 text-slate-600 border-slate-200"
                            >
                              {item.dominantOperation}
                            </Badge>
                          </TableCell>
                          <TableCell className="py-3">
                            <div className="flex items-center gap-3">
                              <ImpactMeter level={item.worstCaseImpactLevel} />
                              <span
                                className={cn(
                                  "text-xs font-medium",
                                  (
                                    IMPACT_LEVEL_CONFIG[
                                      item.worstCaseImpactLevel
                                    ]?.bg || "text-slate-600 bg-slate-100"
                                  ).split(" ")[0],
                                )}
                              >
                                {IMPACT_LEVEL_CONFIG[item.worstCaseImpactLevel]
                                  ?.label || "Unknown"}
                              </span>
                            </div>
                          </TableCell>
                          <TableCell className="text-right py-3 pr-6">
                            <Sheet>
                              <SheetTrigger asChild>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  className="h-8 text-xs hover:bg-primary/10 hover:text-primary group-hover:opacity-100 opacity-70 transition-all"
                                >
                                  Inspect Path{" "}
                                  <ChevronRight className="w-3 h-3 ml-1" />
                                </Button>
                              </SheetTrigger>
                              <SheetContent className="w-full sm:max-w-lg">
                                <SheetHeader className="mb-8 border-b pb-4">
                                  <SheetTitle className="text-lg font-semibold flex items-center gap-2">
                                    <GitPullRequest className="w-5 h-5 text-primary" />
                                    Dependency Trace
                                  </SheetTitle>
                                  <SheetDescription>
                                    Visualizing the propagation path from the
                                    root change to{" "}
                                    <strong>{item.entity.name}</strong>.
                                  </SheetDescription>
                                </SheetHeader>
                                <DependencyTrace
                                  paths={item.paths}
                                  riskScore={item.riskScore}
                                />
                              </SheetContent>
                            </Sheet>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </Card>
            </section>
          </div>
        </div>
      </ScrollArea>
    </div>
  );
}
