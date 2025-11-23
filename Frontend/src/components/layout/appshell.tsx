// "use client"

// import { useState, useMemo, useCallback } from "react"
// import { Card } from "../ui/card"
// import { useToast } from "../../hooks/useToast"
// import { type ProjectOption } from "../project/ProjectSelector"
// import TreeView from "../database/TreeView"
// import TableSchemaViewer, { type TableSchema } from "../database/TableSchemaViewer"
// import SPTypeCard, { type SPType } from "../codegen/SPTypeCard"
// import SPConfigPanel, { type SPConfigValues } from "../codegen/SPConfigPanel"
// import SPPreviewPane from "../codegen/SPPreviewPanel"
// import CodeExportButton from "../codegen/CodeExportButton"
// import DataTable, { type DataTableColumn } from "../database/DataTable"
// import { ResizablePanelGroup, ResizablePanel, ResizableHandle } from "../../components/ui/resizable"
// import { ChevronLeft, ChevronRight } from "lucide-react"    
// import { cn } from "../../lib/utils"

// type TreeNode = {
//   id: string
//   name: string
//   children?: TreeNode[]
//   type?: "database" | "table"
// }

// export default function AppShell() {
//   const { showToast: toast } = useToast()
//   const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
//   const [selectedTable, setSelectedTable] = useState<string | null>(null)
//   const [spType, setSpType] = useState<SPType>("CUD")
//   const [sqlCode, setSqlCode] = useState<string>("-- Generated SQL will appear here")
//   const [isGenerating, setIsGenerating] = useState(false)
//   const [isLoadingDbs] = useState(false)
//   const [treeSearch, setTreeSearch] = useState("")
//   const [isLeftPanelCollapsed, setIsLeftPanelCollapsed] = useState(false)

//   // Default config values by mode
//   const [config, setConfig] = useState<SPConfigValues>({
//     mode: "CUD",
//     generateCreate: true,
//     generateUpdate: true,
//     generateDelete: true,
//     spPrefix: "usp",
//     includeErrorHandling: true,
//     includeTransaction: true,
//     actionParamName: "Action",
//   })

//   const projects = useMemo<ProjectOption[]>(
//     () => [
//       { id: "proj_1", name: "Marketing DB" },
//       { id: "proj_2", name: "Product DB" },
//     ],
//     [],
//   )

//   const treeData = useMemo<TreeNode[]>(
//     () => [
//       {
//         id: "db_main",
//         name: "main",
//         type: "database",
//         children: [
//           { id: "tbl_users", name: "users", type: "table" },
//           { id: "tbl_orders", name: "orders", type: "table" },
//         ],
//       },
//       {
//         id: "db_reporting",
//         name: "reporting",
//         type: "database",
//         children: [{ id: "tbl_daily", name: "daily_metrics", type: "table" }],
//       },
//     ],
//     [],
//   )

//   const schema = useMemo<TableSchema>(
//     () => ({
//       tableName: selectedTable ?? "",
//       schemaName: "dbo",
//       columns: [
//         { name: "id", dataType: "uuid", constraints: ["PK", "NOT NULL", "DEFAULT gen_random_uuid()"] },
//         { name: "email", dataType: "varchar(255)", constraints: ["UNIQUE", "NOT NULL"] },
//         { name: "created_at", dataType: "timestamp", constraints: ["DEFAULT now()"] },
//       ],
//     }),
//     [selectedTable],
//   )

//   const availableColumns = useMemo(() => schema.columns.map((c) => c.name), [schema])

//   const historyColumns = useMemo<DataTableColumn[]>(
//     () => [
//       { header: "Table", accessorKey: "table" },
//       { header: "Type", accessorKey: "type" },
//       { header: "When", accessorKey: "time" },
//       { header: "Status", accessorKey: "status" },
//     ],
//     [],
//   )
//   const historyRows = useMemo(
//     () => [
//       { id: "1", table: "users", type: "CUD", time: "2m ago", status: "draft" },
//       { id: "2", table: "orders", type: "SELECT", time: "10m ago", status: "exported" },
//     ],
//     [],
//   )

//   const handleProjectSelect = useCallback(
//     (id: string) => {
//       setSelectedProjectId(id)
//       toast({ title: "Project selected", description: id })
//     },
//     [toast],
//   )

//   const handleTreeSelect = useCallback((tableName: string) => {
//     setSelectedTable(tableName)
//   }, [])

//   const handleConfigSubmit = useCallback(
//     (values: SPConfigValues) => {
//       setConfig(values)
//       setIsGenerating(true)
//       // Simulate SQL generation preview
//       const preview = `-- Type: ${spType}
// -- Table: ${selectedTable ?? "(none)"}
// -- Config: ${JSON.stringify(values, null, 2)}
// -- SQL preview...
// `
//       setTimeout(() => {
//         setSqlCode(preview)
//         setIsGenerating(false)
//       }, 600)
//     },
//     [selectedTable, spType],
//   )

//   const handleExport = useCallback(
//     (format: "sql" | "copy" | "zip") => {
//       toast({ title: "Export", description: `Requested ${format.toUpperCase()}` })
//     },
//     [toast],
//   )

//   // Update default form values when spType switches
//   const onChangeType = useCallback((t: SPType) => {
//     setSpType(t)
//     if (t === "CUD") {
//       setConfig({
//         mode: "CUD",
//         generateCreate: true,
//         generateUpdate: true,
//         generateDelete: true,
//         spPrefix: "usp",
//         includeErrorHandling: true,
//         includeTransaction: true,
//         actionParamName: "Action",
//       })
//     } else {
//       setConfig({
//         mode: "SELECT",
//         includePagination: true,
//         orderBy: [],
//         filters: [],
//       })
//     }
//   }, [])

//   const toggleLeftPanel = () => {
//     setIsLeftPanelCollapsed(!isLeftPanelCollapsed)
//   }

//   return (
//     <div className="min-h-screen flex flex-col">
//       {/* Header */}
//       {/* <header className="w-full border-b bg-background">
//         <div className="mx-auto px-4 py-3 flex items-center justify-between gap-4">
//           <div className="flex items-center gap-3">
//             <Button
//               variant="ghost"
//               size="icon"
//               onClick={toggleLeftPanel}
//               className="h-8 w-8"
//               title={isLeftPanelCollapsed ? "Show sidebar" : "Hide sidebar"}
//             >
//               {isLeftPanelCollapsed ? <PanelLeft className="h-4 w-4" /> : <PanelLeftClose className="h-4 w-4" />}
//             </Button>
//             <div className="font-semibold">ActoX</div>
//             <Separator orientation="vertical" className="h-6" />
//             <ProjectSelector projects={projects} onSelect={handleProjectSelect} />
//           </div>
//           <div className="flex items-center gap-2">
//             <ThemeToggle />
//             <Button variant="outline" size="sm">
//               Help
//             </Button>
//           </div>
//         </div>
//       </header> */}

//       {/* Main with collapsible left panel and resizable right panels */}
//       <main className="flex-1">
//         <div className="flex">
//           {/* Left Panel - Collapsible */}
//           <div
//             className={cn(
//               "relative transition-all duration-300 ease-in-out overflow-hidden",
//               isLeftPanelCollapsed ? "w-0" : "w-100"
//             )}
//           >
//             <div className="absolute inset-0 p-4 pr-2">
//               <div className="h-full min-w-[300px] grid grid-rows-[auto_1fr_auto] gap-4">
//                 <Card className="rounded-2xl p-4">
//                   <div className="text-sm font-medium mb-2">Databases</div>
//                   <TreeView
//                     treeData={treeData}
//                     onSelectTable={handleTreeSelect}
//                     searchQuery={treeSearch}
//                     onSearchChange={setTreeSearch}
//                     isLoading={isLoadingDbs}
//                   />
//                 </Card>
//                 <Card className="rounded-2xl p-4">
//                   <div className="text-sm font-medium mb-2">History</div>
//                   <DataTable
//                     rows={historyRows}
//                     columns={historyColumns}
//                     onRowClick={(row) => setSelectedTable(String((row as any).table))}
//                     isLoading={false}
//                   />
//                 </Card>
//               </div>
//             </div>
//           </div>

//           {/* Toggle Button - Always visible */}
//           <button
//             onClick={toggleLeftPanel}
//             className={cn(
//               "group relative h-full w-1 bg-border hover:bg-primary/20 transition-all duration-200",
//               "flex items-center justify-center cursor-col-resize",
//               isLeftPanelCollapsed && "ml-0"
//             )}
//             title={isLeftPanelCollapsed ? "Show sidebar" : "Hide sidebar"}
//           >
//             <div className="absolute inset-y-0 flex items-center justify-center">
//               <div className="bg-background border rounded-md p-0.5 opacity-0 group-hover:opacity-100 transition-opacity shadow-sm">
//                 {isLeftPanelCollapsed ? (
//                   <ChevronRight className="h-3 w-3" />
//                 ) : (
//                   <ChevronLeft className="h-3 w-3" />
//                 )}
//               </div>
//             </div>
//           </button>

//           {/* Right Panel - Resizable workspace */}
//           <div className="flex-1 p-4 pl-2">
//             <ResizablePanelGroup direction="horizontal" autoSaveId="workspace-split" className="h-full">
//               <ResizablePanel defaultSize={50}>
//                 <div className="h-full grid grid-rows-[auto_auto_1fr] gap-4">
//                   <Card className="rounded-2xl p-4">
//                     <div className="text-sm font-medium mb-2">Stored Procedure Type</div>
//                     <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
//                       <SPTypeCard type="CUD" selected={spType === "CUD"} onChange={onChangeType} />
//                       <SPTypeCard type="SELECT" selected={spType === "SELECT"} onChange={onChangeType} />
//                     </div>
//                   </Card>

//                   <Card className="rounded-2xl p-4">
//                     <div className="text-sm font-medium mb-2">Configuration</div>
//                     <SPConfigPanel
//                       spType={spType}
//                       config={config}
//                       availableColumns={availableColumns}
//                       onSubmit={handleConfigSubmit}
//                     />
//                   </Card>

//                   <Card className="rounded-2xl p-4 overflow-hidden">
//                     <div className="text-sm font-medium mb-3">Table Schema</div>
//                     <TableSchemaViewer schema={schema} selectedTable={selectedTable ?? ""} />
//                   </Card>
//                 </div>
//               </ResizablePanel>

//               <ResizableHandle withHandle className="hover:bg-primary/20 transition-colors" />

//               <ResizablePanel defaultSize={50}>
//                 <Card className="rounded-2xl p-4 h-full flex flex-col gap-3">
//                   <div className="flex items-center justify-between">
//                     <div className="text-sm font-medium">SQL Preview</div>
//                     <CodeExportButton onExport={handleExport} />
//                   </div>
//                   <div className="flex-1 min-h-[300px]">
//                     <SPPreviewPane sqlCode={sqlCode} onChange={setSqlCode} isLoading={isGenerating} />
//                   </div>
//                 </Card>
//               </ResizablePanel>
//             </ResizablePanelGroup>
//           </div>
//         </div>
//       </main>
//     </div>
//   )
// }