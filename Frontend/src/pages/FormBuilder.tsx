import { useState, useEffect } from "react";
import {
  FormConfig,
  useFormBuilder,
  type TableSchema,
} from "../hooks/useFormBuilder";
import { useProject } from "../hooks/useProject";
import { useApi, useApiDelete, api } from "../hooks/useApi";
import { useAuthorization } from "../hooks/useAuth";
import { useConfirm } from "../hooks/useConfirm";
import { Save, Code, Table, List, Trash2, FolderOpen } from "lucide-react";
import { utcToLocal } from "../lib/utils";
import { toast } from "sonner";
import { Button } from "../components/ui/button";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "../components/ui/tabs";
import { Card } from "../components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../components/ui/select";
import { Input } from "../components/ui/input";
import { Badge } from "../components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "../components/ui/dialog";
import {
  Table as UITable,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../components/ui/table";
import PreviewTab from "../components/formgen/Preview";
import CodeTab from "../components/formgen/Code";
import BuilderTab from "../components/formgen/Builder";

interface SchemaResponse {
  schema: TableSchema;
  isStale: boolean;
  lastSyncTimestamp: string;
  warning: string | null;
}

interface FormConfigListItem {
  id: number;
  projectId: number;
  tableName: string;
  formName: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export default function FormBuilder() {
  const { selectedProject } = useProject();
  const canUpdate = useAuthorization("Forms:Update");
  const canGenerate = useAuthorization("Forms:Generate");
  const canDelete = useAuthorization("Forms:Delete");
  const canRead = useAuthorization("Forms:Read");
  const { confirm } = useConfirm();

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
    setConfig,
  } = useFormBuilder();

  const [activeTab, setActiveTab] = useState<"builder" | "preview" | "code">(
    "builder",
  );
  const [selectedTable, setSelectedTable] = useState<string | number>("");
  const [customFormName, setCustomFormName] = useState("");
  const [isConfigListOpen, setIsConfigListOpen] = useState(false);

  // Fetch tables for the project
  const tablesUrl = selectedProject
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables`
    : "";
  const { data: tablesData } =
    useApi<Array<{ tableId: number; tableName: string; schemaName?: string }>>(
      tablesUrl,
    );

  const availableTables = tablesData || [];

  // Find selected table info (name and schema)
  const selectedTableInfo = availableTables.find(
    (t) => String(t.tableId) === String(selectedTable),
  );
  const selectedTableName = selectedTableInfo?.tableName;
  const selectedSchemaName = selectedTableInfo?.schemaName || "dbo";

  // Fetch schema when table is selected - include schemaName as query param
  const schemaUrl =
    selectedProject && selectedTableName && selectedTable !== "custom"
      ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables/${encodeURIComponent(selectedTableName)}/schema?schemaName=${encodeURIComponent(selectedSchemaName)}`
      : "";
  const { data: schemaData, isLoading: loadingSchema } =
    useApi<SchemaResponse>(schemaUrl);

  // Fetch saved form configs
  const { data: savedConfigs, refetch: refetchConfigs } = useApi<
    FormConfigListItem[]
  >(
    selectedProject ? `/FormBuilder/configs/${selectedProject.projectId}` : "",
    {
      enabled: !!selectedProject && canRead,
    },
  );

  // Delete mutation
  const deleteMutation = useApiDelete<void, { id: number }>(
    `/FormBuilder/:id`,
    {
      successMessage: "Form configuration deleted successfully",
      onSuccess: () => {
        refetchConfigs();
      },
    },
  );

  // Initialize form when table schema is loaded or custom form is created
  useEffect(() => {
    if (
      schemaUrl &&
      schemaData &&
      schemaData.schema &&
      selectedTable &&
      selectedProject &&
      !config &&
      selectedTable !== "custom"
    ) {
      const tableId =
        typeof selectedTable === "string"
          ? Number(selectedTable)
          : selectedTable;
      initializeFromTable(
        tableId,
        selectedProject.projectId,
        schemaData.schema,
      );
      toast.success(
        `Loaded ${schemaData.schema.columns?.length || 0} fields from ${schemaData.schema.tableName || selectedTable}`,
      );
    }
  }, [
    schemaData,
    selectedTable,
    selectedProject,
    config,
    schemaUrl,
    initializeFromTable,
  ]);

  const handleLoadConfig = async (configId: number) => {
    try {
      const loadedConfig = await api.get<FormConfig>(
        `/FormBuilder/load/${configId}`,
      );
      setConfig(loadedConfig);
      setIsConfigListOpen(false);
      toast.success(`Loaded form: ${loadedConfig.formName}`);
    } catch (error: any) {
      toast.error(error.message || "Failed to load form configuration");
    }
  };

  const handleDeleteConfig = async (configItem: FormConfigListItem) => {
    const confirmed = await confirm({
      title: "Delete Form Configuration",
      description: `Are you sure you want to delete "${configItem.title}"? This action cannot be undone.`,
      confirmText: "Delete",
      cancelText: "Cancel",
      variant: "destructive",
    });

    if (confirmed) {
      deleteMutation.mutate({ id: configItem.id });
    }
  };

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
      <Card className="border-b bg-white dark:bg-neutral-900 dark:border-neutral-800 px-6 py-3 transition-colors">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">
              {config ? config.formName : "Form Builder"}
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
            {canRead && (
              <Button
                variant="outline"
                onClick={() => setIsConfigListOpen(true)}
                className="dark:border-neutral-700 dark:text-gray-100 dark:hover:bg-neutral-800"
              >
                <List className="h-4 w-4 mr-1" />
                Saved Forms
                {savedConfigs && savedConfigs.length > 0 && (
                  <Badge variant="secondary" className="ml-2">
                    {savedConfigs.length}
                  </Badge>
                )}
              </Button>
            )}
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
                    <SelectItem
                      key={table.tableId}
                      value={String(table.tableId)}
                    >
                      {table.schemaName
                        ? `${table.schemaName}.${table.tableName}`
                        : table.tableName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              {selectedTable === "custom" && (
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
                      setCustomFormName("");
                      setSelectedTable("");
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
                    setSelectedTable("");
                    setCustomFormName("");
                  }}
                  variant="outline"
                  className="dark:border-neutral-700 dark:text-gray-100 dark:hover:bg-neutral-800"
                >
                  <Table className="h-4 w-4 mr-1" />
                  Change Form
                </Button>

                {canUpdate && (
                  <Button
                    onClick={saveConfig}
                    disabled={isSaving}
                    variant="default"
                    className="dark:bg-blue-600 dark:hover:bg-blue-700 dark:text-white"
                  >
                    <Save className="h-4 w-4" />
                    {isSaving ? "Saving..." : "Save"}
                  </Button>
                )}

                {canGenerate && (
                  <Button
                    onClick={generateCode}
                    disabled={isGenerating}
                    variant="outline"
                    className="dark:border-neutral-700 dark:text-gray-100 dark:hover:bg-neutral-800"
                  >
                    <Code className="h-4 w-4" />
                    {isGenerating ? "Generating..." : "Generate"}
                  </Button>
                )}
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
                setActiveTab(value as "builder" | "preview" | "code")
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
                  {generatedCode && (
                    <CodeTab code={generatedCode} key={activeTab} />
                  )}
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

      {/* Saved Configs Dialog */}
      <Dialog open={isConfigListOpen} onOpenChange={setIsConfigListOpen}>
        <DialogContent className="sm:max-w-[700px] max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Saved Form Configurations</DialogTitle>
            <DialogDescription>
              Load or delete saved form configurations for this project
            </DialogDescription>
          </DialogHeader>

          {savedConfigs && savedConfigs.length > 0 ? (
            <div className="border rounded-lg overflow-hidden">
              <UITable>
                <TableHeader>
                  <TableRow>
                    <TableHead>Form Name</TableHead>
                    <TableHead>Table</TableHead>
                    <TableHead>Title</TableHead>
                    <TableHead>Updated</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {savedConfigs.map((configItem) => (
                    <TableRow key={configItem.id}>
                      <TableCell className="font-medium">
                        {configItem.formName}
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{configItem.tableName}</Badge>
                      </TableCell>
                      <TableCell>{configItem.title}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {utcToLocal(configItem.updatedAt, "PPP")}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleLoadConfig(configItem.id)}
                            title="Load Form"
                          >
                            <FolderOpen className="h-4 w-4" />
                          </Button>
                          {canDelete && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => handleDeleteConfig(configItem)}
                              className="text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                              title="Delete Form"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </UITable>
            </div>
          ) : (
            <div className="text-center py-8 text-muted-foreground">
              No saved form configurations found
            </div>
          )}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsConfigListOpen(false)}
            >
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
