/**
 * Auto-layout hook for ER diagrams using dagre.
 *
 * Dagre handles:
 *   - Hierarchical/layered placement
 *   - Edge crossing minimization
 *   - Proper spacing based on actual node dimensions
 *   - Multi-depth ring support
 *
 * We feed dagre the React Flow nodes/edges and get back positioned nodes.
 */
import Dagre from "@dagrejs/dagre";
import { type Node, type Edge } from "@xyflow/react";

/** Estimated node dimensions based on column count */
const NODE_WIDTH = 260;
const NODE_BASE_HEIGHT = 44; // header
const COLUMN_HEIGHT = 28;    // per column row
const HORIZONTAL_SPACING = 80;
const VERTICAL_SPACING = 60;

export interface LayoutOptions {
    /** Layout direction: TB (top-bottom) or LR (left-right) */
    direction: "TB" | "LR";
    /** ID of the focus node — will be pinned to a central position */
    focusNodeId?: string;
}

/**
 * Apply dagre layout to a set of nodes and edges.
 * Returns new nodes with updated positions (edges stay the same).
 */
export function getLayoutedElements(
    nodes: Node[],
    edges: Edge[],
    options: LayoutOptions = { direction: "LR" }
): { nodes: Node[]; edges: Edge[] } {
    if (nodes.length === 0) return { nodes, edges };

    const g = new Dagre.graphlib.Graph({ directed: true, compound: false, multigraph: false });

    g.setGraph({
        rankdir: options.direction,
        nodesep: HORIZONTAL_SPACING,
        ranksep: VERTICAL_SPACING + 40,
        edgesep: 30,
        marginx: 40,
        marginy: 40,
        // Dagre rank assignment aligns with our depth concept
        ranker: "network-simplex",
    });

    g.setDefaultEdgeLabel(() => ({}));

    // Add nodes with estimated dimensions
    for (const node of nodes) {
        const data = node.data as { columns?: unknown[] };
        const columnCount = Array.isArray(data.columns) ? data.columns.length : 0;
        const height = NODE_BASE_HEIGHT + columnCount * COLUMN_HEIGHT;

        g.setNode(node.id, { width: NODE_WIDTH, height });
    }

    // Add edges
    for (const edge of edges) {
        g.setEdge(edge.source, edge.target);
    }

    // Run layout
    Dagre.layout(g);

    // Apply computed positions back to nodes
    const layoutedNodes = nodes.map((node) => {
        const nodeWithPosition = g.node(node.id);
        if (!nodeWithPosition) return node;

        const data = node.data as { columns?: unknown[] };
        const columnCount = Array.isArray(data.columns) ? data.columns.length : 0;
        const height = NODE_BASE_HEIGHT + columnCount * COLUMN_HEIGHT;

        return {
            ...node,
            position: {
                // dagre returns center coordinates, React Flow uses top-left
                x: nodeWithPosition.x - NODE_WIDTH / 2,
                y: nodeWithPosition.y - height / 2,
            },
        };
    });

    return { nodes: layoutedNodes, edges };
}
