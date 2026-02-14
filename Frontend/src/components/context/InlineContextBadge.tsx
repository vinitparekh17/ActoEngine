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
import { Skeleton } from "@/components/ui/skeletons";
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from "@/components/ui/hover-card";
import {
  FileText,
  AlertTriangle,
  Users,
  Shield,
  AlertCircle,
  ExternalLink,
  Edit2,
  BadgeAlert,
} from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { QuickContextDialog } from "./QuickContextDialog";
// import { RequireProject } from '@/components/errors/';

// Types
interface ContextSummary {
  context: {
    purpose?: string;
    businessImpact?: string;
    businessDomain?: string;

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
    } | null;
    expertiseLevel: string;
  }>;
  lastReviewed?: string;
  lastReviewedAt?: string; // Fallback mapping
}

interface InlineContextBadgeProps {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  variant?: "minimal" | "detailed";
  allowQuickEdit?: boolean;
  onEditSuccess?: () => void;
  preloadedContext?: ContextSummary;
  disableFetch?: boolean;
  loading?: boolean;
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
  allowQuickEdit = false,
  onEditSuccess,
  preloadedContext,
  disableFetch = false,
  loading = false,
}) => {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  const {
    data: fetchedContext,
    isLoading: isFetching,
    error,
  } = useApi<ContextSummary>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}`,
    {
      enabled:
        !disableFetch &&
        !preloadedContext &&
        hasProject &&
        !!selectedProjectId &&
        !!entityId,
      staleTime: 5 * 60 * 1000, // Cache for 5 minutes
      retry: 1, // Only retry once for inline badges
      showErrorToast: false, // Don't show toast errors for inline badges
    },
  );

  const contextResponse = preloadedContext || fetchedContext;
  const isLoading = loading || (!preloadedContext && isFetching);

  // Loading state
  if (isLoading) {
    return <Skeleton className="h-5 w-16 rounded-full" />;
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

  // No project selected
  if (!hasProject) {
    return null;
  }

  const context = contextResponse?.context;
  const completeness = contextResponse?.completenessScore || 0;
  const isStale = contextResponse?.isStale || false;
  const hasContext = !!(contextResponse && context && context.purpose);
  const experts = contextResponse?.experts || [];
  const detailsRoute =
    selectedProjectId != null
      ? getEntityRoute(entityType, entityId, selectedProjectId)
      : null;

  const createBadge = () => {
    if (!hasContext) {
      // No context badge
      return (
        <Badge
          variant="outline"
          className="cursor-help border-dashed hover:border-solid transition-all"
        >
          <AlertCircle className="w-3 h-3 mr-1" />
          No docs
        </Badge>
      );
    }

    // Has context badge
    return (
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
  };

  // No context = show prompt
  if (!hasContext) {
    return (
      <QuickContextDialog
        entityId={String(entityId)}
        entityType={entityType}
        entityName={entityName}
        currentPurpose={context?.purpose}
        currentSensitivity="PUBLIC"
        onSuccess={onEditSuccess}
        trigger={
          <Badge
            variant="outline"
            className="cursor-pointer border-dashed hover:border-solid hover:bg-accent/50 transition-all"
          >
            <AlertCircle className="w-3 h-3 mr-1" />
            No docs
            <Edit2 className="h-3 w-3 ml-1 opacity-60" />
          </Badge>
        }
      />
    );
  }

  // Minimal variant - just completeness
  if (variant === "minimal") {
    const badge = createBadge();
    const tooltipText = hasContext
      ? `${completeness}% documented${isStale ? " (needs review)" : ""}`
      : `This ${entityType.toLowerCase()} hasn't been documented yet`;

    // If quick edit is enabled, wrap with edit button
    if (allowQuickEdit) {
      return (
        <div className="inline-flex items-center gap-1">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>{badge}</TooltipTrigger>
              <TooltipContent>
                <p>{tooltipText}</p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>

          {/* Quick Edit Button - Always show when allowQuickEdit=true */}
          <QuickContextDialog
            entityId={String(entityId)}
            entityType={entityType}
            entityName={entityName}
            currentPurpose={context?.purpose}
            currentSensitivity={context?.sensitivity || "PUBLIC"}
            onSuccess={onEditSuccess}
            trigger={
              <Button
                variant="ghost"
                size="icon"
                className="h-6 w-6 hover:bg-accent"
                title={hasContext ? "Edit context" : "Add context"}
              >
                <Edit2 className="h-3 w-3" />
              </Button>
            }
          />
        </div>
      );
    }

    // FIX: minimal variant without quick edit should just return the tooltip
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>{badge}</TooltipTrigger>
          <TooltipContent>
            <p>{tooltipText}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
  // Detailed variant - hover card with more info
  return (
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
              ["PII", "FINANCIAL", "SENSITIVE"].includes(
                context.sensitivity,
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
                      ["PII", "FINANCIAL"].includes(context.sensitivity)
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
                {formatRelativeTime(
                  contextResponse.lastReviewed ||
                  contextResponse.lastReviewedAt ||
                  "",
                )}
                {(context.reviewedBy || (context as any).updatedBy) &&
                  ` by ${context.reviewedBy || (context as any).updatedBy}`}
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
  projectId: number,
): string {
  const entityTypeSlug = entityType === "TABLE" ? "tables" : "stored-procedures";
  return `/project/${projectId}/${entityTypeSlug}/${entityId}/detail`;
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
