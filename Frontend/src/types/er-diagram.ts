/**
 * ER Diagram Types
 *
 * Frontend types matching the backend ErDiagram models.
 * Backend source: Features/ErDiagram/ErDiagramController.cs
 */

export interface ErDiagramResponse {
    focusTableId: number;
    nodes: ErNodeData[];
    edges: ErEdgeData[];
}

export interface ErNodeData {
    tableId: number;
    tableName: string;
    schemaName?: string;
    columns: ErColumnData[];
    depth: number;
}

export interface ErColumnData {
    columnId: number;
    columnName: string;
    dataType: string;
    isPrimaryKey: boolean;
    isForeignKey: boolean;
    isNullable: boolean;
}

export interface ErEdgeData {
    id: string;
    sourceTableId: number;
    sourceColumnId: number;
    sourceColumnName: string;
    targetTableId: number;
    targetColumnId: number;
    targetColumnName: string;
    relationshipType: "PHYSICAL" | "LOGICAL";
    status?: "SUGGESTED" | "CONFIRMED" | "REJECTED";
    confidenceScore?: number;
    logicalFkId?: number;
    discoveryMethod?: string;
    confirmedAt?: string;
    confirmedBy?: number;
    createdAt?: string;
}

export interface LogicalFkDto {
    logicalFkId: number;
    projectId: number;
    sourceTableId: number;
    sourceTableName: string;
    sourceColumnIds: number[];
    sourceColumnNames: string[];
    targetTableId: number;
    targetTableName: string;
    targetColumnIds: number[];
    targetColumnNames: string[];
    discoveryMethod: string;
    confidenceScore: number;
    status: "SUGGESTED" | "CONFIRMED" | "REJECTED";
    confirmedBy?: number;
    confirmedAt?: string;
    notes?: string;
    createdAt: string;
}

export interface LogicalFkCandidate {
    sourceTableId: number;
    sourceTableName: string;
    sourceColumnId: number;
    sourceColumnName: string;
    sourceDataType: string;
    targetTableId: number;
    targetTableName: string;
    targetColumnId: number;
    targetColumnName: string;
    targetDataType: string;
    confidenceScore: number;
    reason: string;
    isAmbiguous?: boolean;
}

export interface TableListItem {
    tableId: number;
    tableName: string;
    schemaName?: string;
}

export interface PhysicalFkDto {
    foreignKeyName: string;
    sourceTableName: string;
    sourceColumnName: string;
    targetTableName: string;
    targetColumnName: string;
    onDeleteAction?: string;
    onUpdateAction?: string;
}
