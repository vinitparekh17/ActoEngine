"use client"

import { useCallback, useMemo, useState } from "react"
import { useToast } from "../hooks/useToast"
import type { SPType } from "../components/spgen/SPTypeCard"
import { ChevronRight, ChevronLeft, Maximize2, Minimize2 } from "lucide-react"
import CodeExportButton from "../components/spgen/CodeExportButton"
import SPConfigPanel, { type SPConfigValues } from "../components/spgen/SPConfigPanel"
import SPPreviewPane from "../components/spgen/SPPreviewPanel"
// import SPTypeCard from "../components/codegen/SPTypeCard"
import { type TableSchema } from "../components/database/TableSchemaViewer"
import TreeView from "../components/database/TreeView"
import { Card } from "../components/ui/card"
import { ResizablePanelGroup, ResizablePanel, ResizableHandle } from "../components/ui/resizable"
import { cn } from "../lib/utils"
import { useProject, useProjectTables, useTableSchema } from "../hooks/useProject"
import { useApiMutation } from "../hooks/useApi"

type TreeNode = {
    id: string
    name: string
    children?: TreeNode[]
    type?: "database" | "table" | "column" | "index" | "stored-procedure" | "scalar-function" | "table-function" | "tables-folder" | "programmability-folder" | "stored-procedures-folder" | "functions-folder"
}

export default function AppShell() {
    const { showToast: toast } = useToast()
    const [selectedTable, setSelectedTable] = useState<string | null>(null)
    const [spType, setSpType] = useState<SPType>("CUD")
    const [sqlCode, setSqlCode] = useState<string>("-- Generated SQL will appear here")
    const [isGenerating, setIsGenerating] = useState(false)
    const [treeSearch, setTreeSearch] = useState("")
    const [isLeftPanelCollapsed, setIsLeftPanelCollapsed] = useState(false)
    const [isFullscreen, setIsFullscreen] = useState(false)

    // Project and table hooks
    const { selectedProject } = useProject()
    const { tables, isLoading: isLoadingTables } = useProjectTables()
    const { schema: tableSchema, isLoading: isLoadingSchema, error: schemaError } = useTableSchema(selectedTable || undefined)

    // API mutation for code generation
    const generateMutation = useApiMutation(
        '/CodeGen/generate',
        'POST',
        {
            onSuccess: (result: any) => {
                const generatedCode = result.storedProcedure?.code || "-- No code generated"
                setSqlCode(generatedCode)
                toast({ title: "Success", description: `Generated ${spType} stored procedure` })
            },
            onError: (error) => {
                setSqlCode(`-- Error generating SQL: ${error.message}`)
                toast({ title: "Error", description: "Failed to generate stored procedure" })
            },
        }
    )

    // Default config values by mode
    const [config, setConfig] = useState<SPConfigValues>({
        mode: "CUD",
        generateCreate: true,
        generateUpdate: true,
        generateDelete: true,
        spPrefix: "usp",
        includeErrorHandling: true,
        includeTransaction: true,
        actionParamName: "Action",
    })

    const treeData = useMemo<TreeNode[]>(
        () => {
            if (!selectedProject || !tables.length) {
                return []
            }

            return [
                {
                    id: `db-${selectedProject.projectId}`,
                    name: selectedProject.databaseName || selectedProject.projectName,
                    type: "database",
                    children: [
                        {
                            id: `db-${selectedProject.projectId}-tables`,
                            name: "Tables",
                            type: "tables-folder",
                            children: tables.map((tableName, index) => ({
                                id: `table-${index}`,
                                name: tableName,
                                type: "table" as const,
                                children: [] // Could expand to show columns later
                            }))
                        }
                    ]
                }
            ]
        },
        [selectedProject, tables],
    )

    const schema = useMemo<TableSchema>(() => {
        if (!tableSchema || !selectedTable) {
            return {
                tableName: selectedTable || "",
                schemaName: "",
                columns: []
            }
        }

        return {
            tableName: tableSchema.tableName,
            schemaName: tableSchema.schemaName,
            columns: tableSchema.columns.map(col => {
                // Format data type with appropriate length/precision info
                let dataType = col.dataType;
                if (col.maxLength && col.maxLength > 0 && col.maxLength !== -1) {
                    dataType += `(${col.maxLength})`;
                } else if (col.precision && col.scale !== undefined) {
                    dataType += `(${col.precision},${col.scale})`;
                } else if (col.precision && col.scale === 0) {
                    dataType += `(${col.precision})`;
                }

                return {
                    name: col.columnName,
                    dataType,
                    constraints: [
                        col.isPrimaryKey ? "PK" : "",
                        !col.isNullable ? "NOT NULL" : "",
                        col.isIdentity ? "IDENTITY" : "",
                        col.isForeignKey ? "FK" : "",
                        col.defaultValue ? `DEFAULT ${col.defaultValue}` : "",
                    ].filter(Boolean)
                };
            })
        }
    }, [tableSchema, selectedTable])

    const handleTreeSelect = useCallback((node: TreeNode) => {
        switch (node.type) {
            case "table":
                setSelectedTable(node.name)
                // Optionally: fetch table details, columns, etc.
                break

            case "stored-procedure":
                // Handle SP selection
                console.log("Selected SP:", node.name)
                break

            case "scalar-function":
            case "table-function":
                // Handle function selection
                console.log("Selected function:", node.name)
                break

            case "column":
                // Handle column selection
                console.log("Selected column:", node.name)
                break

            case "index":
                // Handle index selection
                console.log("Selected index:", node.name)
                break

            // Folder nodes - you might want to ignore these or handle differently
            case "database":
            case "tables-folder":
            case "programmability-folder":
            case "stored-procedures-folder":
            case "functions-folder":
                // Do nothing or toggle folder
                break
        }
    }, [])

    const handleConfigSubmit = useCallback(
        (values: SPConfigValues) => {
            if (!selectedProject || !selectedTable || !tableSchema) {
                toast({ title: "Error", description: "Please select a project and table first" })
                return
            }

            setIsGenerating(true)

            // Convert frontend config to backend format
            const requestData = {
                projectId: selectedProject.projectId,
                tableName: selectedTable,
                type: values.mode === "CUD" ? "Cud" : "Select",
                columns: tableSchema.columns.map(col => ({
                    columnName: col.columnName,
                    dataType: col.dataType,
                    maxLength: col.maxLength,
                    isNullable: col.isNullable,
                    isPrimaryKey: col.isPrimaryKey,
                    isIdentity: col.isIdentity,
                    includeInCreate: values.mode === "CUD" ? (values as any).generateCreate ?? true : true,
                    includeInUpdate: values.mode === "CUD" ? (values as any).generateUpdate ?? true : true,
                    defaultValue: col.defaultValue || "",
                })),
                cudOptions: values.mode === "CUD" ? {
                    spPrefix: (values as any).spPrefix || "usp",
                    includeErrorHandling: (values as any).includeErrorHandling ?? true,
                    includeTransaction: (values as any).includeTransaction ?? true,
                    actionParamName: (values as any).actionParamName || "Action",
                } : undefined,
                selectOptions: values.mode === "SELECT" ? {
                    spPrefix: "usp",
                    filters: (values as any).filters?.map((f: any) => ({
                        columnName: f.column,
                        operator: f.operator === "=" ? "Equals" :
                            f.operator === "LIKE" ? "Like" :
                                f.operator === ">" ? "GreaterThan" :
                                    f.operator === "<" ? "LessThan" : "Between",
                        isOptional: f.optional,
                    })) || [],
                    orderByColumns: (values as any).orderBy || [],
                    includePagination: (values as any).includePagination ?? true,
                } : undefined,
            }

            generateMutation.mutate(requestData as any, {
                onSettled: () => setIsGenerating(false)
            })
        },
        [selectedProject, selectedTable, tableSchema, toast, generateMutation],
    )

    const handleExport = useCallback(
        (format: "sql" | "copy" | "zip") => {
            toast({ title: "Export", description: `Requested ${format.toUpperCase()}` })
        },
        [toast],
    )

    // Update default form values when spType switches
    const onChangeType = useCallback((t: SPType) => {
        setSpType(t)
        if (t === "CUD") {
            setConfig({
                mode: "CUD",
                generateCreate: true,
                generateUpdate: true,
                generateDelete: true,
                spPrefix: "usp",
                includeErrorHandling: true,
                includeTransaction: true,
                actionParamName: "Action",
            })
        } else {
            setConfig({
                mode: "SELECT",
                includePagination: true,
                orderBy: [],
                filters: [],
            })
        }
    }, [])

    const toggleLeftPanel = () => {
        setIsLeftPanelCollapsed(!isLeftPanelCollapsed)
    }

    return (
        <div className="min-h-screen flex flex-col">
            {/* Main with collapsible left panel and resizable right panels */}
            <main className="flex-1">
                <div className="flex">
                    {/* Left Panel - Collapsible */}
                    <div
                        className={cn(
                            "relative transition-all duration-300 ease-in-out overflow-hidden",
                            isLeftPanelCollapsed ? "w-0" : "w-100"
                        )}
                    >
                        <div className="absolute inset-0 p-4 pr-2">
                            <div className="h-full min-w-[300px] grid grid-rows-[auto_1fr_auto] gap-4">
                                <Card className="rounded-2xl p-4">
                                    <div className="text-sm font-medium mb-2">Databases</div>
                                    <TreeView
                                        treeData={treeData}
                                        onSelectNode={handleTreeSelect}
                                        searchQuery={treeSearch}
                                        onSearchChange={setTreeSearch}
                                        isLoading={isLoadingTables}
                                    />
                                </Card>
                                {/* <Card className="rounded-2xl p-4">
                                    <div className="text-sm font-medium mb-2">History</div>
                                    <DataTable
                                        rows={historyRows}
                                        columns={historyColumns}
                                        onRowClick={(row) => setSelectedTable(String((row as any).table))}
                                        isLoading={false}
                                    />
                                </Card> */}
                            </div>
                        </div>
                    </div>

                    {/* Toggle Button - Always visible */}
                    <button
                        onClick={toggleLeftPanel}
                        className={cn(
                            "group relative h-full w-1 bg-border hover:bg-primary/20 transition-all duration-200",
                            "flex items-center justify-center cursor-col-resize",
                            isLeftPanelCollapsed && "ml-0"
                        )}
                        title={isLeftPanelCollapsed ? "Show sidebar" : "Hide sidebar"}
                    >
                        <div className="absolute inset-y-0 flex items-center justify-center">
                            <div className="bg-background border rounded-md p-0.5 opacity-0 group-hover:opacity-100 transition-opacity shadow-sm">
                                {isLeftPanelCollapsed ? (
                                    <ChevronRight className="h-3 w-3" />
                                ) : (
                                    <ChevronLeft className="h-3 w-3" />
                                )}
                            </div>
                        </div>
                    </button>

                    {/* Right Panel - Resizable workspace */}
                    <div className="flex-1 p-4 pl-2">
                        <ResizablePanelGroup
                            direction="horizontal"
                            autoSaveId="workspace-split"
                            className={`h-full ${isFullscreen ? 'hidden' : ''}`}
                        >
                            <ResizablePanel defaultSize={50}>
                                <div className="h-full grid grid-rows-[auto_1fr] gap-4">
                                    <Card className="rounded-2xl p-4">
                                        <div className="text-sm font-medium mb-2">Configuration</div>
                                        {isLoadingSchema ? (
                                            <div className="flex items-center justify-center py-12 text-muted-foreground">
                                                <div className="flex flex-col items-center gap-2">
                                                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
                                                    <span>Loading schema...</span>
                                                </div>
                                            </div>
                                        ) : schemaError ? (
                                            <div className="flex items-center justify-center py-12 text-destructive">
                                                <div className="flex flex-col items-center gap-2">
                                                    <span>❌ Error loading schema</span>
                                                    <span className="text-xs text-muted-foreground">{schemaError.message}</span>
                                                </div>
                                            </div>
                                        ) : !selectedTable ? (
                                            <div className="flex items-center justify-center py-12 text-muted-foreground">
                                                <span>👈 Select a table from the tree</span>
                                            </div>
                                        ) : (
                                            <SPConfigPanel
                                                spType={spType}
                                                config={config}
                                                schema={schema}
                                                onSubmit={handleConfigSubmit}
                                                onChangeType={onChangeType}
                                            />
                                        )}
                                    </Card>
                                </div>
                            </ResizablePanel>

                            <ResizableHandle withHandle className="hover:bg-primary/20 transition-colors" />

                            <ResizablePanel defaultSize={50}>
                                <Card className="rounded-2xl p-4 h-full flex flex-col gap-3">
                                    <div className="flex items-center justify-between">
                                        <div className="text-sm font-medium">SQL Preview</div>
                                        <div className="flex items-center gap-2">
                                            <button
                                                onClick={() => setIsFullscreen(true)}
                                                className="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
                                                title="Fullscreen"
                                            >
                                                <Maximize2 className="w-4 h-4" />
                                            </button>
                                            <CodeExportButton onExport={handleExport} />
                                        </div>
                                    </div>
                                    <div className="flex-1 min-h-[300px]">
                                        <SPPreviewPane sqlCode={sqlCode} onChange={setSqlCode} isLoading={isGenerating} />
                                    </div>
                                </Card>
                            </ResizablePanel>
                        </ResizablePanelGroup>
                    </div>
                    {/* Fullscreen SQL Preview */}
                    <div
                        className={cn(
                            "fixed inset-0 z-50 bg-background transition-all duration-300 ease-in-out",
                            isFullscreen
                                ? "opacity-100 scale-100 visible"
                                : "opacity-0 scale-95 invisible"
                        )}
                    >
                        <Card className="rounded-2xl p-4 h-full flex flex-col gap-3">
                            <div className="flex items-center justify-between">
                                <div className="text-sm font-medium">SQL Preview</div>
                                <div className="flex items-center gap-2">
                                    <button
                                        onClick={() => setIsFullscreen(false)}
                                        className="p-2 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
                                        title="Exit Fullscreen"
                                    >
                                        <Minimize2 className="w-4 h-4" />
                                    </button>
                                    <CodeExportButton onExport={handleExport} />
                                </div>
                            </div>
                            <div className="flex-1 min-h-0">
                                <SPPreviewPane sqlCode={sqlCode} onChange={setSqlCode} isLoading={isGenerating} />
                            </div>
                        </Card>
                    </div>

                </div>
            </main>
        </div>
    )
}