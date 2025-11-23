// components/context/ContextCoverageWidget.tsx
import React from "react";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import { formatRelativeTime } from "@/lib/utils";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Link } from "react-router-dom";
import {
  TrendingUp,
  TrendingDown,
  Minus,
  ExternalLink,
  AlertCircle,
} from "lucide-react";

// Types
interface CoverageItem {
  entityType: "TABLE" | "COLUMN" | "SP";
  total: number;
  documented: number;
  coveragePercentage: number;
}

interface CoverageData {
  breakdown: CoverageItem[];
  lastUpdated?: string;
  trends?: {
    change: number;
    previousPeriod: number;
  };
}

/**
 * Compact widget showing context coverage stats
 * Can be embedded in sidebar, dashboard, or entity lists
 * Endpoint: GET /api/projects/{projectId}/context/statistics/coverage
 */
export const ContextCoverageWidget: React.FC = () => {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  const {
    data: coverageData,
    isLoading,
    error,
  } = useApi<CoverageData>(
    `/projects/${selectedProjectId}/context/statistics/coverage`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 30 * 1000, // 30 seconds
      refetchInterval: 60 * 1000, // Refresh every minute
      retry: 2,
    }
  );

  // Error state
  if (error) {
    return (
      <Card>
        <CardContent className="py-4">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription className="text-xs">
              Failed to load coverage stats
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  // Loading state
  if (isLoading || !coverageData) {
    return (
      <Card>
        <CardContent className="py-4">
          <div className="animate-pulse space-y-2">
            <div className="h-4 bg-muted rounded w-3/4" />
            <div className="h-2 bg-muted rounded" />
            <div className="space-y-1">
              <div className="h-3 bg-muted rounded w-full" />
              <div className="h-3 bg-muted rounded w-5/6" />
              <div className="h-3 bg-muted rounded w-4/5" />
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  // No project selected
  if (!hasProject) {
    return (
      <Card>
        <CardContent className="py-4">
          <p className="text-xs text-muted-foreground text-center">
            Select a project to view coverage
          </p>
        </CardContent>
      </Card>
    );
  }

  const coverage = coverageData.breakdown || [];

  // Calculate overall metrics
  const totalEntities = coverage.reduce((sum, item) => sum + item.total, 0);
  const totalDocumented = coverage.reduce(
    (sum, item) => sum + item.documented,
    0
  );
  const overallPercentage =
    totalEntities > 0 ? Math.round((totalDocumented / totalEntities) * 100) : 0;

  // Determine trend - use real trend data if available, fallback to percentage-based
  const getTrendIcon = (percentage: number) => {
    const trendChange = coverageData.trends?.change;

    if (trendChange !== undefined) {
      if (trendChange > 0)
        return <TrendingUp className="w-3 h-3 text-green-500" />;
      if (trendChange < 0)
        return <TrendingDown className="w-3 h-3 text-red-500" />;
      return <Minus className="w-3 h-3 text-muted-foreground" />;
    }

    // Fallback to percentage-based trend
    if (percentage >= 70)
      return <TrendingUp className="w-3 h-3 text-green-500" />;
    if (percentage >= 40) return <Minus className="w-3 h-3 text-yellow-500" />;
    return <TrendingDown className="w-3 h-3 text-red-500" />;
  };

  const getTrendColor = (percentage: number) => {
    const trendChange = coverageData.trends?.change;

    if (trendChange !== undefined) {
      if (trendChange > 0) return "text-green-500";
      if (trendChange < 0) return "text-red-500";
      return "text-muted-foreground";
    }

    // Fallback to percentage-based color
    if (percentage >= 70) return "text-green-500";
    if (percentage >= 40) return "text-yellow-500";
    return "text-red-500";
  };

  const getTrendLabel = (percentage: number) => {
    const trendChange = coverageData.trends?.change;

    if (trendChange !== undefined) {
      const sign = trendChange > 0 ? "+" : "";
      return `${sign}${trendChange}%`;
    }

    // Fallback to status label
    if (percentage >= 70) return "Good";
    if (percentage >= 40) return "Fair";
    return "Low";
  };

  const getEntityTypeLabel = (entityType: string) => {
    switch (entityType) {
      case "TABLE":
        return "Tables";
      case "COLUMN":
        return "Columns";
      case "SP":
        return "Procedures";
      default:
        return entityType;
    }
  };

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-sm font-medium">
            Documentation Coverage
          </CardTitle>
          <Button variant="ghost" size="icon" className="h-6 w-6" asChild>
            <Link to="/context/dashboard" aria-label="Open Context Dashboard">
              <ExternalLink className="w-3 h-3" />
            </Link>
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Overall Progress */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-2xl font-bold">{overallPercentage}%</span>
            <div className="flex items-center gap-1">
              {getTrendIcon(overallPercentage)}
              <span
                className={`text-xs font-medium ${getTrendColor(overallPercentage)}`}
              >
                {getTrendLabel(overallPercentage)}
              </span>
            </div>
          </div>
          <Progress value={overallPercentage} className="h-2" />
          <p className="text-xs text-muted-foreground">
            {totalDocumented} of {totalEntities} entities documented
          </p>
        </div>

        {/* Breakdown by Type */}
        {coverage.length > 0 && (
          <div className="space-y-2 pt-2 border-t">
            {coverage.map((item) => (
              <div
                key={item.entityType}
                className="flex items-center justify-between text-xs"
              >
                <span className="text-muted-foreground">
                  {getEntityTypeLabel(item.entityType)}
                </span>
                <div className="flex items-center gap-2">
                  <span className="text-muted-foreground">
                    {item.documented}/{item.total}
                  </span>
                  <Badge
                    variant={
                      item.coveragePercentage >= 70
                        ? "default"
                        : item.coveragePercentage >= 40
                          ? "secondary"
                          : "outline"
                    }
                    className="min-w-[3rem] justify-center"
                  >
                    {Math.round(item.coveragePercentage || 0)}%
                  </Badge>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* No data state */}
        {coverage.length === 0 && (
          <div className="pt-2 border-t">
            <p className="text-xs text-muted-foreground text-center py-2">
              No entities found in this project
            </p>
          </div>
        )}

        {/* Project info and last updated */}
        {(selectedProject?.projectName || coverageData.lastUpdated) && (
          <div className="pt-2 border-t space-y-2">
            {selectedProject?.projectName && (
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Project:</span>
                <span
                  className="font-medium truncate ml-2 max-w-[60%]"
                  title={selectedProject.projectName}
                >
                  {selectedProject.projectName}
                </span>
              </div>
            )}
            {coverageData.lastUpdated && (
              <p className="text-xs text-muted-foreground">
                Updated{" "}
                {formatRelativeTime(coverageData.lastUpdated, "recently")}
              </p>
            )}
          </div>
        )}

        {/* Quick Action */}
        <Button variant="outline" size="sm" className="w-full" asChild>
          <Link to="/context">View Full Dashboard</Link>
        </Button>
      </CardContent>
    </Card>
  );
};

// Removed local time formatter; using date-fns formatDistanceToNow
