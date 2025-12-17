import { useParams, Link, useNavigate } from "react-router-dom";
import { useProject } from "@/hooks/useProject";
import { useApi } from "@/hooks/useApi";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  ArrowLeft,
  Code2,
  AlertCircle,
  ArrowRight,
  Network,
  FileCode,
} from "lucide-react";
import { GridSkeleton, PageHeaderSkeleton } from "@/components/ui/skeletons";
import { ExpertManagement } from "@/components/context/ExpertManagement";
import { ContextEditor } from "@/components/context/ContextEditorPanel";

// Types
interface ParameterMetadata {
  name: string;
  dataType: string;
  direction: "IN" | "OUT" | "INOUT";
  defaultValue?: string;
  isOptional?: boolean;
}

interface StoredProcedureMetadata {
  storedProcedureId: number;
  procedureName: string;
  schemaName?: string;
  definition?: string;
  parameters?: ParameterMetadata[];
  createdDate?: string;
  modifiedDate?: string;
  description?: string;
}

/**
 * Renders the stored procedure details page for the route's project and procedure IDs.
 *
 * Validates route parameters, fetches stored procedure metadata, and displays appropriate
 * loading, error, and no-project states. When data is available, shows header, stats,
 * parameters table, SQL definition (if present), context editor, and expert management tabs.
 *
 * @returns The React element for the stored procedure details page.
 */
export default function StoredProcedureDetail() {
  const { projectId, procedureId } = useParams<{
    projectId: string;
    procedureId: string;
  }>();
  const { selectedProject, hasProject } = useProject();
  const navigate = useNavigate();

  // Validate and parse route parameters
  if (!projectId || !procedureId) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Invalid route: Missing project ID or procedure ID.
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button asChild>
            <Link to="/projects">Back to Projects</Link>
          </Button>
        </div>
      </div>
    );
  }

  const projectIdNum = parseInt(projectId, 10);
  const procedureIdNum = parseInt(procedureId, 10);

  if (isNaN(projectIdNum) || isNaN(procedureIdNum)) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Invalid route: Project ID or procedure ID must be numeric.
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button asChild>
            <Link to="/projects">Back to Projects</Link>
          </Button>
        </div>
      </div>
    );
  }

  // Fetch stored procedure metadata
  const {
    data: procedureData,
    isLoading,
    error,
  } = useApi<StoredProcedureMetadata>(
    `/DatabaseBrowser/projects/${projectId}/stored-procedures/${procedureId}`,
    {
      enabled: hasProject && !!projectId && !!procedureId,
      staleTime: 60 * 1000,
      retry: 2,
    },
  );

  // Helper functions
  const getParameterIcon = (direction: string) => {
    switch (direction) {
      case "IN":
        return <ArrowRight className="h-3.5 w-3.5 text-blue-600" />;
      case "OUT":
        return <ArrowLeft className="h-3.5 w-3.5 text-green-600" />;
      case "INOUT":
        return (
          <div className="flex items-center">
            <ArrowRight className="h-3.5 w-3.5 text-purple-600" />
            <ArrowLeft className="h-3.5 w-3.5 text-purple-600 -ml-2" />
          </div>
        );
      default:
        return null;
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return "N/A";
    return new Date(dateString).toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  };

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <PageHeaderSkeleton />
        <GridSkeleton count={3} className="grid gap-4 md:grid-cols-3" />
        <div className="h-[200px] w-full bg-muted/10 animate-pulse rounded-lg border border-neutral-200 dark:border-neutral-800" />
      </div>
    );
  }

  // Error state
  if (error || !procedureData) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load stored procedure details:{" "}
            {error?.message || "Procedure not found"}
          </AlertDescription>
        </Alert>
        <div className="flex justify-center">
          <Button onClick={() => window.location.reload()} variant="outline">
            Try Again
          </Button>
        </div>
      </div>
    );
  }

  // No project selected
  if (!hasProject) {
    return (
      <div className="space-y-6 p-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Please select a project to view stored procedure details.
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

  return (
    <div className="space-y-6 p-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link to={`/project/${projectId}/context/experts`}>
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <Code2 className="h-6 w-6 text-indigo-600" />
              <h1 className="text-3xl font-bold">
                {procedureData.procedureName}
              </h1>
              <Badge variant="outline">
                {procedureData.schemaName || "dbo"}
              </Badge>
            </div>
            <p className="text-muted-foreground mt-1">
              Stored Procedure in{" "}
              <span className="font-medium">
                {selectedProject?.projectName}
              </span>
            </p>
          </div>
        </div>
        <Button
          variant="outline"
          onClick={() =>
            navigate(`/project/${projectId}/impact/SP/${procedureId}`)
          }
        >
          <Network className="mr-2 h-4 w-4" />
          View Impact
        </Button>
      </div>

      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        {procedureData.parameters && (
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Parameters</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {procedureData.parameters.length}
              </div>
            </CardContent>
          </Card>
        )}
        {procedureData.createdDate && (
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Created</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-bold">
                {formatDate(procedureData.createdDate)}
              </div>
            </CardContent>
          </Card>
        )}
        {procedureData.modifiedDate && (
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Modified</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-bold">
                {formatDate(procedureData.modifiedDate)}
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      <Tabs defaultValue="overview" className="space-y-4">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          {procedureData.definition && (
            <TabsTrigger value="definition">Definition</TabsTrigger>
          )}
          <TabsTrigger value="context">Context & Docs</TabsTrigger>
          <TabsTrigger value="experts">Experts</TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="space-y-4">
          {procedureData.description && (
            <Card>
              <CardHeader>
                <CardTitle>Description</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-muted-foreground">
                  {procedureData.description}
                </p>
              </CardContent>
            </Card>
          )}

          {/* Parameters */}
          {procedureData.parameters && procedureData.parameters.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Parameters</CardTitle>
                <CardDescription>
                  Input and output parameters for {procedureData.procedureName}
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="border rounded-lg overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Parameter</TableHead>
                        <TableHead>Type</TableHead>
                        <TableHead>Direction</TableHead>
                        <TableHead>Default</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {procedureData.parameters.map((param) => (
                        <TableRow key={param.name}>
                          <TableCell className="font-medium">
                            <div className="flex items-center gap-2">
                              {getParameterIcon(param.direction)}
                              <span>{param.name}</span>
                              {param.isOptional && (
                                <Badge variant="outline" className="text-xs">
                                  Optional
                                </Badge>
                              )}
                            </div>
                          </TableCell>
                          <TableCell className="font-mono text-sm">
                            {param.dataType}
                          </TableCell>
                          <TableCell>
                            <Badge
                              variant={
                                param.direction === "IN"
                                  ? "default"
                                  : param.direction === "OUT"
                                    ? "secondary"
                                    : "outline"
                              }
                            >
                              {param.direction}
                            </Badge>
                          </TableCell>
                          <TableCell className="font-mono text-sm text-muted-foreground">
                            {param.defaultValue || "None"}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Definition Tab */}
        {procedureData.definition && (
          <TabsContent value="definition" className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <FileCode className="h-5 w-5" />
                  SQL Definition
                </CardTitle>
                <CardDescription>
                  The SQL code that defines this stored procedure
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="relative">
                  <pre className="bg-muted p-4 rounded-lg overflow-x-auto">
                    <code className="text-sm font-mono">
                      {procedureData.definition}
                    </code>
                  </pre>
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        )}

        {/* Context Tab */}
        <TabsContent value="context" className="space-y-4">
          <ContextEditor
            projectId={projectIdNum}
            entityType="SP"
            entityId={procedureIdNum}
            entityName={procedureData.procedureName}
          />
        </TabsContent>

        {/* Experts Tab */}
        <TabsContent value="experts" className="space-y-4">
          <ExpertManagement
            entityType="SP"
            entityId={procedureIdNum}
            entityName={procedureData.procedureName}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
