import { Code2, Plus, Search } from "lucide-react";
import { Button } from "../ui/button";

export function EmptyView({ hasFilters, onClear, onCreate }: { hasFilters: boolean; onClear: () => void; onCreate: () => void }) {
    return (
        <div className="flex flex-col items-center justify-center h-full min-h-[400px] text-center px-6 py-16 animate-in fade-in duration-500">
            <div className="w-16 h-16 bg-muted/40 rounded-2xl flex items-center justify-center mb-5 border border-border/50 shadow-sm">
                {hasFilters ? <Search className="h-6 w-6 text-muted-foreground/60" /> : <Code2 className="h-6 w-6 text-muted-foreground/60" />}
            </div>
            <h3 className="text-lg font-semibold text-foreground mb-2">
                {hasFilters ? "No matching snippets" : "Your Library is Empty"}
            </h3>
            <p className="text-sm text-muted-foreground max-w-[300px] mb-6 leading-relaxed">
                {hasFilters ? "Try removing some filters or adjusting your search terms." : "Start building your team's knowledge base by creating your first code snippet."}
            </p>
            {hasFilters ? (
                <Button variant="secondary" onClick={onClear}>Clear all filters</Button>
            ) : (
                <Button onClick={onCreate} className="shadow-sm">
                    <Plus className="h-4 w-4 mr-2" /> Create Snippet
                </Button>
            )}
        </div>
    );
}