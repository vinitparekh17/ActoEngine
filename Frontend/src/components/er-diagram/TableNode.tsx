/**
 * Custom table node for the ER diagram.
 * Renders a table with its columns, highlighting PKs and FKs.
 * Each column gets a source/target handle for edge connections.
 */
import { memo } from "react";
import { Handle, Position, type NodeProps } from "@xyflow/react";
import {
    KeyRound,
    Link2,
    CircleDot,
} from "lucide-react";
import type { ErColumnData } from "@/types/er-diagram";

export interface TableNodeData {
    tableId: number;
    label: string;
    schemaName?: string;
    columns: ErColumnData[];
    depth: number;
    isFocus: boolean;
    [key: string]: unknown;
}

const TableNode = memo(({ data }: NodeProps) => {
    const { label, schemaName, columns, isFocus } = data as TableNodeData;

    return (
        <div
            className={`
        rounded-lg border shadow-md bg-card text-card-foreground min-w-[220px] overflow-hidden
        ${isFocus ? "ring-2 ring-primary border-primary" : "border-border"}
      `}
        >
            {/* Table header */}
            <div
                className={`
          px-3 py-2 font-semibold text-sm border-b flex items-center gap-2
          ${isFocus ? "bg-primary/10 text-primary" : "bg-muted/50"}
        `}
            >
                <CircleDot className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">{label}</span>
                {schemaName && (
                    <span className="text-[10px] text-muted-foreground ml-auto font-normal">
                        {schemaName}
                    </span>
                )}
            </div>

            {/* Columns */}
            <div className="divide-y divide-border/50">
                {(columns as ErColumnData[]).map((col, idx) => (
                    <div
                        key={col.columnId}
                        className="relative px-3 py-1.5 text-xs flex items-center gap-2 hover:bg-muted/30 transition-colors"
                    >
                        {/* Left handle (target) */}
                        <Handle
                            type="target"
                            position={Position.Left}
                            id={`${col.columnId}-target`}
                            className="!w-2 !h-2 !bg-muted-foreground/40 !border-0 !-left-1"
                        />

                        {/* Column icon */}
                        {col.isPrimaryKey ? (
                            <KeyRound className="h-3 w-3 text-amber-500 shrink-0" />
                        ) : col.isForeignKey ? (
                            <Link2 className="h-3 w-3 text-blue-500 shrink-0" />
                        ) : (
                            <span className="w-3 shrink-0" />
                        )}

                        {/* Column name */}
                        <span
                            className={`font-mono truncate ${col.isPrimaryKey ? "font-semibold" : ""}`}
                        >
                            {col.columnName}
                        </span>

                        {/* Data type */}
                        <span className="ml-auto text-muted-foreground font-mono text-[10px] shrink-0">
                            {col.dataType}
                        </span>

                        {/* Nullable badge */}
                        {col.isNullable && (
                            <span className="text-[9px] text-muted-foreground/60">?</span>
                        )}

                        {/* Right handle (source) */}
                        <Handle
                            type="source"
                            position={Position.Right}
                            id={`${col.columnId}-source`}
                            className="!w-2 !h-2 !bg-muted-foreground/40 !border-0 !-right-1"
                        />
                    </div>
                ))}
            </div>
        </div>
    );
});

TableNode.displayName = "TableNode";
export default TableNode;
