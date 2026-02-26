/**
 * LogicalFkPanel — Relationships tab content for EntityDetailPage
 *
 * Shows:
 * 1. Physical Foreign Keys (read-only)
 * 2. Logical Foreign Keys (confirmed + suggested)  
 * 3. "Add Logical Relationship" button → modal
 * 4. "Run Detection" to auto-detect candidates
 */
import { useState, useCallback, useMemo } from "react";
import {
    ArrowRight,
    Check,
    X,
    Plus,
    Sparkles,
    Link2,
    Loader2,
    Trash2,
    Undo2,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { queryKeys, useApi, useApiPut, useApiDelete } from "@/hooks/useApi";
import { useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { toast } from "sonner";
import { utcToLocal } from "@/lib/utils";
import type { LogicalFkDto, PhysicalFkDto } from "@/types/er-diagram";
import AddRelationshipModal from "./AddRelationshipModal";

// ---------- Types ----------

interface ColumnForModal {
    columnId: number;
    columnName: string;
    dataType: string;
    isPrimaryKey?: boolean;
    isForeignKey?: boolean;
}

interface LogicalFkPanelProps {
    projectId: number;
    tableId: number;
    tableName: string;
    /** Current table columns (for the Add modal) */
    columns: ColumnForModal[];
}

export const logicalFkQueryKey = (projectId: number, tableId: number) =>
    Array.from(queryKeys.logicalFks.byTable(projectId, tableId));

// ---------- Component ----------

export default function LogicalFkPanel({
    projectId,
    tableId,
    tableName,
    columns,
}: LogicalFkPanelProps) {
    const normalizedProjectId = Number(projectId);
    const normalizedTableId = Number(tableId);
    const [showAddModal, setShowAddModal] = useState(false);
    const [isDetecting, setIsDetecting] = useState(false);
    const [inflightFkId, setInflightFkId] = useState<number | null>(null);
    const queryClient = useQueryClient();

    // Fetch physical FKs
    const { data: physicalFks, isLoading: isLoadingPhysical } = useApi<PhysicalFkDto[]>(
        `/logical-fks/${normalizedProjectId}/table/${normalizedTableId}/physical`,
        { queryKey: ["physical-fks", normalizedProjectId, "table", normalizedTableId] }
    );

    // Fetch logical FKs for this table
    const logicalFkTableQueryKey = useMemo(
        () => logicalFkQueryKey(normalizedProjectId, normalizedTableId),
        [normalizedProjectId, normalizedTableId]
    );
    const { data: logicalFks, isLoading } = useApi<LogicalFkDto[]>(
        `/logical-fks/${normalizedProjectId}/table/${normalizedTableId}`,
        { queryKey: logicalFkTableQueryKey, staleTime: 30_000 }
    );

    // Mutations
    const confirmMutation = useApiPut<unknown, { id: number }>(
        `/logical-fks/${normalizedProjectId}/:id/confirm`,
        {
            successMessage: "Relationship confirmed",
            invalidateKeys: [logicalFkTableQueryKey],
        }
    );

    const rejectMutation = useApiPut<unknown, { id: number }>(
        `/logical-fks/${normalizedProjectId}/:id/reject`,
        {
            invalidateKeys: [logicalFkTableQueryKey],
        }
    );

    const deleteMutation = useApiDelete<unknown, { id: number }>(
        `/logical-fks/${normalizedProjectId}/:id`,
        {
            successMessage: "Relationship removed",
            invalidateKeys: [logicalFkTableQueryKey],
        }
    );

    // Detect candidates
    const handleDetect = useCallback(async () => {
        setIsDetecting(true);
        try {
            await api.get(`/logical-fks/${normalizedProjectId}/detect-candidates`);
            queryClient.invalidateQueries({ queryKey: logicalFkTableQueryKey });
            toast.success("Detection complete — check for new suggestions");
        } catch {
            toast.error("Detection failed");
        } finally {
            setIsDetecting(false);
        }
    }, [normalizedProjectId, queryClient, logicalFkTableQueryKey]);

    // Split logical FKs by status
    const confirmed = (logicalFks ?? []).filter((fk) => fk.status === "CONFIRMED");
    const suggested = (logicalFks ?? []).filter((fk) => fk.status === "SUGGESTED");

    // Determine direction labels (this table could be source or target)
    const fkLabel = (fk: LogicalFkDto) => {
        const isSource = fk.sourceTableId === normalizedTableId;
        const otherTable = isSource ? fk.targetTableName : fk.sourceTableName;
        const myColumns = isSource ? fk.sourceColumnNames : fk.targetColumnNames;
        const otherColumns = isSource ? fk.targetColumnNames : fk.sourceColumnNames;
        return {
            myColumns: myColumns.join(", "),
            otherTable,
            otherColumns: otherColumns.join(", "),
            direction: isSource ? "→" : "←",
        };
    };

    return (
        <div className="space-y-6">
            {/* Physical Foreign Keys */}
            <Card className="border-border/60 shadow-sm">
                <CardHeader className="bg-muted/30 pb-3 border-b">
                    <CardTitle className="text-sm font-semibold flex items-center gap-2">
                        <Link2 className="h-4 w-4 text-blue-500" />
                        Physical Foreign Keys
                        <Badge variant="secondary" className="ml-1 h-5 px-1.5 text-xs">
                            {physicalFks?.length ?? 0}
                        </Badge>
                    </CardTitle>
                </CardHeader>
                <CardContent className="p-0">
                    {isLoadingPhysical ? (
                        <div className="flex items-center justify-center py-6">
                            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                        </div>
                    ) : (physicalFks?.length ?? 0) === 0 ? (
                        <div className="px-4 py-6 text-center text-sm text-muted-foreground">
                            No physical foreign keys on this table
                        </div>
                    ) : (
                        <div className="divide-y">
                            {(physicalFks || []).map((fk) => (
                                <div
                                    key={
                                        fk.foreignKeyName ||
                                        `${fk.sourceTableName}.${fk.sourceColumnName}->${fk.targetTableName}.${fk.targetColumnName}`
                                    }
                                    className="flex items-center gap-2 px-4 py-2.5 text-sm hover:bg-muted/20 transition-colors"
                                >
                                    <code className="text-xs font-mono text-foreground bg-muted px-1.5 py-0.5 rounded">
                                        {fk.sourceColumnName}
                                    </code>
                                    <ArrowRight className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                                    <code className="text-xs font-mono text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/40 px-1.5 py-0.5 rounded border border-blue-100 dark:border-blue-900">
                                        {fk.targetTableName}.{fk.targetColumnName}
                                    </code>
                                    {fk.onDeleteAction && fk.onDeleteAction !== "NO ACTION" && (
                                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-red-50 text-red-600 dark:bg-red-950/30 dark:text-red-400 border border-red-200 dark:border-red-900 shrink-0">
                                            ON DELETE {fk.onDeleteAction}
                                        </span>
                                    )}
                                    {fk.onUpdateAction && fk.onUpdateAction !== "NO ACTION" && (
                                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-50 text-blue-600 dark:bg-blue-950/30 dark:text-blue-400 border border-blue-200 dark:border-blue-900 shrink-0">
                                            ON UPDATE {fk.onUpdateAction}
                                        </span>
                                    )}
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Logical Foreign Keys */}
            <Card className="border-border/60 shadow-sm">
                <CardHeader className="bg-muted/30 pb-3 border-b">
                    <div className="flex items-center justify-between">
                        <CardTitle className="text-sm font-semibold flex items-center gap-2">
                            <Sparkles className="h-4 w-4 text-amber-500" />
                            Logical Foreign Keys
                            {!isLoading && (
                                <span className="text-xs font-normal text-muted-foreground ml-1">
                                    ({confirmed.length} confirmed{suggested.length > 0 ? `, ${suggested.length} suggested` : ""})
                                </span>
                            )}
                        </CardTitle>
                        <div className="flex items-center gap-2">
                            <Button
                                variant="outline"
                                size="sm"
                                className="h-7 text-xs"
                                onClick={handleDetect}
                                disabled={isDetecting}
                            >
                                {isDetecting ? (
                                    <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                                ) : (
                                    <Sparkles className="h-3.5 w-3.5 mr-1.5" />
                                )}
                                Run Detection
                            </Button>
                            <Button
                                size="sm"
                                className="h-7 text-xs"
                                onClick={() => setShowAddModal(true)}
                            >
                                <Plus className="h-3.5 w-3.5 mr-1.5" />
                                Add Relationship
                            </Button>
                        </div>
                    </div>
                </CardHeader>
                <CardContent className="p-0">
                    {isLoading ? (
                        <div className="flex items-center justify-center py-8">
                            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                        </div>
                    ) : confirmed.length === 0 && suggested.length === 0 ? (
                        <div className="px-4 py-8 text-center">
                            <p className="text-sm text-muted-foreground mb-2">
                                No logical relationships found
                            </p>
                            <p className="text-xs text-muted-foreground">
                                Click "Run Detection" to discover candidates, or "Add Relationship" to create one manually.
                            </p>
                        </div>
                    ) : (
                        <div className="divide-y">
                            {/* Suggested — shown first with action buttons */}
                            {suggested.map((fk) => {
                                const info = fkLabel(fk);
                                return (
                                    <div
                                        key={fk.logicalFkId}
                                        className="px-4 py-3 hover:bg-amber-50/30 dark:hover:bg-amber-950/10 transition-colors"
                                    >
                                        <div className="flex items-center justify-between gap-3">
                                            <div className="flex items-center gap-2 min-w-0 flex-1">
                                                <Badge
                                                    variant="outline"
                                                    className="text-[10px] h-5 px-1.5 border-amber-300 text-amber-600 dark:text-amber-400 shrink-0"
                                                >
                                                    ?
                                                </Badge>
                                                <code className="text-xs font-mono text-foreground bg-muted px-1.5 py-0.5 rounded truncate">
                                                    {info.myColumns}
                                                </code>
                                                <span className="text-muted-foreground text-xs shrink-0">
                                                    {info.direction}
                                                </span>
                                                <code className="text-xs font-mono text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/30 px-1.5 py-0.5 rounded border border-amber-200 dark:border-amber-900 truncate">
                                                    {info.otherTable}.{info.otherColumns}
                                                </code>
                                            </div>
                                            <div className="flex items-center gap-1.5 shrink-0">
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="h-7 px-2 text-green-600 hover:text-green-700 hover:bg-green-50 dark:hover:bg-green-950/30"
                                                    onClick={() => {
                                                        const fkId = fk.logicalFkId;
                                                        setInflightFkId(fkId);
                                                        confirmMutation.mutate(
                                                            { id: fkId },
                                                            {
                                                                onSettled: () => setInflightFkId(null),
                                                            }
                                                        );
                                                    }}
                                                    disabled={inflightFkId === fk.logicalFkId}
                                                >
                                                    <Check className="h-3.5 w-3.5 mr-1" />
                                                    Confirm
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="h-7 px-2 text-destructive hover:bg-destructive/10"
                                                    onClick={() => {
                                                        const fkId = fk.logicalFkId;
                                                        setInflightFkId(fkId);
                                                        rejectMutation.mutate(
                                                            { id: fkId },
                                                            {
                                                                onSuccess: () => {
                                                                    toast("Relationship rejected", {
                                                                        duration: 8000,
                                                                        action: {
                                                                            label: "Undo",
                                                                            onClick: () => {
                                                                                confirmMutation.mutate({ id: fkId });
                                                                            },
                                                                        },
                                                                        icon: <Undo2 className="h-4 w-4" />,
                                                                    });
                                                                },
                                                                onSettled: () => setInflightFkId(null),
                                                            }
                                                        );
                                                    }}
                                                    disabled={inflightFkId === fk.logicalFkId}
                                                >
                                                    <X className="h-3.5 w-3.5 mr-1" />
                                                    Reject
                                                </Button>
                                            </div>
                                        </div>
                                        {/* Meta row */}
                                        <div className="flex items-center gap-3 mt-1.5 ml-7 text-[11px] text-muted-foreground">
                                            <span>
                                                Confidence: {Math.round(fk.confidenceScore * 100)}%
                                            </span>
                                            <span>•</span>
                                            <span>Method: {fk.discoveryMethod.toLowerCase()}</span>
                                        </div>
                                    </div>
                                );
                            })}

                            {/* Confirmed */}
                            {confirmed.map((fk) => {
                                const info = fkLabel(fk);

                                return (
                                    <div
                                        key={fk.logicalFkId}
                                        className="px-4 py-3 hover:bg-muted/20 transition-colors"
                                    >
                                        <div className="flex items-center justify-between gap-3">
                                            <div className="flex items-center gap-2 min-w-0 flex-1">
                                                <Badge
                                                    variant="outline"
                                                    className="text-[10px] h-5 px-1.5 border-green-300 text-green-600 dark:text-green-400 shrink-0"
                                                >
                                                    ✓
                                                </Badge>
                                                <code className="text-xs font-mono text-foreground bg-muted px-1.5 py-0.5 rounded truncate">
                                                    {info.myColumns}
                                                </code>
                                                <span className="text-muted-foreground text-xs shrink-0">
                                                    {info.direction}
                                                </span>
                                                <code className="text-xs font-mono text-green-600 dark:text-green-400 bg-green-50 dark:bg-green-950/30 px-1.5 py-0.5 rounded border border-green-200 dark:border-green-900 truncate">
                                                    {info.otherTable}.{info.otherColumns}
                                                </code>
                                            </div>
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                className="h-7 px-2 text-muted-foreground hover:text-destructive"
                                                aria-label={`Delete relationship to ${info.otherTable}`}
                                                title={`Delete relationship to ${info.otherTable}`}
                                                onClick={() => {
                                                    const fkId = fk.logicalFkId;
                                                    setInflightFkId(fkId);
                                                    deleteMutation.mutate(
                                                        { id: fkId },
                                                        {
                                                            onSettled: () => setInflightFkId(null),
                                                        }
                                                    );
                                                }}
                                                disabled={inflightFkId === fk.logicalFkId}
                                            >
                                                <Trash2 className="h-3.5 w-3.5" />
                                            </Button>
                                        </div>
                                        {/* Meta row */}
                                        <div className="flex items-center gap-3 mt-1.5 ml-7 text-[11px] text-muted-foreground">
                                            {fk.confirmedAt && (
                                                <span>
                                                    Confirmed{" "}
                                                    {utcToLocal(fk.confirmedAt, "PPP")}
                                                </span>
                                            )}
                                            {fk.notes && (
                                                <>
                                                    <span>•</span>
                                                    <span className="italic truncate">{fk.notes}</span>
                                                </>
                                            )}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </CardContent>
            </Card>

            {/* Add Relationship Modal */}
            {showAddModal && (
                <AddRelationshipModal
                    projectId={normalizedProjectId}
                    sourceTableId={normalizedTableId}
                    sourceTableName={tableName}
                    sourceColumns={columns}
                    onClose={() => setShowAddModal(false)}
                    onSuccess={() =>
                        queryClient.invalidateQueries({ queryKey: logicalFkTableQueryKey })
                    }
                />
            )}
        </div>
    );
}
