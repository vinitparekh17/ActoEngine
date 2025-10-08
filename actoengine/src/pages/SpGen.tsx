"use client"

import { useCallback, useMemo, useState } from "react"
import { useToast } from "../hooks/useToast"
import type { SPType } from "../components/codegen/SPTypeCard"
import { ChevronRight, ChevronLeft, Maximize2, Minimize2 } from "lucide-react"
import CodeExportButton from "../components/codegen/CodeExportButton"
import SPConfigPanel, { type SPConfigValues } from "../components/codegen/SPConfigPanel"
import SPPreviewPane from "../components/codegen/SPPreviewPanel"
// import SPTypeCard from "../components/codegen/SPTypeCard"
import DataTable, { type DataTableColumn } from "../components/database/DataTable"
import TableSchemaViewer, { type TableSchema } from "../components/database/TableSchemaViewer"
import TreeView from "../components/database/TreeView"
// import type { ProjectOption } from "../components/project/ProjectSelector"
import { Card } from "../components/ui/card"
import { ResizablePanelGroup, ResizablePanel, ResizableHandle } from "../components/ui/resizable"
import { cn } from "../lib/utils"
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "../components/ui/accordion"

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
    const [isLoadingDbs] = useState(false)
    const [treeSearch, setTreeSearch] = useState("")
    const [isLeftPanelCollapsed, setIsLeftPanelCollapsed] = useState(false)
    const [isFullscreen, setIsFullscreen] = useState(false)

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
        () => [
            {
                id: "db1",
                name: "MyDatabase",
                type: "database",
                children: [
                    {
                        id: "db1-tables",
                        name: "Tables",
                        type: "tables-folder",
                        children: [
                            {
                                id: "table1",
                                name: "Users",
                                type: "table",
                                children: [
                                    { id: "col1", name: "UserId", type: "column" },
                                    { id: "col2", name: "Username", type: "column" },
                                    { id: "idx1", name: "IX_Users_Username", type: "index" }
                                ]
                            }
                        ]
                    },
                    {
                        id: "db1-prog",
                        name: "Programmability",
                        type: "programmability-folder",
                        children: [
                            {
                                id: "db1-sps",
                                name: "Stored Procedures",
                                type: "stored-procedures-folder",
                                children: [
                                    { id: "sp1", name: "GetUserById", type: "stored-procedure" }
                                ]
                            },
                            {
                                id: "db1-funcs",
                                name: "Functions",
                                type: "functions-folder",
                                children: [
                                    { id: "fn1", name: "CalculateTotal", type: "scalar-function" },
                                    { id: "fn2", name: "GetUserOrders", type: "table-function" }
                                ]
                            }
                        ]
                    }
                ]
            }
        ],
        [],
    )

    const schema = useMemo<TableSchema>(
        () => ({
            tableName: selectedTable ?? "",
            schemaName: "dbo",
            columns: [
                { name: "id", dataType: "uuid", constraints: ["PK", "NOT NULL", "DEFAULT gen_random_uuid()"] },
                { name: "email", dataType: "varchar(255)", constraints: ["UNIQUE", "NOT NULL"] },
                { name: "created_at", dataType: "timestamp", constraints: ["DEFAULT now()"] },
            ],
        }),
        [selectedTable],
    )

    const availableColumns = useMemo(() => schema.columns.map((c) => c.name), [schema])

    const historyColumns = useMemo<DataTableColumn[]>(
        () => [
            { header: "Table", accessorKey: "table" },
            { header: "Type", accessorKey: "type" },
            { header: "When", accessorKey: "time" },
            { header: "Status", accessorKey: "status" },
        ],
        [],
    )
    const historyRows = useMemo(
        () => [
            { id: "1", table: "users", type: "CUD", time: "2m ago", status: "draft" },
            { id: "2", table: "orders", type: "SELECT", time: "10m ago", status: "exported" },
        ],
        [],
    )

    const handleTreeSelect = useCallback((node: TreeNode) => {
        // Now you receive the full node object instead of just the table name

        // Handle different node types
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
            setConfig(values)
            setIsGenerating(true)
            // Simulate SQL generation preview
            const preview = `-- Type: ${spType}
-- Table: ${selectedTable ?? "(none)"}
-- Config: ${JSON.stringify(values, null, 2)}
-- SQL preview...
`
            setTimeout(() => {
                setSqlCode(preview)
                setIsGenerating(false)
            }, 600)
        },
        [selectedTable, spType],
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
                                    {/* <TreeView
                                        treeData={treeData}
                                        onSelectTable={handleTreeSelect}
                                        searchQuery={treeSearch}
                                        onSearchChange={setTreeSearch}
                                        isLoading={isLoadingDbs}
                                    /> */}
                                    <TreeView
                                        treeData={treeData}
                                        onSelectNode={handleTreeSelect}  // renamed from onSelectTable
                                        searchQuery={treeSearch}
                                        onSearchChange={setTreeSearch}
                                        isLoading={isLoadingDbs}
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
                                <div className="h-full grid grid-rows-[auto_auto_1fr] gap-4">
                                    <Card className="rounded-2xl p-4 overflow-hidden">
                                        <Accordion type="single" collapsible>
                                            <AccordionItem value="table-schema">
                                                <AccordionTrigger className="hover:no-underline">Table Schema</AccordionTrigger>
                                                <AccordionContent>
                                                    <TableSchemaViewer schema={schema} selectedTable={selectedTable ?? ""} />
                                                </AccordionContent>
                                            </AccordionItem>
                                        </Accordion>
                                    </Card>
                                    <Card className="rounded-2xl p-4">
                                        <div className="text-sm font-medium mb-2">Configuration</div>
                                        <SPConfigPanel
                                            spType={spType}
                                            config={config}
                                            availableColumns={availableColumns}
                                            onSubmit={handleConfigSubmit}
                                            onChangeType={onChangeType}
                                        />
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