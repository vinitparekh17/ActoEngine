import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useProject } from "../hooks/useProject";
import { useApiMutation } from "../hooks/useApi";
import { useSyncStatus } from "../hooks/useSyncStatus";
import {
  Database,
  Plus,
  Settings,
  Trash2,
  Activity,
  Clock,
  CheckCircle,
  AlertCircle,
  Loader2,
  ExternalLink,
} from "lucide-react";
import { useConfirm } from "../hooks/useConfirm";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "../components/ui/card";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { ScrollArea } from "../components/ui/scroll-area";
import { formatRelativeTime } from "../lib/utils";
import type { Project } from "../types/project";
import { GridSkeleton, Skeleton } from "../components/ui/skeletons";

// Helper component for real-time sync status badge
function SyncStatusBadge({ project }: { project: Project }) {
  // Check if project is actively syncing
  const isSyncing =
    project.syncStatus?.toLowerCase().includes("sync") ||
    project.syncStatus?.toLowerCase() === "started";

  // Use real-time updates if syncing
  const { status: realtimeStatus, progress: realtimeProgress } = useSyncStatus(
    project.projectId,
    {
      enabled: isSyncing,
      onComplete: () => {
        // Could trigger a refetch here if needed
        console.log("Sync completed for project", project.projectId);
      },
    },
  );

  // Use real-time data if available, otherwise use project data
  const displayStatus = realtimeStatus || project.syncStatus;
  const displayProgress = realtimeProgress || project.syncProgress || 0;

  const map: Record<
    string,
    {
      variant: "default" | "secondary" | "destructive" | "outline";
      icon: any;
      label: string;
    }
  > = {
    completed: { variant: "default", icon: CheckCircle, label: "Synced" },
    started: { variant: "secondary", icon: Loader2, label: "Syncing" },
    "syncing tables": { variant: "secondary", icon: Loader2, label: "Syncing" },
    "syncing columns": {
      variant: "secondary",
      icon: Loader2,
      label: "Syncing",
    },
    "syncing foreign keys": {
      variant: "secondary",
      icon: Loader2,
      label: "Syncing",
    },
    "syncing stored procedures": {
      variant: "secondary",
      icon: Loader2,
      label: "Syncing",
    },
    failed: { variant: "destructive", icon: AlertCircle, label: "Failed" },
    pending: { variant: "outline", icon: Clock, label: "Pending" },
  };

  const state = map[displayStatus?.toLowerCase() ?? "pending"] ?? map.pending;
  const Icon = state.icon;
  const isSpinning =
    displayStatus?.toLowerCase().includes("sync") ||
    displayStatus?.toLowerCase() === "started";

  return (
    <Badge variant={state.variant} className="flex items-center gap-1">
      <Icon className={`h-3 w-3 ${isSpinning ? "animate-spin" : ""}`} />
      {state.label}
      {displayProgress > 0 && displayProgress < 100 && (
        <span>({displayProgress}%)</span>
      )}
    </Badge>
  );
}

export default function ProjectsDashboard() {
  const navigate = useNavigate();
  const { projects, isLoadingProjects, refetchProjects } = useProject();
  const { confirm } = useConfirm();

  const [deletingProjectId, setDeletingProjectId] = useState<number | null>(null);

  const deleteMutation = useApiMutation<void, { projectId: number }>(
    "/projects/:projectId",
    "DELETE",
    {
      onSuccess: () => {
        setDeletingProjectId(null);
        refetchProjects();
      },
      onError: () => {
        setDeletingProjectId(null);
      }
    },
  );

  const handleDelete = async (projectId: number) => {
    const confirmed = await confirm({
      title: "Delete Project",
      description:
        "Are you sure you want to delete this project? This action cannot be undone.",
      confirmText: "Delete",
      cancelText: "Cancel",
      variant: "destructive",
    });

    if (confirmed) {
      setDeletingProjectId(projectId);
      deleteMutation.mutate({ projectId });
    }
  };

  if (isLoadingProjects) {
    return (
      <div className="h-[calc(100vh-110px)] bg-gradient-to-br from-neutral-50 to-neutral-100 dark:from-neutral-900 dark:to-neutral-950">
        <div className="max-w-7xl mx-auto px-4 py-6">
          <div className="mb-8 flex items-center justify-between">
            <div className="space-y-2">
              <Skeleton className="h-8 w-48" />
              <Skeleton className="h-4 w-64" />
            </div>
            <Skeleton className="h-10 w-32" />
          </div>
          <GridSkeleton />
        </div>
      </div>
    );
  }

  return (
    <ScrollArea className="h-[calc(100vh-110px)] overflow-y-hidden border-radius-lg">
      <div className="max-w-7xl mx-auto px-4 py-6">
        {/* Header */}
        <div className="mb-8 flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold text-neutral-900 dark:text-neutral-100">
              Projects
            </h1>
            <p className="text-neutral-600 dark:text-neutral-400 mt-1">
              Manage your projects
            </p>
          </div>
          <Button onClick={() => navigate("/project/new")} className="gap-2">
            <Plus className="w-5 h-5" /> New Project
          </Button>
        </div>

        {/* Empty State */}
        {projects && projects.length === 0 ? (
          <Card className="p-12 text-center border border-neutral-200 dark:border-neutral-700">
            <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-neutral-100 dark:bg-neutral-800">
              <Database className="w-8 h-8 text-neutral-400" />
            </div>
            <CardTitle className="text-lg mb-2 text-neutral-900 dark:text-neutral-100">
              No projects yet
            </CardTitle>
            <CardDescription className="mb-6 text-neutral-600 dark:text-neutral-400">
              Get started by creating your first project
            </CardDescription>
            <Button onClick={() => navigate("/project/new")} className="gap-2">
              <Plus className="w-5 h-5" /> Create Project
            </Button>
          </Card>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {projects &&
              projects.map((project) => (
                <Card
                  key={project.projectId}
                  className="border border-neutral-200 dark:border-neutral-700 transition-shadow hover:shadow-md"
                >
                  <CardHeader>
                    <div className="flex justify-between items-start">
                      <div className="flex items-center">
                        <div className="mr-3 flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-100 dark:bg-emerald-900/30">
                          <Database className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
                        </div>
                        <div>
                          <CardTitle className="text-neutral-900 dark:text-neutral-100 hover:text-emerald-600 cursor-pointer">
                            {project.projectName}
                          </CardTitle>
                          <CardDescription className="text-xs text-neutral-500 dark:text-neutral-400">
                            {project.databaseName}
                          </CardDescription>
                        </div>
                      </div>
                      <SyncStatusBadge project={project} />
                    </div>
                    {project.description && (
                      <p className="mt-2 text-sm text-neutral-600 dark:text-neutral-400 line-clamp-2">
                        {project.description}
                      </p>
                    )}
                  </CardHeader>

                  <CardContent className="space-y-2 text-sm text-neutral-600 dark:text-neutral-400">
                    <div className="flex justify-between">
                      <span className="flex items-center gap-2">
                        <Activity className="h-4 w-4" /> Status
                      </span>
                      <span
                        className={`font-medium ${project.isActive
                            ? "text-emerald-600 dark:text-emerald-400"
                            : "text-neutral-400"
                          }`}
                      >
                        {project.isActive ? "Active" : "Inactive"}
                      </span>
                    </div>

                    <div className="flex justify-between">
                      <span className="flex items-center gap-2">
                        <Clock className="h-4 w-4" /> Created
                      </span>
                      <span className="text-neutral-900 dark:text-neutral-200">
                        {formatRelativeTime(project.createdAt, "N/A")}
                      </span>
                    </div>

                    {project.lastSyncAttempt && (
                      <div className="flex justify-between">
                        <span className="flex items-center gap-2">
                          <Database className="h-4 w-4" /> Last Sync
                        </span>
                        <span className="text-neutral-900 dark:text-neutral-200">
                          {formatRelativeTime(project.lastSyncAttempt, "Never")}
                        </span>
                      </div>
                    )}
                  </CardContent>

                  <CardFooter className="flex justify-between items-center">
                    <Button
                      variant="link"
                      onClick={() => navigate(`/project/${project.projectId}`)}
                      className="text-emerald-600 dark:text-emerald-400 p-0 h-auto"
                    >
                      Open Project
                      <ExternalLink className="w-4 h-4 ml-1" />
                    </Button>

                    <div className="flex items-center space-x-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() =>
                          navigate(`/project/${project.projectId}/settings`)
                        }
                        title="Settings"
                        className="text-neutral-400 hover:text-neutral-600 dark:hover:text-neutral-200"
                      >
                        <Settings className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => handleDelete(project.projectId)}
                        disabled={deleteMutation.isPending && deletingProjectId === project.projectId}
                        title="Delete"
                        className="text-neutral-400 hover:text-red-600 dark:hover:text-red-400"
                      >
                        {deleteMutation.isPending && deletingProjectId === project.projectId ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Trash2 className="h-4 w-4" />
                        )}
                      </Button>
                    </div>
                  </CardFooter>
                </Card>
              ))}
          </div>
        )}
      </div>
    </ScrollArea>
  );
}
