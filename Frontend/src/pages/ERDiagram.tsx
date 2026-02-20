/**
 * ER Diagram Page
 *
 * Interactive schema visualization using React Flow.
 * Search-first workflow: user searches for a table, then sees a 2-hop neighborhood.
 * Physical FKs shown as solid lines, logical FKs as dashed lines.
 * Click a dashed edge to confirm/reject a logical FK suggestion.
 */
import { useState, useCallback, useMemo, useEffect, useRef } from "react";
import { useParams } from "react-router-dom";
import {
    ReactFlow,
    Background,
    Controls,
    MiniMap,
    useNodesState,
    useEdgesState,
    type Node,
    type Edge,
    type EdgeMouseHandler,
    BackgroundVariant,
    Panel,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import {
    Search,
    GitBranch,
    Loader2,
    AlertCircle,
    CheckCircle2,
    XCircle,
    Clock,
    Zap,
    Undo2,
} from "lucide-react";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { queryKeys, useApi, useApiMutation } from "@/hooks/useApi";
import TableNode, { type TableNodeData } from "@/components/er-diagram/TableNode";
import { getLayoutedElements, type LayoutOptions } from "@/components/er-diagram/useAutoLayout";
import type {
    ErDiagramResponse,
    ErNodeData,
    ErEdgeData,
    TableListItem,
} from "@/types/er-diagram";

// Register custom node types
const nodeTypes = { table: TableNode };

export default function ERDiagramPage() {
    const { projectId } = useParams<{ projectId: string }>();
    const pid = Number(projectId);

    // State
    const [searchQuery, setSearchQuery] = useState("");
    const [isSearchOpen, setIsSearchOpen] = useState(false);
    const [selectedTableId, setSelectedTableId] = useState<number | null>(null);
    const [hops, setHops] = useState(1);
    const [layoutDirection, setLayoutDirection] = useState<"LR" | "TB">("LR");
    const [selectedEdge, setSelectedEdge] = useState<ErEdgeData | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const searchContainerRef = useRef<HTMLDivElement | null>(null);
    const modalRef = useRef<HTMLDivElement | null>(null);
    const previouslyFocusedElementRef = useRef<HTMLElement | null>(null);

    // React Flow state
    const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);

    // Fetch tables list for search
    const { data: tables } = useApi<TableListItem[]>(
        projectId ? `/DatabaseBrowser/projects/${pid}/tables` : "",
        {
            queryKey: projectId ? Array.from(queryKeys.tables.all(pid)) : [],
            staleTime: 5 * 60 * 1000,
        }
    );

    // Fetch ER diagram data for selected table
    const {
        data: erData,
        isLoading: isLoadingDiagram,
        error: diagramError,
    } = useApi<ErDiagramResponse>(
        selectedTableId
            ? `/er-diagram/projects/${pid}/neighborhood/${selectedTableId}?hops=${hops}`
            : "",
        {
            queryKey: selectedTableId
                ? Array.from(queryKeys.erDiagram.neighborhood(pid, selectedTableId, hops))
                : [],
            enabled: !!selectedTableId,
        }
    );

    const erDiagramInvalidateKey = selectedTableId
        ? Array.from(queryKeys.erDiagram.neighborhood(pid, selectedTableId))
        : [];

    // Confirm/reject mutations
    const confirmMutation = useApiMutation<unknown, { id: number; projectId: number; notes?: string }>(
        "/logical-fks/:projectId/:id/confirm",
        "PUT",
        {
            successMessage: "Logical FK confirmed",
            invalidateKeys: [erDiagramInvalidateKey],
        }
    );

    const rejectMutation = useApiMutation<unknown, { id: number; projectId: number; notes?: string }>(
        "/logical-fks/:projectId/:id/reject",
        "PUT",
        {
            successMessage: "Logical FK rejected",
            invalidateKeys: [erDiagramInvalidateKey],
        }
    );

    // Filter tables by search
    const filteredTables = useMemo(() => {
        if (!tables || !searchQuery.trim()) return tables?.slice(0, 20) ?? [];
        const q = searchQuery.toLowerCase();
        return tables.filter((t) => t.tableName.toLowerCase().includes(q)).slice(0, 20);
    }, [tables, searchQuery]);

    // Close search dropdown when clicking outside search container
    useEffect(() => {
        const handleDocumentMouseDown = (event: MouseEvent) => {
            if (!searchContainerRef.current) return;
            if (!searchContainerRef.current.contains(event.target as globalThis.Node)) {
                setIsSearchOpen(false);
            }
        };

        document.addEventListener("mousedown", handleDocumentMouseDown);
        return () => document.removeEventListener("mousedown", handleDocumentMouseDown);
    }, []);

    // Modal focus management + Escape close + Tab trapping
    useEffect(() => {
        if (!isModalOpen) return;

        previouslyFocusedElementRef.current = document.activeElement as HTMLElement | null;

        const modalElement = modalRef.current;
        if (modalElement) {
            const focusable = Array.from(
                modalElement.querySelectorAll<HTMLElement>(
                    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
                )
            ).filter((el) => !el.hasAttribute("aria-hidden"));

            (focusable[0] ?? modalElement).focus();
        }

        const handleModalKeyDown = (event: KeyboardEvent) => {
            const currentModal = modalRef.current;
            if (!currentModal) return;

            if (event.key === "Escape") {
                event.preventDefault();
                setIsModalOpen(false);
                return;
            }

            if (event.key !== "Tab") return;

            const focusable = Array.from(
                currentModal.querySelectorAll<HTMLElement>(
                    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
                )
            ).filter((el) => !el.hasAttribute("aria-hidden"));

            if (focusable.length === 0) {
                event.preventDefault();
                currentModal.focus();
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            const activeElement = document.activeElement as HTMLElement | null;

            if (event.shiftKey) {
                if (activeElement === first || !currentModal.contains(activeElement)) {
                    event.preventDefault();
                    last.focus();
                }
                return;
            }

            if (activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        };

        document.addEventListener("keydown", handleModalKeyDown);
        return () => {
            document.removeEventListener("keydown", handleModalKeyDown);
            previouslyFocusedElementRef.current?.focus();
        };
    }, [isModalOpen]);

    // Convert API data to React Flow nodes/edges
    useEffect(() => {
        if (!erData) return;

        const rfNodes: Node[] = erData.nodes.map((node) => ({
            id: `table-${node.tableId}`,
            type: "table",
            position: { x: 0, y: 0 }, // dagre will overwrite
            data: {
                tableId: node.tableId,
                label: node.tableName,
                schemaName: node.schemaName,
                columns: node.columns,
                depth: node.depth,
                isFocus: node.tableId === erData.focusTableId,
            } satisfies TableNodeData,
        }));

        const rfEdges: Edge[] = erData.edges.map((edge) => ({
            id: edge.id,
            source: `table-${edge.sourceTableId}`,
            target: `table-${edge.targetTableId}`,
            sourceHandle: `${edge.sourceColumnId}-source`,
            targetHandle: `${edge.targetColumnId}-target`,
            type: "smoothstep",
            animated: edge.relationshipType === "LOGICAL",
            style: {
                stroke:
                    edge.relationshipType === "LOGICAL"
                        ? edge.status === "CONFIRMED"
                            ? "#22c55e"
                            : "#f59e0b"
                        : "#6b7280",
                strokeWidth: edge.relationshipType === "LOGICAL" ? 2 : 1.5,
                strokeDasharray: edge.relationshipType === "LOGICAL" ? "6 4" : undefined,
            },
            label:
                edge.relationshipType === "LOGICAL"
                    ? `${edge.status} (${Math.round((edge.confidenceScore ?? 0) * 100)}%)`
                    : undefined,
            labelStyle: {
                fontSize: 10,
                fill:
                    edge.status === "CONFIRMED" ? "#22c55e" : "#f59e0b",
            },
            data: { ...edge } as Record<string, unknown>,
        }));

        const { nodes: layoutedNodes, edges: layoutedEdges } = getLayoutedElements(
            rfNodes,
            rfEdges,
            { direction: layoutDirection, focusNodeId: `table-${erData.focusTableId}` }
        );

        setNodes(layoutedNodes);
        setEdges(layoutedEdges);
    }, [erData, layoutDirection, setNodes, setEdges]);

    // Handle edge click for logical FK modal
    const onEdgeClick: EdgeMouseHandler<Edge> = useCallback(
        (_event: React.MouseEvent, edge: Edge) => {
            const edgeData = edge.data as unknown as ErEdgeData | undefined;
            if (edgeData?.relationshipType === "LOGICAL") {
                setSelectedEdge(edgeData);
                setIsModalOpen(true);
            }
        },
        []
    );

    // Handle confirm/reject
    const handleConfirm = () => {
        if (!selectedEdge?.logicalFkId || !projectId) return;
        confirmMutation.mutate(
            { id: selectedEdge.logicalFkId, projectId: pid },
            { onSuccess: () => setIsModalOpen(false) }
        );
    };

    const handleReject = () => {
        if (!selectedEdge?.logicalFkId || !projectId) return;
        const fkId = selectedEdge.logicalFkId;
        rejectMutation.mutate(
            { id: fkId, projectId: pid },
            {
                onSuccess: () => {
                    setIsModalOpen(false);
                    // Show undo toast for 8 seconds
                    toast("Logical FK rejected", {
                        duration: 8000,
                        action: {
                            label: "Undo",
                            onClick: () => {
                                confirmMutation.mutate(
                                    { id: fkId, projectId: pid }
                                );
                            },
                        },
                        icon: <Undo2 className="h-4 w-4" />,
                    });
                },
            }
        );
    };

    return (
        <div className="h-[calc(100vh-4rem)] flex flex-col">
            {/* Header */}
            <div className="flex items-center gap-3 p-4 border-b bg-background">
                <GitBranch className="h-5 w-5 text-primary" />
                <h1 className="text-lg font-semibold">ER Diagram</h1>

                {/* Search */}
                <div ref={searchContainerRef} className="relative ml-4 w-80">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <input
                        type="text"
                        aria-label="Search tables"
                        placeholder="Search tables..."
                        value={searchQuery}
                        onChange={(e) => {
                            setSearchQuery(e.target.value);
                            setIsSearchOpen(true);
                        }}
                        onFocus={() => setIsSearchOpen(true)}
                        className="w-full pl-9 pr-3 py-2 text-sm rounded-md border bg-background focus:outline-none focus:ring-2 focus:ring-primary/50"
                    />

                    {/* Search dropdown */}
                    {isSearchOpen && searchQuery.trim() && filteredTables.length > 0 && (
                        <div className="absolute z-50 top-full mt-1 w-full rounded-md border bg-popover shadow-lg max-h-60 overflow-auto">
                            {filteredTables.map((table) => (
                                <button
                                    key={table.tableId}
                                    className="w-full text-left px-3 py-2 text-sm hover:bg-muted transition-colors flex items-center gap-2"
                                    onClick={() => {
                                        setSelectedTableId(table.tableId);
                                        setSearchQuery("");
                                        setIsSearchOpen(false);
                                    }}
                                >
                                    <span className="font-mono">{table.tableName}</span>
                                    {table.schemaName && (
                                        <span className="text-xs text-muted-foreground">
                                            ({table.schemaName})
                                        </span>
                                    )}
                                </button>
                            ))}
                        </div>
                    )}
                </div>

                {isLoadingDiagram && (
                    <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                )}

                {/* Hop depth selector */}
                {selectedTableId && (
                    <div className="ml-auto flex items-center gap-3 text-sm">
                        {/* Direction toggle */}
                        <div className="flex items-center gap-1.5">
                            <span className="text-muted-foreground">Layout:</span>
                            {(["LR", "TB"] as const).map((dir) => (
                                <button
                                    key={dir}
                                    onClick={() => setLayoutDirection(dir)}
                                    className={`px-2 py-1 rounded-md border transition-colors ${layoutDirection === dir
                                        ? "bg-primary text-primary-foreground border-primary"
                                        : "hover:bg-muted border-border"
                                        }`}
                                >
                                    {dir === "LR" ? "\u2192" : "\u2193"}
                                </button>
                            ))}
                        </div>

                        <div className="w-px h-5 bg-border" />

                        {/* Depth selector */}
                        <div className="flex items-center gap-1.5">
                            <span className="text-muted-foreground">Depth:</span>
                            {[1, 2, 3].map((h) => (
                                <button
                                    key={h}
                                    onClick={() => setHops(h)}
                                    className={`px-2.5 py-1 rounded-md border transition-colors ${hops === h
                                        ? "bg-primary text-primary-foreground border-primary"
                                        : "hover:bg-muted border-border"
                                        }`}
                                >
                                    {h}
                                </button>
                            ))}
                        </div>
                    </div>
                )}
            </div>

            {/* Diagram canvas */}
            <div className="flex-1 relative">
                {!selectedTableId ? (
                    <div className="flex items-center justify-center h-full text-muted-foreground">
                        <div className="text-center">
                            <GitBranch className="h-12 w-12 mx-auto mb-4 opacity-30" />
                            <p className="text-lg font-medium">Search for a table to explore</p>
                            <p className="text-sm mt-1">
                                Select a table from the search above to view its relationships
                            </p>
                        </div>
                    </div>
                ) : diagramError ? (
                    <div className="flex items-center justify-center h-full text-destructive">
                        <div className="text-center">
                            <AlertCircle className="h-8 w-8 mx-auto mb-2" />
                            <p>Failed to load ER diagram</p>
                        </div>
                    </div>
                ) : (
                    <ReactFlow
                        nodes={nodes}
                        edges={edges}
                        onNodesChange={onNodesChange}
                        onEdgesChange={onEdgesChange}
                        onEdgeClick={onEdgeClick}
                        nodeTypes={nodeTypes}
                        fitView
                        fitViewOptions={{ padding: 0.3 }}
                        minZoom={0.3}
                        maxZoom={2}
                        proOptions={{ hideAttribution: true }}
                    >
                        <Background variant={BackgroundVariant.Dots} gap={20} size={1} />
                        <Controls />
                        <MiniMap
                            nodeStrokeWidth={3}
                            className="!bg-card !border-border"
                        />

                        {/* Legend */}
                        <Panel position="bottom-left">
                            <div className="bg-card border rounded-lg p-3 shadow-sm text-xs space-y-2">
                                <div className="font-semibold text-sm mb-1">Legend</div>
                                <div className="flex items-center gap-2">
                                    <div className="w-6 h-0.5 bg-gray-500" />
                                    <span>Physical FK</span>
                                </div>
                                <div className="flex items-center gap-2">
                                    <div
                                        className="w-6 h-0.5 bg-amber-500"
                                        style={{ backgroundImage: "repeating-linear-gradient(90deg, #f59e0b 0px, #f59e0b 4px, transparent 4px, transparent 8px)" }}
                                    />
                                    <span>Logical FK (Suggested)</span>
                                </div>
                                <div className="flex items-center gap-2">
                                    <div
                                        className="w-6 h-0.5 bg-green-500"
                                        style={{ backgroundImage: "repeating-linear-gradient(90deg, #22c55e 0px, #22c55e 4px, transparent 4px, transparent 8px)" }}
                                    />
                                    <span>Logical FK (Confirmed)</span>
                                </div>
                            </div>
                        </Panel>
                    </ReactFlow>
                )}
            </div>

            {/* Logical FK Action Modal */}
            {isModalOpen && selectedEdge && (
                <div
                    className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="fk-modal-title"
                    onClick={(e) => { if (e.target === e.currentTarget) setIsModalOpen(false); }}
                >
                    <div
                        ref={modalRef}
                        tabIndex={-1}
                        className="bg-card border rounded-xl shadow-2xl w-[460px] p-6"
                    >
                        <div className="flex items-center justify-between mb-5">
                            <h3 id="fk-modal-title" className="text-lg font-semibold">Logical Foreign Key</h3>
                            <span
                                className={`text-xs font-semibold px-2 py-1 rounded-full ${selectedEdge.status === "CONFIRMED"
                                    ? "bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-400"
                                    : selectedEdge.status === "REJECTED"
                                        ? "bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400"
                                        : "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400"
                                    }`}
                            >
                                {selectedEdge.status}
                            </span>
                        </div>

                        {/* Relationship columns */}
                        <div className="space-y-3 text-sm">
                            <div>
                                <span className="text-muted-foreground text-xs uppercase tracking-wider">Source Column</span>
                                <div className="font-mono mt-0.5">{selectedEdge.sourceColumnName}</div>
                            </div>
                            <div>
                                <span className="text-muted-foreground text-xs uppercase tracking-wider">Target Column</span>
                                <div className="font-mono mt-0.5">{selectedEdge.targetColumnName}</div>
                            </div>
                        </div>

                        {/* Metadata section */}
                        <div className="mt-5 pt-4 border-t space-y-2.5 text-xs text-muted-foreground">
                            <div className="flex items-center gap-2">
                                <Zap className="h-3.5 w-3.5 shrink-0" />
                                <span>Confidence:</span>
                                <span className="font-semibold text-foreground">
                                    {Math.round((selectedEdge.confidenceScore ?? 0) * 100)}%
                                </span>
                            </div>
                            {selectedEdge.discoveryMethod && (
                                <div className="flex items-center gap-2">
                                    <Zap className="h-3.5 w-3.5 shrink-0" />
                                    <span>Discovery:</span>
                                    <span className="font-semibold text-foreground capitalize">
                                        {selectedEdge.discoveryMethod.toLowerCase()}
                                    </span>
                                </div>
                            )}
                            {selectedEdge.createdAt && (
                                <div className="flex items-center gap-2">
                                    <Clock className="h-3.5 w-3.5 shrink-0" />
                                    <span>Detected:</span>
                                    <span className="text-foreground">
                                        {new Date(selectedEdge.createdAt).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" })}
                                    </span>
                                </div>
                            )}
                            {selectedEdge.confirmedAt && (
                                <div className="flex items-center gap-2">
                                    <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-green-500" />
                                    <span>Confirmed:</span>
                                    <span className="text-foreground">
                                        {new Date(selectedEdge.confirmedAt).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" })}
                                    </span>
                                </div>
                            )}
                        </div>

                        {/* Action buttons */}
                        <div className="flex gap-2 mt-6">
                            {selectedEdge.status !== "CONFIRMED" && (
                                <button
                                    onClick={handleConfirm}
                                    disabled={confirmMutation.isPending}
                                    className="flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-lg bg-green-600 text-white hover:bg-green-700 disabled:opacity-50 transition-colors"
                                >
                                    <CheckCircle2 className="h-4 w-4" />
                                    Confirm
                                </button>
                            )}
                            {selectedEdge.status === "SUGGESTED" && (
                                <button
                                    onClick={handleReject}
                                    disabled={rejectMutation.isPending}
                                    className="flex-1 flex items-center justify-center gap-2 px-4 py-2 rounded-lg bg-red-600 text-white hover:bg-red-700 disabled:opacity-50 transition-colors"
                                >
                                    <XCircle className="h-4 w-4" />
                                    Reject
                                </button>
                            )}
                            <button
                                onClick={() => setIsModalOpen(false)}
                                className="flex-1 px-4 py-2 rounded-lg border hover:bg-muted transition-colors"
                            >
                                Close
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

