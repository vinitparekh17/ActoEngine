// ============================================
// Builder Tab - Simplified field management with groups
// ============================================
import { ScrollArea } from "@radix-ui/react-scroll-area";
import { Plus, Trash2 } from "lucide-react";
import { useFormBuilder, type FormField } from "../../hooks/useFormBuilder";
import { Button } from "../ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "../ui/card";
import { Label } from "../ui/label";
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from "@radix-ui/react-select";
import { Switch } from "@radix-ui/react-switch";
import { useState, useEffect } from "react";
import { toast } from "sonner";
import { useApi, api } from "../../hooks/useApi";
import { useProject } from "../../hooks/useProject";
import { Input } from "../ui/input";
import { Badge } from "../ui/badge";
import { useQueryClient } from "@tanstack/react-query";

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
    const queryClient = useQueryClient();

    // Sync fkTable with field.referencedTable
    useEffect(() => {
        setFkTable(field.referencedTable || '');
    }, [field.referencedTable]);

    // Fetch available tables for validation
    const tablesUrl = selectedProject
        ? `/DatabaseBrowser/projects/${selectedProject.projectId}/tables`
        : '';
    const { data: availableTables, isLoading: loadingTables } = useApi<string[]>(tablesUrl);

    // Function to load table data using React Query's fetchQuery
    const loadTableData = async (tableName: string) => {
        if (!selectedProject?.projectId) return;

        try {
            toast.info(`Loading options from ${tableName}...`);

            // Use queryClient.fetchQuery to leverage React Query with the API client
            const tableData = await queryClient.fetchQuery({
                queryKey: ['DatabaseBrowser', 'projects', selectedProject.projectId, 'tables', tableName, 'data'],
                queryFn: () => api.get<Record<string, any>[]>(
                    `/DatabaseBrowser/projects/${selectedProject.projectId}/tables/${tableName}/data?limit=50`
                ),
                staleTime: 5 * 60 * 1000, // Cache for 5 minutes
            });

            // Try to find ID and display columns
            const firstRow = tableData[0];
            if (firstRow && Object.keys(firstRow).length > 0) {
                const columns = Object.keys(firstRow);
                // Use first column as value (usually ID), second or first as label
                const valueCol = columns[0];
                const labelCol = columns.length > 1 ? columns[1] : columns[0];

                const options = tableData.map((row: any) => ({
                    value: String(row[valueCol] || ''),
                    label: String(row[labelCol] || row[valueCol] || 'Unknown'),
                }));

                onUpdate(field.id, {
                    referencedTable: tableName,
                    options: [{ label: `Select ${tableName}`, value: '' }, ...options],
                });
                toast.success(`Loaded ${options.length} options from ${tableName}`);
            } else {
                // No data in table
                onUpdate(field.id, {
                    referencedTable: tableName,
                    options: [{ label: `No data in ${tableName}`, value: '' }],
                });
                toast.warning(`Table ${tableName} is empty`);
            }
        } catch (error) {
            console.error('Error loading table data:', error);
            toast.error(`Error loading data from ${tableName}`);
            onUpdate(field.id, {
                referencedTable: tableName,
                options: [{ label: `Select from ${tableName}`, value: '' }],
            });
        }
    };

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
                                        toast.info("Validating table name... (table list is still loading)");
                                        return;
                                    }
                                    if (availableTables.includes(fkTable)) {
                                        // Load table data using React Query
                                        loadTableData(fkTable);
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
                        {field.options && field.options.length > 1 && (
                            <p className="mt-1 text-xs text-green-600">
                                ✓ {field.options.length - 1} options loaded
                            </p>
                        )}
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