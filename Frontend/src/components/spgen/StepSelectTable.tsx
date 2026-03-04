import { useState } from "react";
import { Database, ArrowRight } from "lucide-react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import TreeView from "@/components/database/TreeView";
import type { TreeNode } from "@/schema/spBuilderSchema";

export default function StepSelectTable({
    treeData,
    selectedTable,
    onSelect,
    isLoading,
    onNext,
}: {
    treeData: TreeNode[];
    selectedTable: string | null;
    onSelect: (node: TreeNode) => void;
    isLoading: boolean;
    onNext: () => void;
}) {
    const [treeSearch, setTreeSearch] = useState("");

    return (
        <div className="flex flex-col h-[calc(100vh-200px)] max-w-3xl mx-auto w-full p-4 gap-3 animate-in fade-in slide-in-from-bottom-4 duration-500">

            <Card className="flex-1 min-h-0 h-[calc(100vh-200px)] flex flex-col overflow-hidden border-border/40 shadow-sm rounded-2xl bg-card">
                <div className="py-2 px-4 border-b border-border/40 flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                        <div className="p-1.5 bg-primary/10 rounded-md">
                            <Database className="h-4 w-4 text-primary" />
                        </div>
                        <span className="text-sm font-semibold">Table Explorer</span>
                    </div>
                </div>
                <div className="flex-1 overflow-auto p-3">
                    <TreeView
                        treeData={treeData}
                        onSelectNode={onSelect}
                        searchQuery={treeSearch}
                        onSearchChange={setTreeSearch}
                        isLoading={isLoading}
                    />
                </div>
            </Card>

            <div className="flex justify-end shrink-0">
                <Button
                    size="lg"
                    onClick={onNext}
                    disabled={!selectedTable}
                    className="gap-2 rounded-xl px-8 h-12 shadow-sm transition-all hover:scale-[1.02] active:scale-[0.98]"
                >
                    Continue to Configuration
                    <ArrowRight className="w-4 h-4" />
                </Button>
            </div>
        </div>
    );
}
