import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Loader2, CheckCircle2, XCircle, RefreshCw, X } from "lucide-react";
import { utcToLocal } from "@/lib/utils";
import { useSyncStatus } from "@/hooks/useSyncStatus";
import { useState } from "react";

interface SyncProgressPanelProps {
  projectId: number;
  useSSE?: boolean;
  onDismiss?: () => void;
  showDismiss?: boolean;
}

/**
 * SyncProgressPanel - Displays detailed sync progress information
 * Shows real-time updates during sync via SSE, then falls back to REST polling
 */
export function SyncProgressPanel({
  projectId,
  useSSE = true,
  onDismiss,
  showDismiss = true,
}: SyncProgressPanelProps) {
  const [dismissed, setDismissed] = useState(false);

  const { status, progress, lastSyncAttempt, isConnected, error, refresh } =
    useSyncStatus(projectId, {
      enabled: true, // Always enabled to track status changes
      useSSE,
    });

  if (dismissed) {
    return null;
  }

  // Don't show panel if there's no status and no active sync
  if (!status && !useSSE) {
    return null;
  }

  const handleDismiss = () => {
    setDismissed(true);
    onDismiss?.();
  };

  const isCompleted = status === "Completed";
  const isFailed = status?.startsWith("Failed");
  const isSyncing = !isCompleted && !isFailed;

  // Get status color
  const getStatusColor = () => {
    if (isCompleted) return "text-green-600 dark:text-green-400";
    if (isFailed) return "text-red-600 dark:text-red-400";
    return "text-blue-600 dark:text-blue-400";
  };

  // Get status icon
  const getStatusIcon = () => {
    if (isCompleted) return <CheckCircle2 className="w-5 h-5 text-green-600" />;
    if (isFailed) return <XCircle className="w-5 h-5 text-red-600" />;
    return <Loader2 className="w-5 h-5 text-blue-600 animate-spin" />;
  };

  // Get connection badge
  const getConnectionBadge = () => {
    if (isConnected) {
      return (
        <Badge variant="outline" className="text-xs">
          Live
        </Badge>
      );
    }
    return null;
  };

  return (
    <Card className="w-full border-l-4 border-l-blue-500">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CardTitle className="text-lg">Sync Progress</CardTitle>
            {getConnectionBadge()}
          </div>
          <div className="flex items-center gap-2">
            {!isSyncing && (
              <Button
                variant="ghost"
                size="sm"
                onClick={refresh}
                className="h-8 w-8 p-0"
                title="Refresh status"
              >
                <RefreshCw className="w-4 h-4" />
              </Button>
            )}
            {showDismiss && (isCompleted || isFailed) && (
              <Button
                variant="ghost"
                size="sm"
                onClick={handleDismiss}
                className="h-8 w-8 p-0"
                title="Dismiss"
              >
                <X className="w-4 h-4" />
              </Button>
            )}
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Status Message */}
        <div className="flex items-start gap-3">
          {getStatusIcon()}
          <div className="flex-1 space-y-1">
            <p className={`font-medium ${getStatusColor()}`}>{status}</p>
            {lastSyncAttempt && (
              <p className="text-sm text-muted-foreground">
                Last updated: {utcToLocal(lastSyncAttempt, "PPp")}
              </p>
            )}
          </div>
        </div>

        {/* Progress Bar */}
        {isSyncing && (
          <div className="space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Progress</span>
              <span className="font-medium">{progress}%</span>
            </div>
            <Progress value={progress} className="h-2" />
          </div>
        )}

        {/* Completion Progress (when done) */}
        {(isCompleted || isFailed) && (
          <div className="space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Progress</span>
              <span className="font-medium">{progress}%</span>
            </div>
            <Progress value={progress} className="h-2" />
          </div>
        )}

        {/* Error Message */}
        {error && (
          <div className="rounded-md bg-red-50 dark:bg-red-950 p-3 border border-red-200 dark:border-red-800">
            <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
          </div>
        )}

        {/* Sync Stages Info */}
        {isSyncing && (
          <div className="text-xs text-muted-foreground space-y-1 pt-2 border-t">
            <p>
              Sync stages: Tables → Columns → Foreign Keys → Stored Procedures
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
