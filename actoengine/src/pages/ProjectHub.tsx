import { useEffect } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import { useProject } from '../hooks/useProject';
import { useApi } from '../hooks/useApi';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Skeleton } from '../components/ui/skeleton';
import {
  Database,
  Code,
  Clock,
  FileText,
  Users,
  Settings2,
  ChevronRight,
  Ghost,
  Loader2,
  AlertCircle,
  CheckCircle,
  XCircle
} from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import { ScrollArea } from '../components/ui/scroll-area';
import type { ProjectStatsResponse, ActivityItem } from '../types/api';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '../components/ui/breadcrumb';

export default function ProjectHub() {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const { selectedProject, selectProject } = useProject();

  const {
    data: stats,
    isLoading: isLoadingStats,
    error: statsError
  } = useApi<ProjectStatsResponse>(`/Project/${projectId}/stats`, {
    enabled: !!projectId,
    staleTime: 2 * 60 * 1000
  });

  // Fetch recent activity
  const {
    data: activity,
    isLoading: isLoadingActivity
  } = useApi<ActivityItem[]>(`/Project/${projectId}/activity?limit=5`, {
    enabled: !!projectId,
    staleTime: 1 * 60 * 1000
  });

  useEffect(() => {
    if (projectId && selectedProject?.projectId !== parseInt(projectId)) {
      selectProject(parseInt(projectId));
    }
  }, [projectId, selectedProject?.projectId]);

  const getSyncStatusBadge = () => {
    if (!selectedProject) return null;

    const { syncStatus, syncProgress } = selectedProject;

    if (!syncStatus || syncStatus === 'never') {
      return <Badge variant="secondary">Never Synced</Badge>;
    }

    const statusMap: Record<string, { variant: any; icon: any; label: string }> = {
      completed: {
        variant: 'success' as any,
        icon: CheckCircle,
        label: 'Completed'
      },
      in_progress: {
        variant: 'warning' as any,
        icon: Loader2,
        label: syncProgress ? `In Progress (${syncProgress}%)` : 'In Progress'
      },
      failed: {
        variant: 'destructive',
        icon: XCircle,
        label: 'Failed'
      }
    };

    const config = statusMap[syncStatus.toLowerCase()] || statusMap.failed;
    const Icon = config.icon;

    return (
      <Badge variant={config.variant} className="gap-1">
        <Icon className={`w-3 h-3 ${syncStatus === 'in_progress' ? 'animate-spin' : ''}`} />
        {config.label}
      </Badge>
    );
  };

  if (!selectedProject) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <Loader2 className="w-8 h-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <ScrollArea className="h-[calc(100vh-110px)] overflow-y-auto border-radius-lg">
      <div className="container mx-auto px-4 py-6 space-y-6">
        <Breadcrumb>
          <BreadcrumbList>
            <BreadcrumbItem>
              <BreadcrumbLink asChild>
                <Link to="/projects" className="hover:text-foreground transition-colors">
                  Projects
                </Link>
              </BreadcrumbLink>
              <BreadcrumbSeparator />
              <BreadcrumbPage>
                {selectedProject.projectName}
              </BreadcrumbPage>
            </BreadcrumbItem>
          </BreadcrumbList>
        </Breadcrumb>

        {/* Header */}
        <div className="space-y-2">
          <div className="flex items-start justify-between">
            <div className="space-y-1">
              <h1 className="text-3xl font-bold tracking-tight">{selectedProject.projectName}</h1>
              <p className="text-muted-foreground">
                {selectedProject.databaseName || 'No database connected'}
              </p>
            </div>
            {getSyncStatusBadge()}
          </div>
          {selectedProject.description && (
            <p className="text-sm text-muted-foreground max-w-3xl">
              {selectedProject.description}
            </p>
          )}
        </div>

        {/* Stats Cards */}
        <div className="grid gap-4 md:grid-cols-3">
          {/* Tables */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Tables</CardTitle>
              <Database className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              {isLoadingStats ? (
                <Skeleton className="h-8 w-16" />
              ) : statsError ? (
                <div className="flex items-center text-sm text-destructive">
                  <AlertCircle className="w-4 h-4 mr-1" />
                  Error
                </div>
              ) : (
                <div className="text-2xl font-bold">{stats?.tableCount || 0}</div>
              )}
              <p className="text-xs text-muted-foreground mt-1">
                Database tables
              </p>
            </CardContent>
          </Card>

          {/* Stored Procedures */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Stored Procedures</CardTitle>
              <Code className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              {isLoadingStats ? (
                <Skeleton className="h-8 w-16" />
              ) : statsError ? (
                <div className="flex items-center text-sm text-destructive">
                  <AlertCircle className="w-4 h-4 mr-1" />
                  Error
                </div>
              ) : (
                <div className="text-2xl font-bold">{stats?.spCount || 0}</div>
              )}
              <p className="text-xs text-muted-foreground mt-1">
                Stored procedures
              </p>
            </CardContent>
          </Card>

          {/* Last Sync */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Last Sync</CardTitle>
              <Clock className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              {isLoadingStats ? (
                <Skeleton className="h-8 w-24" />
              ) : (
                <div className="text-2xl font-bold">
                  {stats?.lastSync
                    ? formatDistanceToNow(new Date(stats.lastSync), { addSuffix: true })
                    : 'Never'}
                </div>
              )}
              <p className="text-xs text-muted-foreground mt-1">
                Schema synchronization
              </p>
            </CardContent>
          </Card>
        </div>

        <div className="grid gap-6 md:grid-cols-2">
          {/* Recent Activity */}
          <Card>
            <CardHeader>
              <CardTitle>Recent Activity</CardTitle>
              <CardDescription>Latest changes and updates</CardDescription>
            </CardHeader>
            <CardContent>
              {isLoadingActivity ? (
                <div className="space-y-3">
                  {[...Array(3)].map((_, i) => (
                    <div key={i} className="space-y-2">
                      <Skeleton className="h-4 w-full" />
                      <Skeleton className="h-3 w-2/3" />
                    </div>
                  ))}
                </div>
              ) : !activity || activity.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-8 text-center">
                  <Ghost className="w-12 h-12 text-muted-foreground mb-3" />
                  <p className="text-sm text-muted-foreground">No recent activity</p>
                </div>
              ) : (
                <div className="space-y-4">
                  <ul className="space-y-3">
                    {activity.map((item, index) => (
                      <li key={index} className="text-sm">
                        <div className="flex items-start gap-2">
                          <div className="mt-0.5">
                            <div className="w-2 h-2 rounded-full bg-primary" />
                          </div>
                          <div className="flex-1 space-y-1">
                            <p className="text-foreground leading-tight">
                              {item.description}
                            </p>
                            <p className="text-xs text-muted-foreground">
                              {formatDistanceToNow(new Date(item.timestamp), { addSuffix: true })}
                              {item.user && ` by ${item.user}`}
                            </p>
                          </div>
                        </div>
                      </li>
                    ))}
                  </ul>
                  <Link
                    to={`/project/${projectId}/activity`}
                    className="text-sm text-primary hover:underline inline-flex items-center"
                  >
                    View All Activity
                    <ChevronRight className="w-4 h-4 ml-1" />
                  </Link>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Quick Actions */}
          <Card>
            <CardHeader>
              <CardTitle>Quick Actions</CardTitle>
              <CardDescription>Jump to tools and features</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 gap-3">
                {/* Database Schema - Primary */}
                <Button
                  variant="default"
                  className="h-24 flex-col gap-2"
                  onClick={() => navigate(`/project/${projectId}/schema`)}
                >
                  <Database className="w-6 h-6" />
                  <span className="text-sm font-medium">Schema</span>
                </Button>

                {/* Form Builder */}
                <Button
                  variant="outline"
                  className="h-24 flex-col gap-2"
                  onClick={() => navigate(`/project/${projectId}/forms`)}
                >
                  <FileText className="w-6 h-6" />
                  <span className="text-sm font-medium">Forms</span>
                </Button>

                {/* SP Generator */}
                <Button
                  variant="outline"
                  className="h-24 flex-col gap-2"
                  onClick={() => navigate(`/project/${projectId}/sp-gen`)}
                >
                  <Code className="w-6 h-6" />
                  <span className="text-sm font-medium">SP Gen</span>
                </Button>

                {/* Client Management */}
                <Button
                  variant="outline"
                  className="h-24 flex-col gap-2"
                  onClick={() => navigate(`/project/${projectId}/clients`)}
                >
                  <Users className="w-6 h-6" />
                  <span className="text-sm font-medium">Clients</span>
                </Button>

                {/* Settings - Spans 2 columns */}
                <Button
                  variant="outline"
                  className="h-16 col-span-2 flex-row gap-2"
                  onClick={() => navigate(`/project/${projectId}/settings`)}
                >
                  <Settings2 className="w-5 h-5" />
                  <span className="text-sm font-medium">Project Settings</span>
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </ScrollArea>
  );
}