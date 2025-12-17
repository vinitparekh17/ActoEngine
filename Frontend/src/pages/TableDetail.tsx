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
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
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
  Database,
  AlertCircle,
  Loader2,
  Key,
  Link as LinkIcon,
  Zap,
  Check,
  X,
  Network,
} from "lucide-react";
import { PageHeaderSkeleton, GridSkeleton } from "@/components/ui/skeletons";
import { ExpertManagement } from "@/components/context/ExpertManagement";
import { ContextEditor } from "@/components/context/ContextEditorPanel";

interface ColumnMetadata {
  columnId?: number;
  name: string;
  dataType: string;
  constraints?: string[];
  isNullable?: boolean;
  defaultValue?: string;
}

interface TableMetadata {
  tableId: number;
  tableName: string;
  schemaName?: string;
  rowCount?: number;
  columns: ColumnMetadata[];
  primaryKeys?: string[];
  foreignKeys?: Array<{
    columnName: string;
    referencedTable: string;
    referencedColumn: string;
  }>;
  indexes?: Array<{
    indexName: string;
    columns: string[];
    isUnique: boolean;
  }>;
}

/**
 * Render a detailed view for a database table identified by the current route's projectId and tableId.
 *
 * Validates that route IDs are present and numeric, fetches table metadata, and displays loading, error, and
 * no-project states. When data is available, presents table schema (columns, foreign keys, indexes), context editor,
 * and expert management interfaces along with navigation and summary cards.
 *
 * @returns A JSX element that displays the table details UI, or an appropriate alert UI for validation, loading, or error states.
 */
export default function TableDetail() {
  const { projectId, tableId } = useParams<{
    projectId: string;
    tableId: string;
  }>();
  const { selectedProject, hasProject } = useProject();
  const navigate = useNavigate();

  if (!projectId || !tableId) {
    return (
      <div className="p-4">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Invalid Route</AlertTitle>
          <AlertDescription>
            Missing required project or table ID.
          </AlertDescription>
        </Alert>
        <Button className="mt-4" onClick={() => navigate(-1)}>
          Go Back
        </Button>
      </div>
    );
  }

  // Validate that IDs are numeric
  const numericProjectId = parseInt(projectId, 10);
  const numericTableId = parseInt(tableId, 10);

  if (isNaN(numericProjectId) || isNaN(numericTableId)) {
    return (
      <div className="p-4">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Invalid Route</AlertTitle>
          <AlertDescription>
            Project ID and Table ID must be numeric.
          </AlertDescription>
        </Alert>
        <Button className="mt-4" onClick={() => navigate(-1)}>
          Go Back
        </Button>
      </div>
    );
  }

  const {
    data: tableData,
    isLoading,
    error,
  } = useApi<TableMetadata>(
    `/DatabaseBrowser/projects/${projectId}/tables/${tableId}`,
    {
      enabled: hasProject && !!projectId && !!tableId,
      staleTime: 60 * 1000,
      retry: 2,
    },
  );

  const renderIcons = (constraints: string[] = []) => {
    const upper = constraints.map((s) => s.toUpperCase());
    return (
      <div className="inline-flex items-center gap-1">
        {upper.some((s) => s.includes("PK")) && (
          <Key
            className="h-3.5 w-3.5 text-yellow-600"
            aria-label="Primary key"
          />
        )}
        {upper.some((s) => s.includes("FK")) && (
          <LinkIcon
            className="h-3.5 w-3.5 text-blue-600"
            aria-label="Foreign key"
          />
        )}
        {upper.some((s) => s.includes("IDENTITY")) && (
          <Zap className="h-3.5 w-3.5 text-purple-600" aria-label="Identity" />
        )}
      </div>
    );
  };

  const isNullable = (constraints: string[] = []) =>
    !constraints.some((s) => s.toUpperCase().includes("NOT NULL"));

  const defaultValue = (constraints: string[] = []) => {
    const d = constraints.find((s) => s.toUpperCase().startsWith("DEFAULT"));
    return d ? d.replace(/^DEFAULT\s*/i, "") : "NULL";
  };

  // Loading state always short-circuits first
  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <PageHeaderSkeleton />
        <GridSkeleton count={2} className="grid gap-4 md:grid-cols-3" />
        <div className="h-[400px] w-full bg-muted/10 animate-pulse rounded-lg border border-neutral-200 dark:border-neutral-800" />
      </div>
    );
  }

  // No project selected (moved before error/tableData check)
  if (!hasProject) {
    return (
      <div className="space-y-6 p-6">
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Please select a project to view table details.
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

  // Error or missing data
  if (error || !tableData) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load table details: {error?.message || "Table not found"}
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

  // Main UI
  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" asChild>
            <Link to={`/project/${projectId}/context/experts`}>
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <Database className="h-6 w-6 text-green-600" />
              <h1 className="text-3xl font-bold">{tableData.tableName}</h1>
              <Badge variant="outline">{tableData.schemaName || "dbo"}</Badge>
            </div>
            <p className="text-muted-foreground mt-1">
              Table in{" "}
              <span className="font-medium">
                {selectedProject?.projectName}
              </span>
            </p>
          </div>
        </div>
        <Button
          variant="outline"
          onClick={() =>
            navigate(`/project/${projectId}/impact/TABLE/${tableId}`)
          }
        >
          <Network className="mr-2 h-4 w-4" />
          View Impact
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Columns</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{tableData.columns.length}</div>
          </CardContent>
        </Card>

        {tableData.primaryKeys && tableData.primaryKeys.length > 0 && (
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">
                Primary Keys
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">
                {tableData.primaryKeys.length}
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      <Tabs defaultValue="schema" className="space-y-4">
        <TabsList>
          <TabsTrigger value="schema">Schema</TabsTrigger>
          <TabsTrigger value="context">Context & Docs</TabsTrigger>
          <TabsTrigger value="experts">Experts</TabsTrigger>
        </TabsList>

        <TabsContent value="schema" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Columns</CardTitle>
              <CardDescription>
                Column definitions and constraints for {tableData.tableName}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="border rounded-lg overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Column</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Nullable</TableHead>
                      <TableHead>Default</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {tableData.columns.map((col) => (
                      <TableRow key={col.name}>
                        <TableCell className="font-medium">
                          <div className="flex items-center gap-2">
                            {renderIcons(col.constraints)}
                            <span>{col.name}</span>
                          </div>
                        </TableCell>
                        <TableCell className="font-mono text-sm">
                          {col.dataType}
                        </TableCell>
                        <TableCell>
                          {isNullable(col.constraints || []) ? (
                            <span className="inline-flex items-center gap-1 text-muted-foreground">
                              <Check className="h-3.5 w-3.5" /> Yes
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 text-muted-foreground">
                              <X className="h-3.5 w-3.5" /> No
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="font-mono text-sm text-muted-foreground">
                          {defaultValue(col.constraints || [])}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>

          {tableData.foreignKeys && tableData.foreignKeys.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Foreign Keys</CardTitle>
                <CardDescription>References to other tables</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {tableData.foreignKeys.map((fk) => (
                    <div
                      key={`${fk.columnName}-${fk.referencedTable}-${fk.referencedColumn}`}
                      className="flex items-center gap-2 p-3 border rounded-lg"
                    >
                      <LinkIcon className="h-4 w-4 text-blue-600" />
                      <span className="font-mono text-sm">{fk.columnName}</span>
                      <span className="text-muted-foreground">→</span>
                      <span className="font-mono text-sm">
                        {fk.referencedTable}.{fk.referencedColumn}
                      </span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}

          {tableData.indexes && tableData.indexes.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Indexes</CardTitle>
                <CardDescription>
                  Database indexes for performance
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {tableData.indexes.map((index, i) => (
                    <div
                      key={index.indexName || `index-${i}`}
                      className="flex items-center gap-2 p-3 border rounded-lg"
                    >
                      <span className="font-medium">{index.indexName}</span>
                      {index.isUnique && (
                        <Badge variant="secondary" className="text-xs">
                          Unique
                        </Badge>
                      )}
                      <span className="text-muted-foreground text-sm">
                        on ({index.columns.join(", ")})
                      </span>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="context" className="space-y-4">
          <ContextEditor
            projectId={numericProjectId}
            entityType="TABLE"
            entityId={numericTableId}
            entityName={tableData.tableName}
          />
        </TabsContent>

        <TabsContent value="experts" className="space-y-4">
          <ExpertManagement
            entityType="TABLE"
            entityId={parseInt(tableId)}
            entityName={tableData.tableName}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
