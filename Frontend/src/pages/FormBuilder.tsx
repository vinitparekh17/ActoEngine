                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            import { useState, useEffect } from 'react';
import { useFormBuilder, type TableSchema } from '../hooks/useFormBuilder';
import { useProject } from '../hooks/useProject';
import { useApi } from '../hooks/useApi';
import { Save, Code, Table } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '../components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import {
  Card,
} from '../components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,                                                                                        
} from '../components/ui/select';
import { Input } from '../components/ui/input';
import { Badge } from '../components/ui/badge';
import PreviewTab from '../components/formgen/Preview';
import CodeTab from '../components/formgen/Code';
import BuilderTab from '../components/formgen/Builder';


export default function FormBuilder() {
  const { selectedProject } = useProject();
  const {
    config,
    fields,
    generatedCode,
    isSaving,
    isGenerating,
    initializeFromTable,
    initializeCustomForm,
    saveConfig,
    generateCode,
    reset,
  } = useFormBuilder();

  const [activeTab, setActiveTab] = useState<
    'builder' | 'preview' | 'code'
  >('builder');
  const [selectedTable, setSelectedTable] = useState<string | number>('');
  const [customFormName, setCustomFormName] = useState('');

  // Fetch tables for the project
  const tablesUrl = selectedProject
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables`
    : '';
  const { data: tablesData } = useApi<Array<{ tableId: number; tableName: string; schemaName?: string }>>(tablesUrl);

  // Fetch schema when table is selected
  const schemaUrl = selectedProject && selectedTable && selectedTable !== 'custom'
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables/${selectedTable}/schema`
    : '';
  const { data: schemaData, isLoading: loadingSchema } = useApi<TableSchema>(schemaUrl);

  const availableTables = tablesData || [];

  // Initialize form when table schema is loaded or custom form is created
  useEffect(() => {
    if (schemaUrl && schemaData && selectedTable && selectedProject && !config && selectedTable !== 'custom') {
      const tableId = typeof selectedTable === 'string' ? Number(selectedTable) : selectedTable;
      initializeFromTable(tableId, selectedProject.projectId, schemaData);
      toast.success(`Loaded ${schemaData.columns.length} fields from ${schemaData.tableName || selectedTable}`);
    }
  }, [schemaData, selectedTable, selectedProject, config, schemaUrl, initializeFromTable]);

  if (!selectedProject) {
    return (
      <div className="flex h-screen items-center justify-center">
        <p className="text-gray-500">Please select a project first</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col">
      {/* Header */}
      <Card className='border-b bg-white dark:bg-neutral-900 dark:border-neutral-800 px-6 py-3 transition-colors'>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">
              {config ? config.formName : 'Form Builder'}
            </h1>
            {config && (
              <p className="text-sm text-gray-600 dark:text-gray-400">
                {config.tableName ? (
                  <>
                    Table: {config.tableName} • {fields.length} fields
                    <Badge
                      variant="outline"
                      className="ml-2 border-gray-300 text-gray-700 dark:border-neutral-700 dark:text-gray-300"
                    >
                      Table-Based
                    </Badge>
                  </>
                ) : (
                  <>
                    Custom Form • {fields.length} fields
                    <Badge
                      variant="outline"
                      className="ml-2 border-gray-300 text-gray-700 dark:border-neutral-700 dark:text-gray-300"
                    >
                      Custom
                    </Badge>
                  </>
                )}
              </p>
            )}
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <Select
                value={String(selectedTable)}
                onValueChange={(value) => {
                  if (value !== String(selectedTable)) {
                    reset();
                  }
                  setSelectedTable(value);
                }}
                disabled={loadingSchema}
              >
                <SelectTrigger className="w-48 dark:bg-neutral-800 dark:border-neutral-700 dark:text-gray-100">
                  <SelectValue placeholder="Select table or custom..." />
                </SelectTrigger>
                <SelectContent className="dark:bg-neutral-900 dark:border-neutral-700">
                  <SelectItem value="custom">Create Custom Form</SelectItem>
                  {availableTables.map((table) => (
                    <SelectItem key={table.tableId} value={String(table.tableId)}>
                      {table.schemaName ? `${table.schemaName}.${table.tableName}` : table.tableName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              {selectedTable === 'custom' && (
                <Input
                  className="w-48 dark:bg-neutral-800 dark:border-neutral-700 dark:text-gray-100"
                  placeholder="Enter form name"
                  value={customFormName}
                  onChange={(e) => setCustomFormName(e.target.value)}
                  onBlur={(e) => {
                    const trimmed = e.target.value.trim();
                    setCustomFormName(trimmed);
                    if (trimmed && selectedProject && !config) {
                      initializeCustomForm(trimmed, selectedProject.projectId);
                      toast.success(`Created custom form: ${trimmed}`);
                      setCustomFormName('');
                      setSelectedTable('');
                    }
                  }}
                />
              )}
            </div>

            {config && (
              <>
                <Button
                  onClick={() => {
                    reset();
                    setSelectedTable('');
                    setCustomFormName('');
                  }}
                  variant="outline"
                  className="dark:border-neutral-700 dark:text-gray-100 dark:hover:bg-neutral-800"
                >
                  <Table className="h-4 w-4 mr-1" />
                  Change Form
                </Button>

                <Button
                  onClick={saveConfig}
                  disabled={isSaving}
                  variant="default"
                  className="dark:bg-blue-600 dark:hover:bg-blue-700 dark:text-white"
                >
                  <Save className="h-4 w-4" />
                  {isSaving ? 'Saving...' : 'Save'}
                </Button>

                <Button
                  onClick={generateCode}
                  disabled={isGenerating}
                  variant="outline"
                  className="dark:border-neutral-700 dark:text-gray-100 dark:hover:bg-neutral-800"
                >
                  <Code className="h-4 w-4" />
                  {isGenerating ? 'Generating...' : 'Generate'}
                </Button>
              </>
            )}
          </div>
        </div>


        {/* Content */}
        <div className="flex-1 overflow-hidden">
          {config ? (
            <Tabs
              value={activeTab}
              onValueChange={(value) =>
                setActiveTab(value as 'builder' | 'preview' | 'code')
              }
              className="h-full"
            >
              <div className="border-b px-6 py-2">
                <TabsList>
                  <TabsTrigger value="builder">Builder</TabsTrigger>
                  <TabsTrigger value="preview">Preview</TabsTrigger>
                  <TabsTrigger value="code" disabled={!generatedCode?.success}>
                    Code
                  </TabsTrigger>
                </TabsList>
              </div>
              <TabsContent value="builder" className="m-0 p-0">
                <div className="h-[calc(100vh-260px)] flex flex-col">
                  <BuilderTab />
                </div>
              </TabsContent>

              <TabsContent value="preview" className="m-0 p-0">
                <div className="isolate bg-white text-gray-900 dark:[&]:bg-white dark:[&]:text-gray-900">
                  <PreviewTab />
                </div>
              </TabsContent>
              <TabsContent value="code" className="m-0 p-0">
                <div className="h-[calc(100vh-260px)] flex flex-col">
                  {generatedCode && <CodeTab code={generatedCode} key={activeTab} />}
                </div>
              </TabsContent>
            </Tabs>
          ) : (
            <div className="flex h-screen items-center justify-center text-muted-foreground">
              Select a table or create a custom form to start building
            </div>
          )}
        </div>
      </Card>
    </div>
  );
}