// components/context/InlineContextBadge.tsx
import React from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { Badge } from "@/components/ui/badge";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from "@/components/ui/hover-card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  FileText,
  AlertTriangle,
  CheckCircle2,
  Users,
  Shield,
  AlertCircle,
  ExternalLink,
  Edit2,
} from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { RequireProject } from "@/components/containers";
import { QuickContextDialog } from "./ContextDialog";
import { CriticalityLevel } from "@/types/context";

// Shared constants for sensitivity levels
const HIGH_SENSITIVITY_LEVELS = ['PII', 'FINANCIAL', 'SENSITIVE'] as const;
const CRITICAL_SENSITIVITY_LEVELS = ['PII', 'FINANCIAL'] as const;

// Type guard for CriticalityLevel validation
function isCriticalityLevel(value: any): value is CriticalityLevel {
  return typeof value === 'number' && value >= 1 && value <= 5;
}

// Types
interface ContextSummary {
  context: {
    purpose?: string;
    businessImpact?: string;
    businessDomain?: string;
    dataOwner?: string;
    criticalityLevel?: number;
    sensitivity?: string;
    reviewedBy?: string;
  };
  completenessScore: number;
  isStale: boolean;
  experts?: Array<{
    userId: string;
    user?: {
      fullName?: string;
      username?: string;
    };
    expertiseLevel: string;
  }>;
  lastReviewed?: string;
}

interface InlineContextBadgeProps {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  variant?: "minimal" | "detailed";
  allowQuickEdit?: boolean;
  onEditSuccess?: () => void;
}

/**
 * Compact badge showing context status in entity lists
 * Shows: coverage %, criticality, staleness
 * Endpoint: GET /api/projects/{projectId}/context/{type}/{id}
 */
export const InlineContextBadge: React.FC<InlineContextBadgeProps> = ({
  entityType,
  entityId,
  entityName,
  variant = "minimal",
  allowQuickEdit = false, // ADD DEFAULT
  onEditSuccess,
}) => {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  const {
    data: contextResponse,
    isLoading,
    error,
  } = useApi<ContextSummary>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}`,
    {
      enabled: hasProject && !!selectedProjectId && !!entityId,
      staleTime: 5 * 60 * 1000, // Cache for 5 minutes
      retry: 1, // Only retry once for inline badges
      showErrorToast: false, // Don't show toast errors for inline badges
    }
  );

  // Loading state
  if (isLoading) {
    return (
      <Badge variant="outline" className="animate-pulse">
        <div className="h-3 w-12 bg-muted rounded" />
      </Badge>
    );
  }

  // Error state (silent - just show unavailable)
  if (error) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Badge variant="outline" className="cursor-help opacity-50">
              <AlertCircle className="w-3 h-3 mr-1" />
              N/A
            </Badge>
          </TooltipTrigger>
          <TooltipContent>
            <p>Context information unavailable</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  const context = contextResponse?.context;
  const completeness = contextResponse?.completenessScore || 0;
  const isStale = contextResponse?.isStale || false;
  const experts = contextResponse?.experts || [];
  const detailsRoute =
    selectedProjectId != null
      ? getEntityRoute(entityType, entityId, selectedProjectId)
      : null;

  // No context = show prompt
  if (!contextResponse || !context || !context.purpose) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <Badge
              variant="outline"
              className="cursor-help border-dashed hover:border-solid transition-all"
            >
              <AlertCircle className="w-3 h-3 mr-1" />
              No docs
            </Badge>
          </TooltipTrigger>
          <TooltipContent>
            <p>This {entityType.toLowerCase()} hasn't been documented yet</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  // Minimal variant - just completeness
  if (variant === "minimal") {
    const badge = (
      <Badge
        variant={
          completeness >= 80
            ? "default"
            : completeness >= 50
              ? "secondary"
              : "outline"
        }
        className="cursor-help gap-1"
      >
        <FileText className="w-3 h-3" />
        {completeness}%
        {isStale && <AlertTriangle className="w-3 h-3 text-orange-500" />}
      </Badge>
    );

    if (allowQuickEdit) {
      return (
        <RequireProject fallback="silent">
          <div className="inline-flex items-center gap-1">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>{badge}</TooltipTrigger>
                <TooltipContent>
                  <p>
                    {completeness}% documented{isStale && " (needs review)"}
                  </p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>

            {/* Quick Edit Button */}
            <QuickContextDialog
              entityId={String(entityId)}
              entityType={entityType}
              entityName={entityName}
              currentPurpose={context?.purpose}
              currentCriticalityLevel={
                isCriticalityLevel(context?.criticalityLevel)
                  ? context.criticalityLevel
                  : undefined
              }
              onSuccess={onEditSuccess}
              trigger={
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6 hover:bg-accent"
                  title="Quick edit context"
                  aria-label="Quick edit context"
                >
                  <Edit2 className="h-3 w-3" />
                </Button>
              }
            />
          </div>
        </RequireProject>
      );
    }
    
    return badge;
  }

  // Detailed variant - hover card with more info
  return (
    <RequireProject fallback="silent">
      <TooltipProvider>
        <HoverCard>
          <HoverCardTrigger asChild>
            <div className="flex items-center gap-1 cursor-pointer">
              {/* Completeness Badge */}
              <Badge
                variant={
                  completeness >= 80
                    ? "default"
                    : completeness >= 50
                      ? "secondary"
                      : "outline"
                }
                className="gap-1"
              >
                <FileText className="w-3 h-3" />
                {completeness}%
              </Badge>

              {/* Criticality Badge */}
              {context.criticalityLevel && context.criticalityLevel >= 4 && (
                <Badge variant="destructive" className="gap-1">
                  <Shield className="w-3 h-3" />
                  Critical
                </Badge>
              )}

              {/* Sensitivity Badge for columns */}
              {entityType === "COLUMN" &&
                context.sensitivity &&
                (HIGH_SENSITIVITY_LEVELS as readonly string[]).includes(
                  context.sensitivity
                ) && (
                  <Badge variant="secondary" className="gap-1">
                    <Shield className="w-3 h-3" />
                    {context.sensitivity}
                  </Badge>
                )}

              {/* Stale Warning */}
              {isStale && (
                <Badge
                  variant="outline"
                  className="gap-1 border-orange-500 text-orange-600"
                >
                  <AlertTriangle className="w-3 h-3" />
                  Stale
                </Badge>
              )}

              {/* Experts Count */}
              {experts.length > 0 && (
                <Badge variant="outline" className="gap-1">
                  <Users className="w-3 h-3" />
                  {experts.length}
                </Badge>
              )}
            </div>
          </HoverCardTrigger>

          <HoverCardContent className="w-80" side="right">
            <div className="space-y-3">
              {/* Header */}
              <div>
                <h4 className="font-semibold text-sm">{entityName}</h4>
                <p className="text-xs text-muted-foreground">
                  {getEntityTypeLabel(entityType)} Documentation
                </p>
              </div>

              {/* Purpose */}
              {context.purpose && (
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-1">
                    Purpose:
                  </p>
                  <p className="text-sm">
                    {context.purpose.length > 100
                      ? `${context.purpose.substring(0, 100)}...`
                      : context.purpose}
                  </p>
                </div>
              )}

              {/* Business Impact for tables/columns */}
              {context.businessImpact &&
                (entityType === "TABLE" || entityType === "COLUMN") && (
                  <div>
                    <p className="text-xs font-medium text-muted-foreground mb-1">
                      Business Impact:
                    </p>
                    <p className="text-sm text-orange-700 dark:text-orange-300">
                      {context.businessImpact.length > 100
                        ? `${context.businessImpact.substring(0, 100)}...`
                        : context.businessImpact}
                    </p>
                  </div>
                )}

              {/* Quick Info Grid */}
              <div className="grid grid-cols-2 gap-2 text-xs">
                {context.businessDomain && (
                  <div className="flex items-center">
                    <span className="text-muted-foreground">Domain:</span>
                    <Badge variant="outline" className="ml-1 text-xs">
                      {context.businessDomain}
                    </Badge>
                  </div>
                )}

                {context.dataOwner && (
                  <div>
                    <span className="text-muted-foreground">Owner:</span>
                    <span className="ml-1 font-medium">
                      {context.dataOwner}
                    </span>
                  </div>
                )}

                <div>
                  <span className="text-muted-foreground">Completeness:</span>
                  <span className="ml-1 font-medium">{completeness}%</span>
                </div>

                <div>
                  <span className="text-muted-foreground">Criticality:</span>
                  <span className="ml-1 font-medium">
                    {context.criticalityLevel || 3}/5
                  </span>
                </div>

                {entityType === "COLUMN" && context.sensitivity && (
                  <div className="col-span-2">
                    <span className="text-muted-foreground">Sensitivity:</span>
                    <Badge
                      variant={
                        (CRITICAL_SENSITIVITY_LEVELS as readonly string[]).includes(context.sensitivity)
                          ? "destructive"
                          : "secondary"
                      }
                      className="ml-1 text-xs"
                    >
                      {context.sensitivity}
                    </Badge>
                  </div>
                )}
              </div>

              {/* Experts */}
              {experts.length > 0 && (
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-1">
                    Experts:
                  </p>
                  <div className="flex flex-wrap gap-1">
                    {experts.slice(0, 3).map((expert) => (
                      <Badge
                        key={expert.userId}
                        variant="secondary"
                        className="text-xs"
                      >
                        {expert.user?.fullName ||
                          expert.user?.username ||
                          "Unknown"}
                      </Badge>
                    ))}
                    {experts.length > 3 && (
                      <Badge variant="outline" className="text-xs">
                        +{experts.length - 3} more
                      </Badge>
                    )}
                  </div>
                </div>
              )}

              {/* Last Review Info */}
              {contextResponse.lastReviewed && (
                <div className="text-xs text-muted-foreground">
                  Last reviewed{" "}
                  {formatRelativeTime(contextResponse.lastReviewed)}
                  {context.reviewedBy && ` by ${context.reviewedBy}`}
                </div>
              )}

              {/* Warnings */}
              {isStale && (
                <div className="flex items-start gap-2 p-2 rounded bg-orange-50 dark:bg-orange-950 border border-orange-200 dark:border-orange-800">
                  <AlertTriangle className="w-4 h-4 text-orange-500 flex-shrink-0 mt-0.5" />
                  <p className="text-xs text-orange-700 dark:text-orange-300">
                    This documentation needs review due to recent schema changes
                  </p>
                </div>
              )}

              {/* Action Button */}
              {detailsRoute ? (
                <Button size="sm" className="w-full" variant="outline" asChild>
                  <Link to={detailsRoute}>
                    <ExternalLink className="w-3 h-3 mr-2" />
                    View Details
                  </Link>
                </Button>
              ) : (
                <Button
                  size="sm"
                  className="w-full"
                  variant="outline"
                  disabled
                  title="Select a project to view details"
                >
                  <ExternalLink className="w-3 h-3 mr-2" />
                  View Details
                </Button>
              )}
            </div>
          </HoverCardContent>
        </HoverCard>
      </TooltipProvider>
    </RequireProject>
  );
};

// Helper functions
function getEntityTypeLabel(entityType: string): string {
  switch (entityType) {
    case "TABLE":
      return "Table";
    case "COLUMN":
      return "Column";
    case "SP":
      return "Stored Procedure";
    default:
      return entityType;
  }
}

function getEntityRoute(
  entityType: string,
  entityId: number,
  projectId: number
): string {
  switch (entityType) {
    case "TABLE":
      return `/projects/${projectId}/tables/${entityId}`;
    case "SP":
      return `/projects/${projectId}/stored-procedures/${entityId}`;
    case "COLUMN":
      return `/projects/${projectId}/columns/${entityId}`;
    default:
      return `/projects/${projectId}`;
  }
}

function formatRelativeTime(date: string): string {
  const now = new Date();
  const past = new Date(date);
  const diffInSeconds = Math.floor((now.getTime() - past.getTime()) / 1000);
  const diffInMinutes = Math.floor(diffInSeconds / 60);
  const diffInHours = Math.floor(diffInMinutes / 60);
  const diffInDays = Math.floor(diffInHours / 24);

  if (diffInSeconds < 60) return "just now";
  if (diffInMinutes < 60) return `${diffInMinutes}m ago`;
  if (diffInHours < 24) return `${diffInHours}h ago`;
  if (diffInDays === 1) return "yesterday";
  if (diffInDays < 7) return `${diffInDays}d ago`;
  return past.toLocaleDateString();
}
