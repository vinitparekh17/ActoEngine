import { useParams, Link } from 'react-router-dom';
import { useProject } from '@/hooks/useProject';
import { useApi } from '@/hooks/useApi';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  ArrowLeft,
  Columns,
  AlertCircle,
  Loader2,
  Key,
  Link as LinkIcon,
  Zap,
  Check,
  X,
  Database,
} from 'lucide-react';
import { ExpertManagement } from '@/components/context/ExpertManagement';
import { ContextEditor } from '@/components/context/ContextEditorPanel';

// Types
interface ColumnMetadata {
  columnId: number;
  columnName: string;
  tableName: string;
  tableId: number;
  schemaName?: string;
  dataType: string;
  maxLength?: number;
  precision?: number;
  scale?: number;
  isNullable: boolean;
  isPrimaryKey: boolean;
  isForeignKey: boolean;
  isIdentity: boolean;
  defaultValue?: string;
  constraints?: string[];
  description?: string;
  foreignKeyReference?: {
    referencedTable: string;
    referencedColumn: string;
  };
}

export default function ColumnDetail() {
  const { projectId, tableId, columnId } = useParams<{
    projectId: string;
    tableId: string;
    columnId: string;
  }>();
  const { selectedProject, hasProject } = useProject();

  // Fetch column metadata
  const {
    data: columnData,
    isLoading,
    error,
  } = useApi<ColumnMetadata>(
    `/DatabaseBrowser/projects/${projectId}/tables/${tableId}/columns/${columnId}`,
    {
      enabled: hasProject && !!projectId && !!tableId && !!columnId,
      staleTime: 60 * 1000,
      retry: 2,
    }
  );

  // Loading state
  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="flex flex-col items-center space-y-4">
          <Loader2 className="h-12 w-12 animate-spin text-primary" />
          <p className="text-muted-foreground">Loading column details...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error || !columnData) {
    return (
      <div className="space-y-6 p-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Failed to load column details: {error?.message || 'Column not found'}
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
          <AlertDescription>Please select a project to view column details.</AlertDescription>
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
            <Link to={`/projects/${projectId}/tables/${tableId}`}>
              <ArrowLeft className="h-4 w-4" />
            </Link>
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <Columns className="h-6 w-6 text-purple-600" />
              <h1 className="text-3xl font-bold">{columnData.columnName}</h1>
              <Badge variant="secondary" className="font-mono">
                {columnData.dataType}
              </Badge>
            </div>
            <div className="flex items-center gap-2 mt-1">
              <Database className="h-4 w-4 text-muted-foreground" />
              <Link
                to={`/projects/${projectId}/tables/${tableId}`}
                className="text-muted-foreground hover:text-foreground transition-colors"
              >
                {columnData.schemaName ? `${columnData.schemaName}.` : ''}
                {columnData.tableName}
              </Link>
              <span className="text-muted-foreground">in</span>
              <span className="font-medium text-muted-foreground">
                {selectedProject?.projectName}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Attribute Badges */}
      <div className="flex flex-wrap gap-2">
        {columnData.isPrimaryKey && (
          <Badge className="bg-yellow-600 hover:bg-yellow-700">
            <Key className="h-3 w-3 mr-1" />
            Primary Key
          </Badge>
        )}
        {columnData.isForeignKey && (
          <Badge className="bg-blue-600 hover:bg-blue-700">
            <LinkIcon className="h-3 w-3 mr-1" />
            Foreign Key
          </Badge>
        )}
        {columnData.isIdentity && (
          <Badge className="bg-purple-600 hover:bg-purple-700">
            <Zap className="h-3 w-3 mr-1" />
            Identity
          </Badge>
        )}
        {columnData.isNullable ? (
          <Badge variant="outline">
            <Check className="h-3 w-3 mr-1" />
            Nullable
          </Badge>
        ) : (
          <Badge variant="outline">
            <X className="h-3 w-3 mr-1" />
            Not Nullable
          </Badge>
        )}
      </div>

      <Tabs defaultValue="properties" className="space-y-4">
        <TabsList>
          <TabsTrigger value="properties">Properties</TabsTrigger>
          <TabsTrigger value="context">Context & Docs</TabsTrigger>
          <TabsTrigger value="experts">Experts</TabsTrigger>
        </TabsList>

        {/* Properties Tab */}
        <TabsContent value="properties" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Column Properties</CardTitle>
              <CardDescription>
                Detailed information about {columnData.columnName}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Data Type</p>
                    <p className="text-lg font-mono">{columnData.dataType}</p>
                  </div>
                  {columnData.maxLength && (
                    <div>
                      <p className="text-sm font-medium text-muted-foreground">Max Length</p>
                      <p className="text-lg font-mono">{columnData.maxLength}</p>
                    </div>
                  )}
                  {columnData.precision !== undefined && (
                    <div>
                      <p className="text-sm font-medium text-muted-foreground">Precision</p>
                      <p className="text-lg font-mono">{columnData.precision}</p>
                    </div>
                  )}
                  {columnData.scale !== undefined && (
                    <div>
                      <p className="text-sm font-medium text-muted-foreground">Scale</p>
                      <p className="text-lg font-mono">{columnData.scale}</p>
                    </div>
                  )}
                  <div>
                    <p className="text-sm font-medium text-muted-foreground">Nullable</p>
                    <p className="text-lg">{columnData.isNullable ? 'Yes' : 'No'}</p>
                  </div>
                  {columnData.defaultValue && (
                    <div>
                      <p className="text-sm font-medium text-muted-foreground">Default Value</p>
                      <p className="text-lg font-mono">{columnData.defaultValue}</p>
                    </div>
                  )}
                </div>

                {columnData.description && (
                  <div>
                    <p className="text-sm font-medium text-muted-foreground mb-2">Description</p>
                    <p className="text-base">{columnData.description}</p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Foreign Key Reference */}
          {columnData.isForeignKey && columnData.foreignKeyReference && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <LinkIcon className="h-5 w-5 text-blue-600" />
                  Foreign Key Reference
                </CardTitle>
                <CardDescription>This column references another table</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex items-center gap-3 p-4 border rounded-lg bg-muted/50">
                  <span className="font-mono text-sm">{columnData.columnName}</span>
                  <span className="text-muted-foreground">→</span>
                  <span className="font-mono text-sm font-medium">
                    {columnData.foreignKeyReference.referencedTable}.
                    {columnData.foreignKeyReference.referencedColumn}
                  </span>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Constraints */}
          {columnData.constraints && columnData.constraints.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Constraints</CardTitle>
                <CardDescription>Applied database constraints</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {columnData.constraints.map((constraint, index) => (
                    <div key={index} className="p-3 border rounded-lg">
                      <p className="font-mono text-sm">{constraint}</p>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* Context Tab */}
        <TabsContent value="context" className="space-y-4">
          <ContextEditor
            projectId={parseInt(projectId!)}
            entityType="COLUMN"
            entityId={parseInt(columnId!)}
            entityName={columnData.columnName}
          />
        </TabsContent>

        {/* Experts Tab */}
        <TabsContent value="experts" className="space-y-4">
          <ExpertManagement
            entityType="COLUMN"
            entityId={parseInt(columnId!)}
            entityName={columnData.columnName}
          />
        </TabsContent>
      </Tabs>
    </div>
  );
}
