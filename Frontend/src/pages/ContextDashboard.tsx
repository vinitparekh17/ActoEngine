// components/context/ContextDashboard.tsx
import React from 'react';
import { useProject } from '@/hooks/useProject';
import { useApi } from '@/hooks/useApi';
import { Link } from 'react-router-dom';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  FileText,
  AlertTriangle,
  TrendingUp,
  Users,
  Target,
  Clock,
  CheckCircle2,
  XCircle,
  Database,
  FileCode,
  Table as TableIcon,
  AlertCircle,
  Loader2
} from 'lucide-react';

// Types
interface CoverageItem {
  entityType: 'TABLE' | 'COLUMN' | 'SP';
  total: number;
  documented: number;
  coveragePercentage: number;
  avgCompleteness?: number;
}

interface CriticalGapItem {
  entityType: 'TABLE' | 'COLUMN' | 'SP';
  entityId: number;
  entityName: string;
  reason: string;
  priority: 'HIGH' | 'MEDIUM' | 'LOW';
  lastSchemaChange?: string;
}

interface StaleItem {
  entityType: 'TABLE' | 'COLUMN' | 'SP';
  entityId: number;
  entityName: string;
  lastContextUpdate: string;
  daysSinceUpdate: number;
  schemaChanged: boolean;
}

interface WellDocumentedItem {
  entityType: 'TABLE' | 'COLUMN' | 'SP';
  entityId: number;
  entityName: string;
  businessDomain?: string;
  dataOwner?: string;
  completenessScore: number;
  expertCount: number;
}

interface DashboardData {
  coverage: CoverageItem[];
  staleEntities: StaleItem[];
  topDocumented: WellDocumentedItem[];
  criticalUndocumented: CriticalGapItem[];
  staleCount: number;
  lastUpdated: string;
  trends?: {
    coverageChange: number;
    period: string;
  };
}

/**
 * Main dashboard showing project-wide context coverage and insights
 * Endpoint: GET /api/projects/{projectId}/context/dashboard
 */
export const ContextDashboard: React.FC = () => {
  const { selectedProject, selectedProjectId, hasProject } = useProject();

  // Fetch dashboard data
  const {
    data: dashboard,
    isLoading,
    error,
    refetch
  } = useApi<DashboardData>(
    `/projects/${selectedProjectId}/context/dashboard`,
    {
      enabled: hasProject && !!selectedProjectId,
      staleTime: 30 * 1000, // 30 seconds
      refetchInterval: 30 * 1000, // Refresh every 30 seconds
      retry: 2,
    }
  );

  // Loading state
  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="flex flex-col items-center space-y-4">
          <Loader2 className="h-12 w-12 animate-spin text-primary" />
          <p className="text-muted-foreground">Loading dashboard...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load dashboard: {error.message}
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button onClick={() => refetch()} variant="outline">
            Try Again
          </Button>
        </div>
      </div>
    );
  }

  // No project selected
  if (!hasProject || !selectedProjectId) {
    return (
      <div className="space-y-6 p-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Please select a project to view the context dashboard.
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button asChild>
            <Link to="/projects">Select Project</Link>
          </Button>
        </div>
      </div>
    );
  }

  // At this point, we know selectedProjectId is defined due to the guard above
  // Create a type-safe variable with proper type assertion
  const projectId: number = selectedProjectId;

  const coverage = dashboard?.coverage || [];
  const staleEntities = dashboard?.staleEntities || [];
  const topDocumented = dashboard?.topDocumented || [];
  const criticalUndocumented = dashboard?.criticalUndocumented || [];

  // Calculate overall stats with weighted coverage
  const totalDocumented = coverage.reduce((acc, item) => acc + (item.documented || 0), 0);
  const totalEntities = coverage.reduce((acc, item) => acc + (item.total || 0), 0);
  const overallCoverage = totalEntities > 0
    ? (totalDocumented / totalEntities) * 100
    : 0;

  return (
    <div className="space-y-6 p-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between space-y-4 sm:space-y-0">
        <div>
          <h1 className="text-3xl font-bold">Context Dashboard</h1>
          <p className="text-muted-foreground mt-1">
            Documentation coverage and health for <span className="font-medium">{selectedProject?.projectName}</span>
          </p>
          {dashboard?.lastUpdated && (
            <p className="text-xs text-muted-foreground mt-1">
              Last updated: {formatDateTime(dashboard.lastUpdated)}
            </p>
          )}
        </div>
        <div className="flex space-x-2">
          <Button variant="outline" asChild>
            <Link to="/context/bulk-import">
              <FileText className="w-4 h-4 mr-2" />
              Bulk Import
            </Link>
          </Button>
          <Button asChild>
            <Link to={`/projects/${projectId}/context/settings`}>
              Settings
            </Link>
          </Button>
        </div>
      </div>

      {/* Key Metrics Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {/* Overall Coverage */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">
              Overall Coverage
            </CardTitle>
            <Database className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline space-x-2">
              <div className="text-2xl font-bold">
                {Math.round(overallCoverage)}%
              </div>
              {dashboard?.trends?.coverageChange && (
                <div className={`flex items-center text-xs ${
                  dashboard.trends.coverageChange > 0 ? 'text-green-600' : 'text-red-600'
                }`}>
                  <TrendingUp className="h-3 w-3 mr-1" />
                  {dashboard.trends.coverageChange > 0 ? '+' : ''}
                  {dashboard.trends.coverageChange}%
                </div>
              )}
            </div>
            <Progress value={overallCoverage} className="mt-2 h-2" />
            <p className="text-xs text-muted-foreground mt-2">
              {totalDocumented} of {totalEntities} entities documented
            </p>
          </CardContent>
        </Card>

        {/* Stale Contexts */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">
              Needs Review
            </CardTitle>
            <AlertTriangle className="h-4 w-4 text-orange-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-orange-500">
              {dashboard?.staleCount || 0}
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              Context marked as stale
            </p>
            {(dashboard?.staleCount || 0) > 0 && (
              <Button
                variant="link"
                size="sm"
                className="mt-2 h-auto p-0 text-orange-600"
                asChild
              >
                <Link to="#stale-section">View all →</Link>
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Critical Gaps */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">
              Critical Gaps
            </CardTitle>
            <XCircle className="h-4 w-4 text-red-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-500">
              {criticalUndocumented.length}
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              High-priority items undocumented
            </p>
            {criticalUndocumented.length > 0 && (
              <Button
                variant="link"
                size="sm"
                className="mt-2 h-auto p-0 text-red-600"
                asChild
              >
                <Link to="#gaps-section">Document now →</Link>
              </Button>
            )}
          </CardContent>
        </Card>

        {/* Top Contributors */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">
              Well Documented
            </CardTitle>
            <CheckCircle2 className="h-4 w-4 text-green-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-500">
              {topDocumented.length}
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              Entities with complete context
            </p>
            {topDocumented.length > 0 && (
              <Button
                variant="link"
                size="sm"
                className="mt-2 h-auto p-0 text-green-600"
                asChild
              >
                <Link to="#top-section">View examples →</Link>
              </Button>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Main Content Tabs */}
      <Tabs defaultValue="coverage" className="space-y-4">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="coverage">Coverage</TabsTrigger>
          <TabsTrigger value="gaps" className="relative">
            Critical Gaps
            {criticalUndocumented.length > 0 && (
              <Badge variant="destructive" className="absolute -top-2 -right-2 h-5 w-5 p-0 text-xs">
                {criticalUndocumented.length}
              </Badge>
            )}
          </TabsTrigger>
          <TabsTrigger value="stale" className="relative">
            Needs Review
            {(dashboard?.staleCount || 0) > 0 && (
              <Badge variant="outline" className="absolute -top-2 -right-2 h-5 w-5 p-0 text-xs border-orange-500 text-orange-600">
                {dashboard?.staleCount}
              </Badge>
            )}
          </TabsTrigger>
          <TabsTrigger value="top">Top Documented</TabsTrigger>
        </TabsList>

        {/* Coverage Tab */}
        <TabsContent value="coverage" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Documentation Coverage by Type</CardTitle>
              <CardDescription>
                Track how well different entity types are documented
              </CardDescription>
            </CardHeader>
            <CardContent>
              {coverage.length === 0 ? (
                <Alert>
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    No entities found in this project. Make sure your database connection is working.
                  </AlertDescription>
                </Alert>
              ) : (
                <div className="space-y-4">
                  {coverage.map((item) => (
                    <div key={item.entityType} className="space-y-2">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          {getEntityIcon(item.entityType)}
                          <span className="font-medium">{getEntityTypeLabel(item.entityType)}</span>
                        </div>
                        <div className="flex items-center gap-4 text-sm">
                          <span className="text-muted-foreground">
                            {item.documented} / {item.total}
                          </span>
                          <span className="font-semibold min-w-[3rem] text-right">
                            {Math.round(item.coveragePercentage || 0)}%
                          </span>
                        </div>
                      </div>
                      <Progress 
                        value={item.coveragePercentage || 0} 
                        className="h-2"
                      />
                      {item.avgCompleteness && (
                        <p className="text-xs text-muted-foreground">
                          Average completeness: {Math.round(item.avgCompleteness)}%
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Critical Gaps Tab */}
        <TabsContent value="gaps" id="gaps-section" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Critical Undocumented Items</CardTitle>
              <CardDescription>
                High-impact entities that need documentation urgently
              </CardDescription>
            </CardHeader>
            <CardContent>
              {criticalUndocumented.length === 0 ? (
                <Alert>
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>
                    Great work! All critical entities are documented.
                  </AlertDescription>
                </Alert>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Reason</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {criticalUndocumented.map((item) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <Badge variant="outline">
                            {item.entityType}
                          </Badge>
                        </TableCell>
                        <TableCell className="font-medium">
                          {item.entityName}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {item.reason}
                        </TableCell>
                        <TableCell>
                          <Badge 
                            variant={
                              item.priority === 'HIGH' ? 'destructive' :
                              item.priority === 'MEDIUM' ? 'secondary' : 'outline'
                            }
                          >
                            {item.priority}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Button
                            size="sm"
                            variant="outline"
                            asChild
                          >
                            <Link to={getEntityRoute(item.entityType, item.entityId, projectId)}>
                              Document
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Stale Contexts Tab */}
        <TabsContent value="stale" id="stale-section" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Stale Documentation</CardTitle>
              <CardDescription>
                Context that needs review due to schema changes
              </CardDescription>
            </CardHeader>
            <CardContent>
              {staleEntities.length === 0 ? (
                <Alert>
                  <CheckCircle2 className="h-4 w-4" />
                  <AlertDescription>
                    All documentation is up to date!
                  </AlertDescription>
                </Alert>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Last Updated</TableHead>
                      <TableHead>Age</TableHead>
                      <TableHead>Schema Changed</TableHead>
                      <TableHead>Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {staleEntities.map((item) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <Badge variant="outline">
                            {item.entityType}
                          </Badge>
                        </TableCell>
                        <TableCell className="font-medium">
                          {item.entityName}
                        </TableCell>
                        <TableCell className="text-sm text-muted-foreground">
                          {formatDate(item.lastContextUpdate)}
                        </TableCell>
                        <TableCell>
                          <Badge 
                            variant={
                              item.daysSinceUpdate > 30 ? 'destructive' :
                              item.daysSinceUpdate > 14 ? 'secondary' : 'outline'
                            }
                          >
                            {item.daysSinceUpdate} days
                          </Badge>
                        </TableCell>
                        <TableCell>
                          {item.schemaChanged ? (
                            <Badge variant="destructive">Yes</Badge>
                          ) : (
                            <Badge variant="outline">No</Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          <Button
                            size="sm"
                            variant="outline"
                            asChild
                          >
                            <Link to={getEntityRoute(item.entityType, item.entityId, projectId)}>
                              Review
                            </Link>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Top Documented Tab */}
        <TabsContent value="top" id="top-section" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Best Documented Entities</CardTitle>
              <CardDescription>
                Examples of well-documented entities in your project
              </CardDescription>
            </CardHeader>
            <CardContent>
              {topDocumented.length === 0 ? (
                <Alert>
                  <AlertDescription>
                    Start documenting entities to see them here!
                  </AlertDescription>
                </Alert>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Type</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Domain</TableHead>
                      <TableHead>Owner</TableHead>
                      <TableHead>Completeness</TableHead>
                      <TableHead>Experts</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {topDocumented.map((item) => (
                      <TableRow key={`${item.entityType}-${item.entityId}`}>
                        <TableCell>
                          <Badge variant="outline">
                            {item.entityType}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Link
                            to={getEntityRoute(item.entityType, item.entityId, projectId)}
                            className="font-medium hover:underline text-primary"
                          >
                            {item.entityName}
                          </Link>
                        </TableCell>
                        <TableCell>
                          <Badge variant="secondary">
                            {item.businessDomain || 'N/A'}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-sm">
                          {item.dataOwner || 'Unassigned'}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Progress 
                              value={item.completenessScore} 
                              className="h-2 w-20" 
                            />
                            <span className="text-sm font-medium">
                              {item.completenessScore}%
                            </span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant="secondary">
                            {item.expertCount} {item.expertCount === 1 ? 'expert' : 'experts'}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Quick Actions */}
      <Card>
        <CardHeader>
          <CardTitle>Quick Actions</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          <Button variant="outline" asChild>
            <Link to={`/projects/${projectId}/tables`}>
              <Database className="w-4 h-4 mr-2" />
              Browse Tables
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to={`/projects/${projectId}/stored-procedures`}>
              <FileCode className="w-4 h-4 mr-2" />
              Browse SPs
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to={`/projects/${projectId}/context/experts`}>
              <Users className="w-4 h-4 mr-2" />
              View Experts
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <Link to={`/projects/${projectId}/context/bulk-import`}>
              <FileText className="w-4 h-4 mr-2" />
              Bulk Import
            </Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
};

// Helper functions
function getEntityIcon(entityType: string) {
  switch (entityType) {
    case 'TABLE':
      return <TableIcon className="w-4 h-4 text-blue-500" />;
    case 'COLUMN':
      return <FileText className="w-4 h-4 text-green-500" />;
    case 'SP':
      return <FileCode className="w-4 h-4 text-purple-500" />;
    default:
      return <Database className="w-4 h-4" />;
  }
}

function getEntityTypeLabel(entityType: string): string {
  switch (entityType) {
    case 'TABLE':
      return 'Tables';
    case 'COLUMN':
      return 'Columns';
    case 'SP':
      return 'Stored Procedures';
    default:
      return entityType;
  }
}

function getEntityRoute(entityType: string, entityId: number, projectId: number): string {
  switch (entityType) {
    case 'TABLE':
      return `/projects/${projectId}/tables/${entityId}`;
    case 'SP':
      return `/projects/${projectId}/stored-procedures/${entityId}`;
    case 'COLUMN':
      return `/projects/${projectId}/columns/${entityId}`;
    default:
      return `/projects/${projectId}`;
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString();
}

function formatDateTime(dateString: string): string {
  return new Date(dateString).toLocaleString();
}