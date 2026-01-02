import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
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
  Loader2,
  Code2
} from 'lucide-react';
import * as Icons from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Separator } from '@/components/ui/separator';
import { ScrollArea } from '@/components/ui/scroll-area';
import { useApi } from '@/hooks/useApi';
import { useProject } from '@/hooks/useProject';
import { GridSkeleton, PageHeaderSkeleton } from '@/components/ui/skeletons';
import { Alert, AlertDescription } from '@/components/ui/alert';

import {
  ImpactDecisionResponse,
  ImpactPath,
  EntityRef
} from '@/types/impact-analysis';
import { api } from '@/lib/api';

// --- HELPER FUNCTIONS ---

/**
 * Parses a stable key (e.g., "Sp:27") into type and ID.
 */
function parseStableKey(stableKey: string): { type: string; id: number } | null {
  const match = stableKey.match(/^([A-Za-z]+):([0-9]+)$/);
  if (!match) return null;
  return { type: match[1].toUpperCase(), id: parseInt(match[2], 10) };
}

/**
 * Maps entity type abbreviations to API endpoints.
 */
function getDefinitionEndpoint(projectId: string, type: string, id: number): string | null {
  switch (type) {
    case 'SP':
      return `/DatabaseBrowser/projects/${projectId}/stored-procedures/${id}`;
    case 'TABLE':
      return `/DatabaseBrowser/projects/${projectId}/tables/${id}`;
    default:
      return null;
  }
}

// --- DOMAIN CONFIGURATION ---

const RISK_CONFIG: Record<number, {
  label: string;
  color: string;
  icon: any;
  barColor: string;
  borderColor: string;
}> = {
  1: {
    label: "Low Risk",
    color: "text-green-700 bg-green-50 border-green-200 hover:bg-green-100",
    icon: CheckCircle2,
    barColor: "bg-green-500",
    borderColor: "border-l-green-500"
  },
  2: {
    label: "Medium Risk",
    color: "text-amber-700 bg-amber-50 border-amber-200 hover:bg-amber-100",
    icon: AlertTriangle,
    barColor: "bg-amber-500",
    borderColor: "border-l-amber-500"
  },
  3: {
    label: "Critical Risk",
    color: "text-red-700 bg-red-50 border-red-200 hover:bg-red-100",
    icon: ShieldAlert,
    barColor: "bg-red-600",
    borderColor: "border-l-red-600"
  }
};

const IMPACT_LEVEL_CONFIG: Record<number, { label: string, color: string }> = {
  1: { label: "Low", color: "bg-slate-300" },
  2: { label: "High", color: "bg-amber-500" },
  3: { label: "Severe", color: "bg-red-600" }
};

// --- SUB-COMPONENTS ---

const RiskBadge = ({ riskLevel, score }: { riskLevel: number, score: number }) => {
  const config = RISK_CONFIG[riskLevel] || RISK_CONFIG[1];
  const Icon = config.icon;

  return (
    <Badge variant="outline" className={cn("gap-1.5 pl-2 pr-3 py-1 font-medium border", config.color)} title={`Internal Risk Score: ${score}`}>
      <Icon className="w-3.5 h-3.5" />
      {config.label}
      <span className="opacity-50 font-mono text-[10px] ml-1.5 pt-0.5 border-l border-current/30 pl-1.5">
        {score}
      </span>
    </Badge>
  );
};

const ImpactMeter = ({ level }: { level: number }) => {
  return (
    <div className="flex gap-1 items-end" aria-label={`Impact Level ${level}`}>
      {[1, 2, 3].map((step) => {
        const isActive = step <= level;
        const activeColor = IMPACT_LEVEL_CONFIG[level]?.color || "bg-slate-400";
        return (
          <div
            key={step}
            className={cn(
              "w-2 rounded-sm transition-all",
              isActive ? activeColor : "bg-muted",
              step === 1 ? "h-3" : step === 2 ? "h-4" : "h-5"
            )}
          />
        );
      })}
    </div>
  );
};

const EntityName = ({ entity }: { entity: EntityRef }) => {
  return (
    <div className="flex flex-col">
      {entity.name ? (
        <span className="font-medium text-sm text-foreground">{entity.name}</span>
      ) : (
        <span className="text-sm text-muted-foreground italic">Unnamed Entity</span>
      )}
      <span className="font-mono text-[10px] text-muted-foreground bg-muted/50 w-fit px-1 rounded mt-0.5">
        {entity.stableKey}
      </span>
    </div>
  );
};

const DependencyTrace = ({ paths, riskScore }: { paths: ImpactPath[], riskScore: number }) => {
  if (!paths || paths.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-10 text-center space-y-3 bg-muted/20 rounded-lg border border-dashed">
        <Activity className="w-8 h-8 text-muted-foreground/50" />
        <p className="text-sm text-muted-foreground">No direct dependency path data available for this entity.</p>
      </div>
    );
  }

  const primaryPath = paths[0];

  const sourceNode = primaryPath?.nodes?.[0];
  const targetNode = primaryPath?.nodes?.[1];

  if (!sourceNode || !targetNode) {
    return (
      <div className="flex flex-col items-center justify-center py-10 text-center space-y-3 bg-muted/20 rounded-lg border border-dashed">
        <Activity className="w-8 h-8 text-muted-foreground/50" />
        <p className="text-sm text-muted-foreground">Invalid dependency path data.</p>
      </div>
    );
  }

  return (
    <div className="space-y-8 pr-4">
      <div className="relative pl-6 border-l-2 border-dashed border-muted space-y-8">

        {/* Source Node */}
        <div className="relative">
          <div className="absolute -left-[31px] bg-blue-100 dark:bg-blue-900 text-blue-600 p-1.5 rounded-md border-4 border-background">
            {sourceNode.iconName && (Icons as any)[sourceNode.iconName] ? (
              React.createElement((Icons as any)[sourceNode.iconName], { className: "w-4 h-4" })
            ) : (
              <Server className="w-4 h-4" />
            )}
          </div>
          <div>
            <div className="ml-2 text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1">Source (Trigger)</div>
            <div className="p-3 bg-muted/30 border rounded-md">
              <div className="font-medium text-sm">{sourceNode.name || sourceNode.stableKey}</div>
              {sourceNode.name && <div className="text-xs text-muted-foreground">{sourceNode.stableKey}</div>}
            </div>
          </div>
        </div>

        {/* Edge */}
        <div className="relative py-2">
          <div className="absolute -left-[31px] top-1/2 -translate-y-1/2 bg-background border rounded-full p-1 text-muted-foreground">
            <ArrowRight className="w-3 h-3" />
          </div>
          <div className="ml-4">
            {primaryPath?.edges?.[0] && (
              <Badge variant="outline" className="font-mono text-[10px]">{primaryPath.edges[0]}</Badge>
            )}
          </div>
        </div>

        {/* Target Node */}
        <div className="relative">
          <div className="absolute -left-[31px] bg-amber-100 dark:bg-amber-900 text-amber-600 p-1.5 rounded-md border-4 border-background">
            {targetNode.iconName && (Icons as any)[targetNode.iconName] ? (
              React.createElement((Icons as any)[targetNode.iconName], { className: "w-4 h-4" })
            ) : (
              <FileCode className="w-4 h-4" />
            )}
          </div>
          <div>
            <div className="ml-2 text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1">Target (Impacted)</div>
            <div className="p-3 bg-amber-50/50 dark:bg-amber-950/10 border border-amber-200/50 rounded-md">
              <div className="font-medium text-sm">
                {targetNode.name || "Unnamed Entity"}
              </div>
              <div className="text-xs text-muted-foreground mb-2">
                {targetNode.stableKey}
              </div>
              <div className="flex gap-2">
                <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-200 border-amber-200 text-[10px] px-1 h-5">
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

// --- SOURCE DEFINITIONS PANEL ---

interface SourceDefinitionsPanelProps {
  projectId: string;
  evidence: string[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

interface DefinitionState {
  definition: string | null;
  name: string;
  loading: boolean;
  error: string | null;
  fetched: boolean;
}

const SourceDefinitionsPanel = ({ projectId, evidence, open, onOpenChange }: SourceDefinitionsPanelProps) => {
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [definitions, setDefinitions] = useState<Record<string, DefinitionState>>({});

  // Fetch definition for a specific key (lazy-load on expand)
  const fetchDefinition = async (key: string) => {
    // Skip if already fetched or currently loading
    if (definitions[key]?.fetched || definitions[key]?.loading) return;

    // Set loading state
    setDefinitions(prev => ({
      ...prev,
      [key]: { definition: null, name: key, loading: true, error: null, fetched: false }
    }));

    const parsed = parseStableKey(key);
    if (!parsed) {
      setDefinitions(prev => ({
        ...prev,
        [key]: { definition: null, name: key, loading: false, error: 'Invalid key format', fetched: true }
      }));
      return;
    }

    const endpoint = getDefinitionEndpoint(projectId, parsed.type, parsed.id);
    if (!endpoint) {
      setDefinitions(prev => ({
        ...prev,
        [key]: { definition: null, name: key, loading: false, error: `Unsupported entity type: ${parsed.type}`, fetched: true }
      }));
      return;
    }

    try {
      const data = await api.get<{ procedureName?: string; tableName?: string; definition?: string }>(endpoint);
      const name = data?.procedureName || data?.tableName || key;
      const definition = data?.definition || null;
      setDefinitions(prev => ({
        ...prev,
        [key]: { definition, name, loading: false, error: null, fetched: true }
      }));
    } catch (err) {
      setDefinitions(prev => ({
        ...prev,
        [key]: { definition: null, name: key, loading: false, error: 'Failed to load definition', fetched: true }
      }));
    }
  };

  // Handle accordion expand - fetch on first expand
  const handleExpand = (key: string) => {
    if (expandedKey === key) {
      setExpandedKey(null);
    } else {
      setExpandedKey(key);
      fetchDefinition(key);
    }
  };

  // Reset state when panel closes
  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      setExpandedKey(null);
      setDefinitions({});
    }
    onOpenChange(newOpen);
  };

  return (
    <Sheet open={open} onOpenChange={handleOpenChange}>
      <SheetContent className="w-full sm:max-w-2xl overflow-y-auto">
        <SheetHeader className="mb-6 text-left">
          <SheetTitle className="text-lg flex items-center gap-2">
            <Code2 className="w-5 h-5" />
            Source Definitions
          </SheetTitle>
          <SheetDescription>
            Click on an entity to view its SQL definition.
          </SheetDescription>
        </SheetHeader>

        <div className="space-y-2">
          {evidence.map((key) => {
            const isExpanded = expandedKey === key;
            const def = definitions[key];
            const displayName = def?.name || key;

            return (
              <div key={key} className="border rounded-lg overflow-hidden">
                <button
                  onClick={() => handleExpand(key)}
                  className="w-full flex items-center justify-between p-3 bg-muted/30 hover:bg-muted/50 transition-colors text-left"
                >
                  <div className="flex items-center gap-2">
                    <Badge variant="outline" className="font-mono text-xs">
                      {key}
                    </Badge>
                    {def?.fetched && def.name !== key && (
                      <span className="text-sm font-medium">{displayName}</span>
                    )}
                  </div>
                  <ChevronRight className={cn("w-4 h-4 transition-transform", isExpanded && "rotate-90")} />
                </button>

                {isExpanded && (
                  <div className="p-3 border-t">
                    {def?.loading ? (
                      <div className="flex items-center gap-2 text-sm text-muted-foreground py-4">
                        <Loader2 className="w-4 h-4 animate-spin" />
                        Loading definition...
                      </div>
                    ) : def?.error ? (
                      <div className="text-sm text-destructive p-3 bg-destructive/10 rounded-lg border border-destructive/20">
                        {def.error}
                      </div>
                    ) : def?.definition ? (
                      <pre className="bg-muted p-4 rounded-lg overflow-x-auto text-xs font-mono whitespace-pre-wrap border max-h-[400px] overflow-y-auto">
                        <code>{def.definition}</code>
                      </pre>
                    ) : (
                      <div className="text-sm text-muted-foreground italic py-4">
                        No definition available for this entity.
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </SheetContent>
    </Sheet>
  );
};


// --- MAIN PAGE COMPONENT ---

export default function ImpactReportPage() {
  const { projectId, entityType, entityId } = useParams<{ projectId: string, entityType: string, entityId: string }>();
  const { selectedProject, hasProject } = useProject();

  // State for source definitions panel
  const [sourceDefsOpen, setSourceDefsOpen] = useState(false);
  const [selectedEvidence, setSelectedEvidence] = useState<string[]>([]);

  const {
    data: response,
    isLoading,
    error,
    refetch
  } = useApi<ImpactDecisionResponse>(
    `/projects/${projectId}/impact/${entityType}/${entityId}?changeType=MODIFY`,
    {
      enabled: !!projectId && !!entityType && !!entityId && hasProject,
    }
  );

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <PageHeaderSkeleton />
        <GridSkeleton count={1} className="grid grid-cols-1" />
        <div className="space-y-4">
          <div className="h-[400px] w-full bg-muted/10 animate-pulse rounded-lg border border-neutral-200 dark:border-neutral-800" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load impact analysis: {error.message}
          </AlertDescription>
        </Alert>
        <Button onClick={() => refetch()} variant="outline" className="mt-4">
          Try Again
        </Button>
      </div>
    );
  }

  if (!response) return null;

  const { verdict, summary, entities } = response;
  const verdictStyle = RISK_CONFIG[verdict.risk] || RISK_CONFIG[1];

  return (
    <ScrollArea className="h-[calc(100vh-110px)]">
      <div className="min-h-full bg-background text-foreground pb-20 font-sans">

        {/* 🟢 ZONE 1: CONTEXT BAR */}
        <div className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
          <div className="container mx-auto px-4 h-16 flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className="hidden md:flex flex-col">
                <div className="flex items-center gap-2 text-[10px] uppercase tracking-wider text-muted-foreground font-semibold">
                  Impact Analysis
                  <span className="opacity-50">•</span>
                  <span className={cn(
                    "flex items-center gap-1",
                    summary.analysisType === "What-If analysis" ? "text-blue-600" : "text-primary/80"
                  )}>
                    {summary.analysisType === "What-If analysis" ? <Search className="w-3 h-3" /> : <GitPullRequest className="w-3 h-3" />}
                    {summary.analysisType}
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary" className="font-mono text-xs rounded-sm px-1.5">{entityType?.toUpperCase()}</Badge>
                  <span className="text-sm font-semibold">
                    {summary.rootEntity?.name || `ID: ${entityId}`}
                  </span>
                </div>
              </div>

              <Separator orientation="vertical" className="h-8 hidden md:block" />

              <Badge
                variant="outline"
                className={cn(
                  "border-orange-200 bg-orange-50 text-orange-700 flex items-center gap-1.5",
                  summary.environment !== "Production" && "border-slate-200 bg-slate-50 text-slate-700"
                )}
              >
                <Server className="w-3 h-3" />
                {summary.environment}
              </Badge>

              <Badge variant="outline" className="hidden lg:flex">
                Project: {selectedProject?.projectName}
              </Badge>
            </div>
          </div>
        </div>

        <div className="container mx-auto px-4 max-w-5xl py-8 space-y-8">

          {/* Banner for What-If Analysis */}
          {summary.analysisType === "What-If analysis" && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 flex items-center gap-3 text-sm text-blue-800 animate-in fade-in slide-in-from-top-2">
              <Search className="w-4 h-4" />
              <p><strong>What-if Mode:</strong> This analysis is explorative and does not correspond to an active system change.</p>
            </div>
          )}

          {/* 🟢 ZONE 2: VERDICT CARD */}
          <Card className={cn("border-l-4 shadow-sm overflow-hidden relative", verdictStyle.borderColor)}>
            <div className={cn("w-1 h-full absolute left-0 top-0", verdictStyle.barColor)}></div>
            <CardHeader className="pb-6">
              <div className="flex flex-col md:flex-row md:justify-between md:items-start gap-4">
                <div className="space-y-1.5 text-left">
                  <CardTitle className="text-xl md:text-2xl flex items-center gap-2">
                    {verdict.summary}
                  </CardTitle>
                  <CardDescription className="flex items-center gap-2 text-xs">
                    Generated {new Date(verdict.generatedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
                    <span className="hidden md:inline">•</span>
                    <span className="hidden md:inline">Ref: #AE-{projectId}-{entityId}</span>
                  </CardDescription>
                </div>

                <div className="flex flex-col items-start md:items-end gap-2">
                  <RiskBadge riskLevel={verdict.risk} score={verdict.reasons.reduce((acc, r) => acc + r.priority, 0) * 8} />

                  {/* Approval status is factual rule metadata, not a recommendation */}
                  <div className="flex items-center text-xs text-muted-foreground bg-muted/50 px-2 py-1 rounded">
                    {verdict.requiresApproval ? (
                      <ShieldAlert className="w-3 h-3 mr-1.5 text-red-600" />
                    ) : (
                      <CheckCircle2 className="w-3 h-3 mr-1.5 text-green-600" />
                    )}
                    {verdict.requiresApproval ? "Approval Required" : "Standard Approval Path"}
                  </div>
                </div>
              </div>
            </CardHeader>
          </Card>

          {/* 🟢 ZONE 3: THE REASONING */}
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <Info className="w-5 h-5 text-muted-foreground" />
              <h3 className="text-lg font-semibold tracking-tight text-left">Analysis & Reasoning</h3>
            </div>

            <Accordion type="single" collapsible className="w-full space-y-3">
              {verdict.reasons.map((reason, idx) => (
                <AccordionItem key={idx} value={`item-${idx}`} className="border rounded-lg px-4 bg-card shadow-sm">
                  <AccordionTrigger className="hover:no-underline py-4">
                    <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 w-full pr-4">
                      <span className="font-medium text-foreground text-left flex-1">{reason.statement}</span>

                      <div className="flex items-center gap-2 text-sm bg-amber-50 dark:bg-amber-950/30 text-amber-800 dark:text-amber-200 px-3 py-1 rounded-full whitespace-nowrap">
                        <ArrowRight className="w-3 h-3" />
                        <span className="font-medium">Implication:</span>
                        <span className="truncate max-w-[200px] md:max-w-none">{reason.implication}</span>
                      </div>
                    </div>
                  </AccordionTrigger>
                  <AccordionContent className="pt-0 pb-4">
                    <div className="pl-0 md:pl-4 border-l-2 border-transparent md:border-muted ml-0 md:ml-2 mt-2">
                      {reason.evidence && reason.evidence.length > 0 ? (
                        <div className="space-y-3">
                          <div className="text-[10px] uppercase tracking-wider text-muted-foreground font-bold text-left">Evidence ({reason.evidence.length} Objects)</div>
                          <div className="flex flex-wrap gap-2">
                            {reason.evidence.map(ev => (
                              <Badge key={ev} variant="outline" className="font-mono text-xs bg-background text-foreground/80 border-border">
                                {ev}
                              </Badge>
                            ))}
                          </div>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-7 text-xs gap-1.5 text-muted-foreground hover:text-foreground -ml-2"
                            onClick={() => {
                              setSelectedEvidence(reason.evidence);
                              setSourceDefsOpen(true);
                            }}
                          >
                            <FileCode className="w-3 h-3" /> View Source Definitions
                          </Button>
                        </div>
                      ) : (
                        <p className="text-sm text-muted-foreground italic text-left">System heuristic (No specific object dependencies).</p>
                      )}
                    </div>
                  </AccordionContent>
                </AccordionItem>
              ))}
            </Accordion>
          </div>

          {/* 🟢 ZONE 4: IMPACT RADIUS */}
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold tracking-tight text-left text-foreground/90">Affected Entities ({entities.length})</h3>
            </div>

            <Card className="overflow-hidden border-border/60">
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow className="bg-muted/50 hover:bg-muted/50">
                      <TableHead className="w-[60px] text-center">Type</TableHead>
                      <TableHead className="min-w-[200px]">Entity</TableHead>
                      <TableHead>Operation</TableHead>
                      <TableHead>Severity</TableHead>
                      <TableHead className="text-right">Trace</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {entities.map((item, idx) => (
                      <TableRow key={idx} className="group">
                        <TableCell className="text-center">
                          <div className="inline-flex p-2 bg-muted rounded-md group-hover:bg-white dark:group-hover:bg-muted/80 transition-colors border shadow-sm">
                            <FileCode className="w-4 h-4 text-muted-foreground" />
                          </div>
                        </TableCell>
                        <TableCell className="text-left">
                          <EntityName entity={item.entity} />
                        </TableCell>
                        <TableCell className="text-left">
                          <Badge variant="secondary" className="font-normal text-xs bg-slate-100 text-slate-700 hover:bg-slate-200">
                            {item.dominantOperation}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-left">
                          <div className="flex items-center gap-3">
                            <ImpactMeter level={item.worstCaseImpactLevel} />
                            <span className="text-xs font-medium text-muted-foreground">
                              Level {item.worstCaseImpactLevel}
                            </span>
                          </div>
                        </TableCell>
                        <TableCell className="text-right">

                          {/* 🟡 ZONE 5: TRACE DRAWER */}
                          <Sheet>
                            <SheetTrigger asChild>
                              <Button variant="ghost" size="sm" className="gap-1 text-muted-foreground hover:text-primary">
                                View <ChevronRight className="w-4 h-4" />
                              </Button>
                            </SheetTrigger>
                            <SheetContent className="w-full sm:max-w-md">
                              <SheetHeader className="mb-6 text-left">
                                <SheetTitle className="text-lg">Dependency Trace</SheetTitle>
                                <SheetDescription>
                                  Causal path from source to target as detected by the analysis engine.
                                </SheetDescription>
                              </SheetHeader>

                              <DependencyTrace paths={item.paths} riskScore={item.riskScore} />

                            </SheetContent>
                          </Sheet>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </Card>
          </div>

        </div>
      </div>

      {/* Source Definitions Panel */}
      {projectId && (
        <SourceDefinitionsPanel
          projectId={projectId}
          evidence={selectedEvidence}
          open={sourceDefsOpen}
          onOpenChange={setSourceDefsOpen}
        />
      )}
    </ScrollArea>
  );
}