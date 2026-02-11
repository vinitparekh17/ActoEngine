import { Tree, type NodeApi } from "react-arborist";
import { Input } from "../ui/input";
import { Badge } from "../ui/badge";
import { Skeleton } from "../ui/skeletons";
import {
  Folder,
  Table,
  Search,
  Columns3,
  Key,
  Code2,
  FunctionSquare,
  Table2,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";

export type TreeNode = {
  id: string;
  name: string;
  children?: TreeNode[];
  entityId?: number;
  type?:
    | "database"
    | "tables-folder"
    | "programmability-folder"
    | "table"
    | "column"
    | "index"
    | "stored-procedures-folder"
    | "functions-folder"
    | "stored-procedure"
    | "scalar-function"
    | "table-function";
};

function highlight(text: string, query: string) {
  if (!query) return <span>{text}</span>;
  const i = text.toLowerCase().indexOf(query.toLowerCase());
  if (i === -1) return <span>{text}</span>;
  return (
    <span>
      {text.slice(0, i)}
      <span className="bg-yellow-200 dark:bg-yellow-600/60 rounded px-0.5">
        {text.slice(i, i + query.length)}
      </span>
      {text.slice(i + query.length)}
    </span>
  );
}

function getNodeIcon(type: TreeNode["type"]) {
  switch (type) {
    case "database":
      return <Folder className="h-4 w-4 text-blue-500" />;
    case "tables-folder":
      return <Folder className="h-4 w-4 text-amber-500" />;
    case "programmability-folder":
      return <Folder className="h-4 w-4 text-purple-500" />;
    case "table":
      return <Table className="h-4 w-4 text-green-600" />;
    case "column":
      return <Columns3 className="h-4 w-4 text-gray-500" />;
    case "index":
      return <Key className="h-4 w-4 text-orange-500" />;
    case "stored-procedures-folder":
      return <Folder className="h-4 w-4 text-indigo-500" />;
    case "functions-folder":
      return <Folder className="h-4 w-4 text-pink-500" />;
    case "stored-procedure":
      return <Code2 className="h-4 w-4 text-indigo-600" />;
    case "scalar-function":
      return <FunctionSquare className="h-4 w-4 text-pink-600" />;
    case "table-function":
      return <Table2 className="h-4 w-4 text-pink-600" />;
    default:
      return <Folder className="h-4 w-4 text-muted-foreground" />;
  }
}

function getNodeLabel(node: TreeNode): string {
  switch (node.type) {
    case "tables-folder":
      return "Tables";
    case "programmability-folder":
      return "Programmability";
    case "stored-procedures-folder":
      return "Stored Procedures";
    case "functions-folder":
      return "Functions";
    default:
      return node.name;
  }
}

export default function TreeView({
  treeData,
  onSelectNode,
  searchQuery,
  onSearchChange,
  isLoading = false,
  persistenceKey,
  hideSearch = false,
}: {
  treeData: TreeNode[];
  onSelectNode: (node: TreeNode) => void;
  searchQuery: string;
  onSearchChange: (q: string) => void;
  isLoading?: boolean;
  persistenceKey?: string;
  hideSearch?: boolean;
}) {
  // Persistence for expanded nodes
  const [expandedNodes, setExpandedNodes] = useState<Record<string, boolean>>(
    {},
  );

  // Load initial state from local storage once on mount
  useEffect(() => {
    if (persistenceKey) {
      try {
        const stored = window.localStorage.getItem(persistenceKey);
        if (stored) {
          setExpandedNodes(JSON.parse(stored));
        }
      } catch (e) {
        console.warn("Failed to load tree state", e);
      }
    }
  }, [persistenceKey]);

  // Save state when nodes are toggled
  const handleToggle = (id: string) => {
    if (!persistenceKey) return;

    setExpandedNodes((prev: Record<string, boolean>) => {
      const next = { ...prev, [id]: !prev[id] };
      try {
        window.localStorage.setItem(persistenceKey, JSON.stringify(next));
      } catch (error) {
        console.warn(
          `Failed to persist tree state for key "${persistenceKey}":`,
          error,
        );
      }
      return next;
    });
  };

  // Recursive filter function that preserves parent nodes if any descendant matches
  function filterTree(nodes: TreeNode[]): TreeNode[] {
    return nodes
      .map((node) => {
        if (node.children && node.children.length > 0) {
          const filteredChildren = filterTree(node.children);

          // If this node has matching children, include it with those children
          if (filteredChildren.length > 0) {
            return { ...node, children: filteredChildren };
          }

          // If this is a folder node without matching children, check if the node itself matches
          if (node.type?.includes("folder")) {
            return null;
          }
        }

        // For leaf nodes, check if they match the search query
        if (
          !searchQuery ||
          node.name.toLowerCase().includes(searchQuery.toLowerCase())
        ) {
          return node;
        }

        return null;
      })
      .filter((node): node is TreeNode => node !== null);
  }

  const filteredTreeData = searchQuery ? filterTree(treeData) : treeData;

  // Ref to store the tree instance
  const treeRef = useRef<any>(null);

  // Auto-expand all nodes when searching
  useEffect(() => {
    if (treeRef.current && searchQuery) {
      // Open all nodes when there's a search query
      treeRef.current.openAll();

      // Sync programmatic expansions to expandedNodes state for persistence
      // Note: openAll() is programmatic and doesn't trigger handleToggle,
      // so we manually update expandedNodes to reflect the actual open state
      if (persistenceKey) {
        const collectAllIds = (nodes: TreeNode[]): string[] => {
          const ids: string[] = [];
          nodes.forEach((node) => {
            ids.push(node.id);
            if (node.children) {
              ids.push(...collectAllIds(node.children));
            }
          });
          return ids;
        };

        const allIds = collectAllIds(filteredTreeData);
        const allOpen = allIds.reduce(
          (acc, id) => ({ ...acc, [id]: true }),
          {},
        );
        setExpandedNodes(allOpen);

        try {
          window.localStorage.setItem(persistenceKey, JSON.stringify(allOpen));
        } catch (error) {
          console.warn(
            `Failed to persist expanded state during search:`,
            error,
          );
        }
      }
    } else if (treeRef.current && !searchQuery && persistenceKey) {
      // When search is cleared, restore persisted state
      // Close all first, then re-apply persisted state
      treeRef.current.closeAll();

      // Re-open nodes that were persisted as open
      Object.entries(expandedNodes).forEach(([id, isOpen]) => {
        if (isOpen) {
          const node = treeRef.current?.get(id);
          if (node && !node.isOpen) {
            node.open();
          }
        }
      });
    }
  }, [searchQuery, filteredTreeData, persistenceKey]);

  function getChildCount(node: TreeNode): number {
    if (!node.children) return 0;

    switch (node.type) {
      case "database":
        return node.children.filter(
          (c) =>
            c.type === "tables-folder" || c.type === "programmability-folder",
        ).length;
      case "tables-folder":
        return node.children.filter((c) => c.type === "table").length;
      case "table":
        return node.children.filter(
          (c) => c.type === "column" || c.type === "index",
        ).length;
      case "programmability-folder":
        return node.children.length;
      case "stored-procedures-folder":
        return node.children.filter((c) => c.type === "stored-procedure")
          .length;
      case "functions-folder":
        return node.children.filter(
          (c) => c.type === "scalar-function" || c.type === "table-function",
        ).length;
      default:
        return node.children.length;
    }
  }

  return (
    <div className="border rounded-xl w-full p-2 bg-card">
      {!hideSearch && (
        <div className="relative mb-2">
          <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Search database objects..."
            className="pl-8 rounded-lg"
          />
        </div>
      )}

      {isLoading ? (
        <div className="space-y-2 p-2">
          <Skeleton className="h-6 w-3/4" />
          <Skeleton className="h-6 w-2/3" />
          <Skeleton className="h-6 w-1/2" />
        </div>
      ) : (
        <Tree
          ref={treeRef}
          data={filteredTreeData}
          openByDefault={false}
          initialOpenState={expandedNodes}
          onToggle={handleToggle}
          rowHeight={28}
          width={"100%"}
          indent={24}
          className="text-sm"
          selectionFollowsFocus
          onActivate={(node: NodeApi<TreeNode>) => {
            const n = node.data;
            // Toggle if it has children, otherwise select
            if (n.children && n.children.length > 0) {
              node.toggle();
            } else {
              onSelectNode(n);
            }
          }}
        >
          {({ node, style, dragHandle }) => {
            const n = node.data as TreeNode;
            const selected = node.isSelected;
            const childCount = getChildCount(n);
            const label = getNodeLabel(n);

            return (
              <div style={style} ref={dragHandle} className="flex items-center">
                <div
                  className={
                    "flex items-center gap-2 px-2 py-1 rounded flex-1 " +
                    (selected
                      ? "bg-blue-50 dark:bg-blue-500/10"
                      : "hover:bg-accent/50")
                  }
                  aria-selected={selected}
                >
                  {getNodeIcon(n.type)}
                  <span className="flex-1 truncate">
                    {highlight(label, searchQuery)}
                  </span>
                  {childCount > 0 && (
                    <Badge variant="outline" className="text-xs">
                      {childCount}
                    </Badge>
                  )}
                </div>
              </div>
            );
          }}
        </Tree>
      )}
    </div>
  );
}
