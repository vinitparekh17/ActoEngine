// ============================================
// Builder Tab - field management with groups
// ============================================
import { ScrollArea } from "../ui/scroll-area";
import {
  AlertCircleIcon,
  Download,
  Plus,
  RefreshCw,
  Trash2,
} from "lucide-react";
import { useFormBuilder, type FormField } from "../../hooks/useFormBuilder";
import { Button } from "../ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card";
import { Label } from "../ui/label";
import { Switch } from "../ui/switch";
import { useState, useEffect } from "react";
import { toast } from "sonner";
import { useApi, api } from "../../hooks/useApi";
import { useProject } from "../../hooks/useProject";
import { Input } from "../ui/input";
import { Badge } from "../ui/badge";
import { useQueryClient } from "@tanstack/react-query";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "../ui/select";

export default function BuilderTab() {
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
            <CardTitle className="text-base font-semibold">
              Groups & Fields
            </CardTitle>
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
                    role="button"
                    tabIndex={0}
                    onClick={() => selectGroup(group.id)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        selectGroup(group.id);
                      }
                    }}
                    aria-pressed={selectedGroup?.id === group.id}
                    className={`
                      cursor-pointer rounded-md border p-2 mb-2
                      ${
                        selectedGroup?.id === group.id
                          ? "bg-neutral-200 dark:bg-neutral-900"
                          : "bg-white dark:bg-neutral-900"
                      }
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
                      role="button"
                      tabIndex={0}
                      onClick={() => selectField(field.id)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          selectField(field.id);
                        }
                      }}
                      aria-selected={selectedField?.id === field.id}
                      className={`
                        group cursor-pointer rounded-md border p-3 mb-2
                        ${
                          selectedField?.id === field.id
                            ? "border-neutral-400 bg-neutral-100 dark:border-neutral-600 dark:bg-neutral-800 shadow-sm"
                            : "border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50 dark:border-neutral-800 dark:bg-neutral-900 dark:hover:border-neutral-700 dark:hover:bg-neutral-800 hover:shadow-sm"
                        }
                      `}
                    >
                      <div className="flex items-start justify-between">
                        <div>
                          <div className="font-medium text-foreground">
                            {field.label || "Untitled"}
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
  const [fkTable, setFkTable] = useState(
    field.foreignKeyInfo?.referencedTable || field.referencedTable || "",
  );
  const [fkDisplayColumn, setFkDisplayColumn] = useState(
    field.foreignKeyInfo?.displayColumn || "",
  );
  const queryClient = useQueryClient();

  // Sync fkTable with field.foreignKeyInfo
  useEffect(() => {
    setFkTable(
      field.foreignKeyInfo?.referencedTable || field.referencedTable || "",
    );
    setFkDisplayColumn(field.foreignKeyInfo?.displayColumn || "");
  }, [field.foreignKeyInfo, field.referencedTable]);

  // Fetch available tables for validation
  const tablesUrl = selectedProject
    ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables`
    : "";
  const { data: tablesMetadata, isLoading: loadingTables } =
    useApi<Array<{ tableId: number; tableName: string; schemaName?: string }>>(
      tablesUrl,
    );
  const availableTables = tablesMetadata?.map((t) => t.tableName) || [];

  // Function to fetch column metadata from the /columns endpoint using React Query's fetchQuery
  const loadTableData = async (tableName: string, displayColumn?: string) => {
    if (!selectedProject?.projectId) return;

    try {
      toast.info(`Fetching columns from ${tableName}...`);

      const columnNameList = await queryClient.fetchQuery({
        queryKey: [
          "DatabaseBrowser",
          "projects",
          selectedProject.projectId,
          "tables",
          tableName,
          "data",
        ],
        queryFn: () =>
          api.get<string[]>(
            `/DatabaseBrowser/projects/${selectedProject.projectId}/tables/${tableName}/columns`,
          ),
        staleTime: 5 * 60 * 1000,
      });

      if (
        columnNameList &&
        Array.isArray(columnNameList) &&
        columnNameList.length > 0
      ) {
        const sampleColumns = columnNameList;

        // Set displayColumn to first column if not provided
        const finalDisplayColumn =
          displayColumn ||
          field.foreignKeyInfo?.displayColumn ||
          sampleColumns[0];

        onUpdate(field.id, {
          referencedTable: tableName,
          foreignKeyInfo: {
            ...field.foreignKeyInfo,
            referencedTable: tableName,
            displayColumn: finalDisplayColumn,
            sampleColumns,
          },
          // Clear options since we only have column names, not actual data
          options: [],
        });

        toast.success(
          `Fetched ${sampleColumns.length} columns from ${tableName}`,
        );
      } else {
        toast.warning(`No columns found in ${tableName}`);
        onUpdate(field.id, {
          referencedTable: tableName,
          foreignKeyInfo: {
            ...field.foreignKeyInfo,
            referencedTable: tableName,
            displayColumn:
              displayColumn || field.foreignKeyInfo?.displayColumn || "",
            sampleColumns: [],
          },
          options: [],
        });
      }
    } catch (error) {
      console.error("Error loading table columns:", error);
      toast.error(`Error fetching columns from ${tableName}`);
    }
  };

  return (
    <Card className="flex-1 min-h-0 overflow-auto p-6">
      <CardHeader>
        <CardTitle className="text-lg font-semibold">
          Field Properties
        </CardTitle>
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
              <SelectGroup>
                <SelectLabel>Input Types</SelectLabel>
                <SelectItem value="text">Text</SelectItem>
                <SelectItem value="number">Number</SelectItem>
                <SelectItem value="email">Email</SelectItem>
                <SelectItem value="date">Date</SelectItem>
                <SelectItem value="select">Dropdown</SelectItem>
                <SelectItem value="textarea">Text Area</SelectItem>
                <SelectItem value="checkbox">Checkbox</SelectItem>
                <SelectItem value="password">Password</SelectItem>
              </SelectGroup>
            </SelectContent>
          </Select>
        </div>

        {/* Foreign Key Config */}
        {field.isForeignKey && field.inputType === "select" && (
          <div className="rounded-md border border-purple-200 bg-purple-50 p-4 space-y-3">
            <div className="flex items-center justify-between">
              <Label className="text-sm font-medium">
                Foreign Key Reference
              </Label>
              {field.foreignKeyInfo && (
                <Badge
                  variant="secondary"
                  className="text-xs bg-purple-200 text-purple-900"
                >
                  Auto-detected
                </Badge>
              )}
            </div>

            {field.foreignKeyInfo ? (
              <>
                <div className="space-y-2">
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-600">Referenced Table:</span>
                    <Badge variant="outline" className="bg-white">
                      {field.foreignKeyInfo.referencedTable}
                    </Badge>
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-600">Referenced Column:</span>
                    <Badge variant="outline" className="bg-white">
                      {field.foreignKeyInfo.referencedColumn}
                    </Badge>
                  </div>
                </div>

                {/* Display Column + Auto-Fetch */}
                <div className="space-y-2">
                  <Label htmlFor="displayColumn">Display Column</Label>

                  {/* Once FK data is fetched, show dropdown selector */}
                  {field.foreignKeyInfo?.sampleColumns?.length ? (
                    <div className="flex items-center gap-2">
                      <Select
                        value={fkDisplayColumn}
                        onValueChange={(col) => {
                          setFkDisplayColumn(col);
                          loadTableData(
                            field.foreignKeyInfo!.referencedTable,
                            col,
                          );
                        }}
                      >
                        <SelectTrigger className="flex-1">
                          <SelectValue placeholder="Select display column" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectGroup>
                            <SelectLabel>Columns</SelectLabel>
                            {field.foreignKeyInfo.sampleColumns.map((col) => (
                              <SelectItem key={col} value={col}>
                                {col}
                              </SelectItem>
                            ))}
                          </SelectGroup>
                        </SelectContent>
                      </Select>

                      <Button
                        onClick={() =>
                          loadTableData(
                            field.foreignKeyInfo!.referencedTable,
                            fkDisplayColumn || undefined,
                          )
                        }
                        size="icon"
                        variant="ghost"
                        title="Refresh options"
                      >
                        <RefreshCw className="h-4 w-4" />
                      </Button>
                    </div>
                  ) : (
                    <>
                      {/* If no sample columns yet, show input + fetch button */}
                      <div className="flex items-center gap-2">
                        <Input
                          id="displayColumn"
                          placeholder="e.g., Name, Title, Description"
                          value={fkDisplayColumn}
                          onChange={(e) => setFkDisplayColumn(e.target.value)}
                        />
                        <Button
                          onClick={() =>
                            loadTableData(
                              field.foreignKeyInfo!.referencedTable,
                              fkDisplayColumn || undefined,
                            )
                          }
                          size="icon"
                          variant="ghost"
                          title="Fetch columns"
                        >
                          <Download className="h-4 w-4" />
                        </Button>
                      </div>
                      <p className="text-xs text-gray-600">
                        Fetch columns to select a display label for dropdown
                      </p>
                    </>
                  )}
                </div>

                {field.foreignKeyInfo?.sampleColumns &&
                  field.foreignKeyInfo.sampleColumns.length > 0 && (
                    <p className="text-xs text-green-600">
                      ✓ {field.foreignKeyInfo.sampleColumns.length} columns
                      loaded
                      {field.foreignKeyInfo.displayColumn &&
                        ` (display: ${field.foreignKeyInfo.displayColumn})`}
                    </p>
                  )}
              </>
            ) : (
              <>
                <p className="text-xs text-amber-600 mb-2 flex items-center">
                  <AlertCircleIcon className="inline-block mr-1 h-4 w-4 align-middle" />{" "}
                  No foreign key metadata found. Manual configuration required.
                </p>
                <Input
                  placeholder="Enter referenced table name"
                  value={fkTable}
                  onChange={(e) => setFkTable(e.target.value)}
                  onBlur={() => {
                    const trimmed = fkTable.trim();
                    if (!trimmed) {
                      // Clear reference if empty
                      onUpdate(field.id, {
                        referencedTable: undefined,
                        foreignKeyInfo: undefined,
                        options: [],
                      });
                      return;
                    }

                    // Only validate if tables are loaded
                    if (
                      loadingTables ||
                      availableTables === undefined ||
                      availableTables === null
                    ) {
                      toast.info(
                        "Validating table name... (table list is still loading)",
                      );
                      return;
                    }

                    if (availableTables.includes(trimmed)) {
                      // Load table data using React Query
                      loadTableData(trimmed);
                    } else {
                      // Invalid table - clear both input and field state
                      toast.error(`Table '${trimmed}' not found in project`);
                      setFkTable("");
                      onUpdate(field.id, {
                        referencedTable: undefined,
                        foreignKeyInfo: undefined,
                        options: [],
                      });
                    }
                  }}
                />
                <p className="text-xs text-gray-600">
                  Dropdown will load data from this table
                </p>
              </>
            )}
          </div>
        )}

        {/* Width */}
        <div className="space-y-2">
          <Label>Width</Label>
          <Select
            value={String(field.colSize)}
            onValueChange={(val) =>
              onUpdate(field.id, { colSize: parseInt(val) })
            }
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
