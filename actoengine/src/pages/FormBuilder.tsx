import { useState, lazy, useEffect, useRef } from 'react';
import { useFormBuilder, type FormField, type GeneratedCode, type TableSchema } from '../hooks/useFormBuilder';
import { useProject } from '../hooks/useProject';
import { useApi } from '../hooks/useApi';
import { Save, Code, Plus, Trash2, Copy, Table } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '../components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '../components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../components/ui/select';
import { cn } from '../lib/utils';
import { Input } from '../components/ui/input';
import { Badge } from '../components/ui/badge';
import { Switch } from '../components/ui/switch';
import { Label } from '../components/ui/label';
import { ScrollArea } from '../components/ui/scroll-area';

const MonacoEditor = lazy(() => import('@monaco-editor/react'));

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
  const [selectedTable, setSelectedTable] = useState('');
  const [customFormName, setCustomFormName] = useState('');

  // Fetch tables for the project
  const tablesUrl = selectedProject
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables`
    : '';
  const { data: tablesData } = useApi<string[]>(tablesUrl);

  // Fetch schema when table is selected
  const schemaUrl = selectedProject && selectedTable && selectedTable !== 'custom'
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables/${selectedTable}/schema`
    : '';
  const { data: schemaData, isLoading: loadingSchema } = useApi<TableSchema>(schemaUrl);

  const availableTables = tablesData || [];

  // Initialize form when table schema is loaded or custom form is created
  useEffect(() => {
    if (schemaUrl && schemaData && selectedTable && selectedProject && !config && selectedTable !== 'custom') {
      initializeFromTable(selectedTable, selectedProject.projectId, schemaData);
      toast.success(`Loaded ${schemaData.columns.length} fields from ${selectedTable}`);
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
                value={selectedTable}
                onValueChange={(value) => {
                  if (value !== selectedTable) {
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
                    <SelectItem key={table} value={table}>
                      {table}
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

// ============================================
// Builder Tab - Simplified field management with groups
// ============================================
function BuilderTab() {
  const {
    groups,
    selectedGroup,
    selectGroup,
    addGroup,
    deleteGroup,
    selectedField,
    addField,
    updateField,
    deleteField,
    selectField,
  } = useFormBuilder();

  return (
    <div className="flex flex-1 min-h-0 overflow-hidden">
      <div className="w-80 border-r bg-background/60 dark:bg-neutral-950/80 backdrop-blur-sm flex flex-col min-h-0">
        <Card className="flex-1 border-none shadow-md dark:shadow-[0_0_10px_rgba(0,0,0,0.4)] flex flex-col overflow-hidden">
          <CardHeader className="flex flex-row items-center justify-between p-4 pb-2 shrink-0">
            <CardTitle className="text-base font-semibold">Groups & Fields</CardTitle>
            <Button onClick={() => addGroup()} size="sm" variant="default">
              <Plus className="mr-1 h-4 w-4" />
              Add Group
            </Button>
          </CardHeader>

          <CardContent className="flex-1 min-h-0 p-4 pt-0 overflow-hidden">
            <ScrollArea className="h-full pr-2">
              {groups.map((group) => (
                <div key={group.id} className="mb-4">
                  <div
                    onClick={() => selectGroup(group.id)}
                    className={`
                      cursor-pointer rounded-md border p-2 mb-2
                      ${selectedGroup?.id === group.id
                        ? 'bg-neutral-200 dark:bg-neutral-900'
                        : 'bg-white dark:bg-neutral-900'}
                    `}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-medium">{group.title}</span>
                      <Button
                        onClick={(e) => {
                          e.stopPropagation();
                          deleteGroup(group.id);
                        }}
                        variant="ghost"
                        size="icon"
                        className="text-muted-foreground"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                  {group.fields.map((field) => (
                    <div
                      key={field.id}
                      onClick={() => selectField(field.id)}
                      className={`
                        group cursor-pointer rounded-md border p-3 mb-2
                        ${selectedField?.id === field.id
                          ? 'border-neutral-400 bg-neutral-100 dark:border-neutral-600 dark:bg-neutral-800 shadow-sm'
                          : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50 dark:border-neutral-800 dark:bg-neutral-900 dark:hover:border-neutral-700 dark:hover:bg-neutral-800 hover:shadow-sm'
                        }
                      `}
                    >
                      <div className="flex items-start justify-between">
                        <div>
                          <div className="font-medium text-foreground">
                            {field.label || 'Untitled'}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {field.columnName} • {field.inputType}
                          </div>
                        </div>
                        <Button
                          onClick={(e) => {
                            e.stopPropagation();
                            deleteField(field.id);
                          }}
                          variant="ghost"
                          size="icon"
                          className="text-muted-foreground opacity-0 transition-opacity group-hover:opacity-100 hover:text-destructive dark:hover:text-destructive"
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  ))}
                  {selectedGroup?.id === group.id && (
                    <Button
                      onClick={() => addField({})}
                      size="sm"
                      variant="outline"
                      className="mt-2 w-full"
                    >
                      <Plus className="mr-1 h-4 w-4" />
                      Add Field
                    </Button>
                  )}
                </div>
              ))}
              {groups.length === 0 && (
                <div className="rounded-md border-2 border-dashed border-gray-300 dark:border-neutral-800 p-4 text-center text-sm text-muted-foreground">
                  No groups yet. Click “Add Group” to start.
                </div>
              )}
            </ScrollArea>
          </CardContent>
        </Card>
      </div>

      {/* === Properties Panel === */}
      <CardContent className="flex-1 min-h-0 overflow-auto p-6">
        {selectedField ? (
          <FieldProperties field={selectedField} onUpdate={updateField} />
        ) : (
          <div className="flex h-full items-center justify-center text-muted-foreground">
            Select a field to edit its properties
          </div>
        )}
      </CardContent>
    </div>
  );
}


// ============================================
// Field Properties Panel
// ============================================

export function FieldProperties({
  field,
  onUpdate,
}: {
  field: FormField;
  onUpdate: (id: string, updates: any) => void;
}) {
  const { config } = useFormBuilder();
  const { selectedProject } = useProject();
  const [fkTable, setFkTable] = useState(field.referencedTable || '');

  // Sync fkTable with field.referencedTable
  useEffect(() => {
    setFkTable(field.referencedTable || '');
  }, [field.referencedTable]);

  // Fetch available tables for validation
  const tablesUrl = selectedProject
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables`
    : '';
  const { data: availableTables, isLoading: loadingTables } = useApi<string[]>(tablesUrl);

  return (
    <Card className="flex-1 min-h-0 overflow-auto p-6">
      <CardHeader>
        <CardTitle className="text-lg font-semibold">Field Properties</CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        {/* Column Info Badges */}
        <div className="flex flex-wrap gap-2">
          {field.isPrimaryKey && (
            <Badge variant="outline" className="bg-blue-100 text-blue-800">
              Primary Key
            </Badge>
          )}
          {field.isForeignKey && (
            <Badge variant="outline" className="bg-purple-100 text-purple-800">
              Foreign Key
            </Badge>
          )}
          {field.isIdentity && (
            <Badge variant="outline" className="bg-gray-100 text-gray-800">
              Identity
            </Badge>
          )}
        </div>

        {/* Label */}
        <div className="space-y-2">
          <Label>Label</Label>
          <Input
            value={field.label}
            onChange={(e) => onUpdate(field.id, { label: e.target.value })}
            placeholder="Enter field label"
          />
        </div>

        {/* Column Name */}
        <div className="space-y-2">
          <Label>Column Name</Label>
          <Input
            value={field.columnName}
            onChange={(e) => onUpdate(field.id, { columnName: e.target.value })}
            placeholder="Enter column name"
            disabled={!!config?.tableName} // Disable if tied to a table
          />
        </div>

        {/* Input Type */}
        <div className="space-y-2">
          <Label>Input Type</Label>
          <Select
            value={field.inputType}
            onValueChange={(val) => onUpdate(field.id, { inputType: val })}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select input type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="text">Text</SelectItem>
              <SelectItem value="number">Number</SelectItem>
              <SelectItem value="email">Email</SelectItem>
              <SelectItem value="date">Date</SelectItem>
              <SelectItem value="select">Dropdown</SelectItem>
              <SelectItem value="textarea">Text Area</SelectItem>
              <SelectItem value="checkbox">Checkbox</SelectItem>
              <SelectItem value="password">Password</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* Foreign Key Config */}
        {field.isForeignKey && field.inputType === 'select' && (
          <div className="rounded-md border border-purple-200 bg-purple-50 p-4">
            <Label className="mb-2 block text-sm font-medium">
              Foreign Key Reference
            </Label>
            <Input
              placeholder="Enter referenced table name"
              value={fkTable}
              onChange={(e) => setFkTable(e.target.value)}
              onBlur={() => {
                if (fkTable) {
                  // Only validate if tables are loaded
                  if (loadingTables || availableTables === undefined || availableTables === null) {
                    // Optionally show a transient loading state, but do not reject
                    toast.info("Validating table name... (table list is still loading)");
                    return;
                  }
                  if (availableTables.includes(fkTable)) {
                    onUpdate(field.id, {
                      referencedTable: fkTable,
                      options: [{ label: `Select from ${fkTable}`, value: '' }],
                    });
                    toast.success(`Linked to ${fkTable} table`);
                  } else {
                    toast.error(`Table '${fkTable}' not found in project`);
                    setFkTable(field.referencedTable || '');
                  }
                } else {
                  // Clear reference if empty
                  onUpdate(field.id, {
                    referencedTable: undefined,
                    options: [],
                  });
                }
              }}
            />
            <p className="mt-1 text-xs text-gray-600">
              Dropdown will load data from this table
            </p>
            {/* TODO: Implement loading real options from the referenced table */}
          </div>
        )}

        {/* Width */}
        <div className="space-y-2">
          <Label>Width</Label>
          <Select
            value={String(field.colSize)}
            onValueChange={(val) => onUpdate(field.id, { colSize: parseInt(val) })}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select width" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="12">Full Width</SelectItem>
              <SelectItem value="6">Half Width</SelectItem>
              <SelectItem value="4">One Third</SelectItem>
              <SelectItem value="3">One Quarter</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {/* Field Toggles */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <Label htmlFor="required">Required Field</Label>
            <Switch
              id="required"
              checked={field.required}
              onCheckedChange={(checked) =>
                onUpdate(field.id, { required: checked })
              }
            />
          </div>

          <div className="flex items-center justify-between">
            <Label htmlFor="excludeInsert">Exclude from Insert</Label>
            <Switch
              id="excludeInsert"
              checked={!field.includeInInsert}
              onCheckedChange={(checked) =>
                onUpdate(field.id, { includeInInsert: !checked })
              }
            />
          </div>

          <div className="flex items-center justify-between">
            <Label htmlFor="excludeUpdate">Exclude from Update</Label>
            <Switch
              id="excludeUpdate"
              checked={!field.includeInUpdate}
              onCheckedChange={(checked) =>
                onUpdate(field.id, { includeInUpdate: !checked })
              }
            />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================
// Preview Tab - Simple form preview
// ============================================

function PreviewTab() {
  const { config, fields } = useFormBuilder();
  const colSpanMap: Record<number, string> = {
    1: 'col-span-1',
    2: 'col-span-2',
    3: 'col-span-3',
    4: 'col-span-4',
    5: 'col-span-5',
    6: 'col-span-6',
    7: 'col-span-7',
    8: 'col-span-8',
    9: 'col-span-9',
    10: 'col-span-10',
    11: 'col-span-11',
    12: 'col-span-12',
  };
  if (!config) return null;

  return (
    <div className="bg-gray-50 p-6 dark:bg-neutral-900 min-h-[calc(100vh-200px)]">
      <div className="mx-auto max-w-4xl rounded-lg bg-white p-8 shadow">
        <h2 className="mb-6 text-2xl font-bold text-gray-900">{config.title}</h2>

        <form onSubmit={(e) => e.preventDefault()}>
          <div className="grid grid-cols-12 gap-4">
            {fields.map((field) => (
              <div
                key={field.id}
                className={`${colSpanMap[field.colSize] || 'col-span-1'} other-static-classes`}
              >
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {field.label}
                  {field.required && <span className="text-red-500">*</span>}
                </label>

                {field.inputType === 'textarea' ? (
                  <textarea className="w-full rounded border px-3 py-2" rows={3} />
                ) : field.inputType === 'select' ? (
                  <select className="w-full rounded border px-3 py-2 text-gray-900 border-gray-300">
                    <option>Choose...</option>
                  </select>
                ) : field.inputType === 'checkbox' ? (
                  <input type="checkbox" className="mt-1" />
                ) : (
                  <input
                    type={field.inputType}
                    className="w-full rounded border px-3 py-2 text-gray-900 border-gray-300"
                    required={field.required}
                  />
                )}
              </div>
            ))}
          </div>

          <div className="mt-6">
            <button
              type="submit"
              className="rounded bg-blue-600 px-6 py-2 text-white hover:bg-blue-700"
            >
              Submit
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ============================================
// Code Tab - Display generated code
// ============================================

function CodeTab({ code }: { code: GeneratedCode }) {
  const [showHtml, setShowHtml] = useState(true);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [editorHeight, setEditorHeight] = useState(480);

  useEffect(() => {
    const updateHeight = () => {
      if (containerRef.current) {
        const height = containerRef.current.clientHeight;
        if (height > 0) {
          setEditorHeight(height);
        }
      }
    };

    updateHeight();
    window.addEventListener('resize', updateHeight);

    // Small delay to ensure DOM is ready
    const timeout = setTimeout(updateHeight, 100);

    return () => {
      window.removeEventListener('resize', updateHeight);
      clearTimeout(timeout);
    };
  }, []);

  return (
    <div className="flex h-full flex-col">
      <div className="border-b p-2 flex-shrink-0">
        <div className="flex gap-2">
          <Button
            onClick={() => setShowHtml(true)}
            variant={showHtml ? "default" : "outline"}
            size="sm"
            className={cn("text-sm", showHtml && "bg-blue-600 hover:bg-blue-700")}
          >
            HTML
          </Button>
          <Button
            onClick={() => setShowHtml(false)}
            variant={!showHtml ? "default" : "outline"}
            size="sm"
            className={cn("text-sm", !showHtml && "bg-blue-600 hover:bg-blue-700")}
          >
            JavaScript
          </Button>
          <Button
            onClick={() => {
              const codeToCopy = showHtml ? code.html : code.javaScript;
              navigator.clipboard.writeText(codeToCopy).then(() => {
                toast.success("Code copied to clipboard");
              });
            }}
            variant="outline"
            size="sm"
            className="flex items-center gap-1 text-sm"
          >
            <Copy className="h-4 w-4" />
            Copy
          </Button>
        </div>
      </div>

      <div
        ref={containerRef}
        className="flex-1 min-h-0"
      >
        <MonacoEditor
          height={editorHeight}
          language={showHtml ? 'html' : 'javascript'}
          theme='vs-dark'
          value={showHtml ? code.html : code.javaScript}
          options={{
            minimap: { enabled: false },
            fontSize: 13,
            wordWrap: "on",
            scrollBeyondLastLine: false,
            readOnly: true,
          }}
        />
      </div>
    </div>
  );
}