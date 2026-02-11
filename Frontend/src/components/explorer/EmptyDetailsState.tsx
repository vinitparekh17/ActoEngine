// components/explorer/EmptyDetailsState.tsx
import { Database, Lightbulb, Users, FileText } from "lucide-react";

/**
 * Empty state shown in the right panel when no entity is selected.
 * Provides helpful tips and quick actions.
 */
export function EmptyDetailsState() {
  return (
    <div className="flex flex-col items-center justify-center h-full p-8 text-center">
      <div className="rounded-full bg-muted p-4 mb-6">
        <Database className="h-8 w-8 text-muted-foreground" />
      </div>

      <h2 className="text-xl font-semibold mb-2">Entity Explorer</h2>
      <p className="text-muted-foreground mb-8 max-w-sm">
        Select an entity from the left panel to view details, manage experts,
        and explore documentation.
      </p>

      <div className="grid grid-cols-1 gap-3 w-full max-w-xs">
        <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50 text-left">
          <Users className="h-5 w-5 text-blue-500 shrink-0" />
          <div>
            <p className="text-sm font-medium">Manage Experts</p>
            <p className="text-xs text-muted-foreground">
              Assign subject matter experts
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50 text-left">
          <FileText className="h-5 w-5 text-green-500 shrink-0" />
          <div>
            <p className="text-sm font-medium">Documentation</p>
            <p className="text-xs text-muted-foreground">
              Add business context and notes
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/50 text-left">
          <Lightbulb className="h-5 w-5 text-yellow-500 shrink-0" />
          <div>
            <p className="text-sm font-medium">Quick Tip</p>
            <p className="text-xs text-muted-foreground">
              Use Ctrl+K to search
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
