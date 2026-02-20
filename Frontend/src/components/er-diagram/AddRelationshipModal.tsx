/**
 * AddRelationshipModal — Create a manual logical FK
 *
 * Source column dropdown (from current table) → target table search → target column selector.
 * Submits to POST /api/logical-fks/{projectId}
 */
import { useState, useMemo, useEffect, useRef } from "react";
import { Search, Plus, X, Link2, AlertTriangle } from "lucide-react";
import { queryKeys, useApi, useApiMutation } from "@/hooks/useApi";
import type { TableListItem } from "@/types/er-diagram";

interface ColumnItem {
    columnId: number;
    columnName?: string;
    name?: string;
    dataType: string;
    isPrimaryKey?: boolean;
    isForeignKey?: boolean;
}

interface AddRelationshipModalProps {
    projectId: number;
    sourceTableId: number;
    sourceTableName: string;
    sourceColumns: ColumnItem[];
    onClose: () => void;
    onSuccess: () => void;
}

interface CreateLogicalFkPayload {
    projectId: number;
    sourceTableId: number;
    sourceColumnIds: number[];
    targetTableId: number;
    targetColumnIds: number[];
    notes?: string;
}

export default function AddRelationshipModal({
    projectId,
    sourceTableId,
    sourceTableName,
    sourceColumns,
    onClose,
    onSuccess,
}: AddRelationshipModalProps) {
    const modalRef = useRef<HTMLDivElement | null>(null);
    const previousFocusedElementRef = useRef<HTMLElement | null>(null);
    const [sourceColumnId, setSourceColumnId] = useState<number | null>(null);
    const [targetTableId, setTargetTableId] = useState<number | null>(null);
    const [targetColumnId, setTargetColumnId] = useState<number | null>(null);
    const [tableSearch, setTableSearch] = useState("");
    const [showTableDropdown, setShowTableDropdown] = useState(false);
    const [notes, setNotes] = useState("");

    // Fetch all tables for target selection
    const { data: tables } = useApi<TableListItem[]>(
        `/DatabaseBrowser/projects/${projectId}/tables`,
        {
            queryKey: Array.from(queryKeys.tables.all(projectId)),
            staleTime: 5 * 60 * 1000,
        }
    );

    // Fetch target table details (including columns) when a target table is selected
    const { data: targetTableDetails } = useApi<{
        tableId: number;
        tableName: string;
        columns: ColumnItem[];
    }>(
        targetTableId
            ? `/DatabaseBrowser/projects/${projectId}/tables/${targetTableId}`
            : "",
        {
            queryKey: targetTableId
                ? Array.from(queryKeys.tables.detail(projectId, targetTableId))
                : [],
            enabled: !!targetTableId,
        }
    );

    const targetColumns = targetTableDetails?.columns;

    // Create mutation
    const createMutation = useApiMutation<unknown, CreateLogicalFkPayload>(
        "/logical-fks/:projectId",
        "POST",
        {
            successMessage: "Logical relationship created",
            invalidateKeys: [
                Array.from(queryKeys.logicalFks.byTable(projectId, sourceTableId)),
            ],
        }
    );

    // Filter tables by search (exclude current table)
    const filteredTables = useMemo(() => {
        if (!tables) return [];
        const filtered = tables.filter(
            (t) => t.tableId !== sourceTableId
        );
        if (!tableSearch.trim()) return filtered.slice(0, 15);
        const q = tableSearch.toLowerCase();
        return filtered
            .filter((t) => t.tableName.toLowerCase().includes(q))
            .slice(0, 15);
    }, [tables, tableSearch, sourceTableId]);

    const selectedTargetTable = tables?.find((t) => t.tableId === targetTableId);

    // Data type mismatch check
    const selectedSourceCol = sourceColumns.find((c) => c.columnId === sourceColumnId);
    const selectedTargetCol = (targetColumns ?? []).find(
        (c) => c.columnId != null && targetColumnId != null && c.columnId === targetColumnId
    );
    const dataTypeMismatch = useMemo(() => {
        if (!selectedSourceCol || !selectedTargetCol) return false;
        const normalize = (dt: string) => dt.toUpperCase().replace(/\(.*\)/, "").trim();
        return normalize(selectedSourceCol.dataType) !== normalize(selectedTargetCol.dataType);
    }, [selectedSourceCol, selectedTargetCol]);

    const canSubmit =
        sourceColumnId && targetTableId && targetColumnId && !dataTypeMismatch && !createMutation.isPending;

    const handleSubmit = () => {
        if (!sourceColumnId || !targetTableId || !targetColumnId) return;

        createMutation.mutate(
            {
                projectId,
                sourceTableId,
                sourceColumnIds: [sourceColumnId],
                targetTableId,
                targetColumnIds: [targetColumnId],
                notes: notes.trim() || undefined,
            },
            {
                onSuccess: () => {
                    onSuccess();
                    onClose();
                },
            }
        );
    };

    // Focus management: move focus into modal on open and restore focus to trigger on close
    useEffect(() => {
        previousFocusedElementRef.current = document.activeElement as HTMLElement | null;

        const modal = modalRef.current;
        if (!modal) {
            return;
        }

        const focusable = Array.from(
            modal.querySelectorAll<HTMLElement>(
                'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
            )
        ).filter((el) => !el.hasAttribute("aria-hidden"));

        (focusable[0] ?? modal).focus();

        return () => {
            previousFocusedElementRef.current?.focus();
        };
    }, []);

    // Keyboard handling: Escape to close + trap Tab/Shift+Tab within modal
    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            const modal = modalRef.current;
            if (!modal) return;

            if (e.key === "Escape") {
                e.preventDefault();
                onClose();
                return;
            }

            if (e.key !== "Tab") return;

            const focusable = Array.from(
                modal.querySelectorAll<HTMLElement>(
                    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
                )
            ).filter((el) => !el.hasAttribute("aria-hidden"));

            if (focusable.length === 0) {
                e.preventDefault();
                modal.focus();
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            const activeElement = document.activeElement as HTMLElement | null;

            if (e.shiftKey) {
                if (activeElement === first || !modal.contains(activeElement)) {
                    e.preventDefault();
                    last.focus();
                }
                return;
            }

            if (activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        };

        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [onClose]);

    // Handle click outside dropdown
    useEffect(() => {
        if (showTableDropdown) {
            const handleClickOutside = (e: MouseEvent) => {
                const target = e.target as HTMLElement;
                if (!target.closest(".table-search-container")) {
                    setShowTableDropdown(false);
                }
            };
            document.addEventListener("mousedown", handleClickOutside);
            return () => document.removeEventListener("mousedown", handleClickOutside);
        }
    }, [showTableDropdown]);

    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/50"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-title"
        >
            <div
                ref={modalRef}
                tabIndex={-1}
                className="bg-card border rounded-xl shadow-2xl w-[500px] max-h-[90vh] overflow-hidden"
            >
                {/* Header */}
                <div className="flex items-center justify-between p-5 border-b">
                    <div className="flex items-center gap-2">
                        <Link2 className="h-5 w-5 text-primary" />
                        <h3 id="modal-title" className="text-lg font-semibold">Add Logical Relationship</h3>
                    </div>
                    <button
                        onClick={onClose}
                        className="p-1 rounded-md hover:bg-muted transition-colors"
                        aria-label="Close modal"
                    >
                        <X className="h-4 w-4" />
                    </button>
                </div>

                {/* Body */}
                <div className="p-5 space-y-5 overflow-y-auto max-h-[60vh]">
                    {/* Source column */}
                    <div>
                        <label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2 block">
                            Source Column ({sourceTableName})
                        </label>
                        <select
                            value={sourceColumnId ?? ""}
                            onChange={(e) =>
                                setSourceColumnId(e.target.value === "" ? null : Number(e.target.value))
                            }
                            className="w-full px-3 py-2 text-sm rounded-md border bg-background focus:outline-none focus:ring-2 focus:ring-primary/50"
                        >
                            <option value="">Select column...</option>
                            {sourceColumns.map((col) => (
                                <option key={col.columnId} value={col.columnId}>
                                    {col.columnName || col.name} ({col.dataType})
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Target table */}
                    <div className="table-search-container">
                        <label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2 block">
                            Target Table
                        </label>
                        {selectedTargetTable ? (
                            <div className="flex items-center gap-2 px-3 py-2 border rounded-md bg-muted/30">
                                <span className="font-mono text-sm flex-1">
                                    {selectedTargetTable.tableName}
                                </span >
                                <button
                                    onClick={() => {
                                        setTargetTableId(null);
                                        setTargetColumnId(null);
                                        setTableSearch("");
                                    }}
                                    className="p-0.5 rounded hover:bg-muted"
                                    aria-label="Clear target table"
                                >
                                    <X className="h-3.5 w-3.5" />
                                </button>
                            </div>
                        ) : (
                            <div className="relative">
                                <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                                <input
                                    type="text"
                                    placeholder="Search tables..."
                                    value={tableSearch}
                                    onChange={(e) => {
                                        setTableSearch(e.target.value);
                                        setShowTableDropdown(true);
                                    }}
                                    onFocus={() => setShowTableDropdown(true)}
                                    className="w-full pl-9 pr-3 py-2 text-sm rounded-md border bg-background focus:outline-none focus:ring-2 focus:ring-primary/50"
                                    aria-label="Search target table"
                                    aria-expanded={showTableDropdown}
                                />
                                {showTableDropdown && filteredTables.length > 0 && (
                                    <div className="absolute z-10 top-full mt-1 w-full rounded-md border bg-popover shadow-lg max-h-40 overflow-auto">
                                        {filteredTables.map((table) => (
                                            <button
                                                key={table.tableId}
                                                className="w-full text-left px-3 py-2 text-sm hover:bg-muted transition-colors font-mono"
                                                onClick={() => {
                                                    setTargetTableId(table.tableId);
                                                    setTargetColumnId(null);
                                                    setTableSearch("");
                                                    setShowTableDropdown(false);
                                                }}
                                            >
                                                {table.tableName}
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </div>
                        )}
                    </div>

                    {/* Target column */}
                    {targetTableId && (
                        <div>
                            <label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2 block">
                                Target Column ({selectedTargetTable?.tableName})
                            </label>
                            <select
                                value={targetColumnId ?? ""}
                                onChange={(e) =>
                                    setTargetColumnId(e.target.value === "" ? null : Number(e.target.value))
                                }
                                className="w-full px-3 py-2 text-sm rounded-md border bg-background focus:outline-none focus:ring-2 focus:ring-primary/50"
                            >
                                <option value="">Select column...</option>
                                {(targetColumns ?? []).map((col) => (
                                    <option key={col.columnId} value={col.columnId}>
                                        {col.columnName || col.name} ({col.dataType})
                                        {col.isPrimaryKey ? " [PK]" : ""}
                                    </option>
                                ))}
                            </select>
                            {dataTypeMismatch && selectedSourceCol && selectedTargetCol && (
                                <div className="flex items-start gap-2 mt-2 p-2.5 rounded-md bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 text-amber-700 dark:text-amber-400 text-xs">
                                    <AlertTriangle className="h-3.5 w-3.5 shrink-0 mt-0.5" />
                                    <span>
                                        Data type mismatch: source is <code className="font-mono font-semibold">{selectedSourceCol.dataType}</code> but target is <code className="font-mono font-semibold">{selectedTargetCol.dataType}</code>. Columns must have compatible types.
                                    </span>
                                </div>
                            )}
                        </div>
                    )}

                    {/* Notes */}
                    <div>
                        <label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2 block">
                            Notes (optional)
                        </label>
                        <textarea
                            value={notes}
                            onChange={(e) => setNotes(e.target.value)}
                            placeholder="Why this relationship exists..."
                            rows={2}
                            className="w-full px-3 py-2 text-sm rounded-md border bg-background resize-none focus:outline-none focus:ring-2 focus:ring-primary/50"
                        />
                    </div>
                </div>

                {/* Footer */}
                <div className="flex justify-end gap-2 p-5 border-t">
                    <button
                        onClick={onClose}
                        className="px-4 py-2 text-sm rounded-lg border hover:bg-muted transition-colors"
                    >
                        Cancel
                    </button>
                    <button
                        onClick={handleSubmit}
                        disabled={!canSubmit}
                        className="flex items-center gap-2 px-4 py-2 text-sm rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50 transition-colors"
                    >
                        <Plus className="h-4 w-4" />
                        Create Relationship
                    </button>
                </div>
            </div>
        </div >
    );
}
