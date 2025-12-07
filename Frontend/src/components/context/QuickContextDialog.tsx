// components/context/QuickContextDialog.tsx
import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Loader2, Sparkles } from "lucide-react";
import { toast } from "sonner";
import { useQuickSaveContext } from "@/hooks/useContext";
import { useQueryClient } from "@tanstack/react-query";

interface QuickContextDialogProps {
  entityId: string;
  entityType: "TABLE" | "COLUMN" | "SP";
  entityName: string;
  currentPurpose?: string;
  currentSensitivity?: string;
  onSuccess?: () => void;
  trigger?: React.ReactNode;
}

export function QuickContextDialog({
  entityId,
  entityType,
  entityName,
  currentPurpose,
  currentSensitivity,
  onSuccess,
  trigger,
}: QuickContextDialogProps) {
  const [open, setOpen] = useState(false);
  const [purpose, setPurpose] = useState(currentPurpose || "");
  const [sensitivity, setSensitivity] = useState(
    currentSensitivity || "PUBLIC",
  );
  const queryClient = useQueryClient();

  const { mutate: quickSave, isPending } = useQuickSaveContext();

  const handleSave = () => {
    const sensitivityToLevel = (s: string) => {
      switch (s) {
        case "INTERNAL":
          return 1;
        case "PII":
          return 2;
        case "FINANCIAL":
          return 3;
        case "SENSITIVE":
          return 4;
        case "PUBLIC":
        default:
          return 0;
      }
    };

    const numericId = parseInt(entityId, 10);
    if (!Number.isInteger(numericId) || numericId <= 0) {
      console.error("Invalid entity ID:", entityId);
      toast.error("Invalid item selected. Please try again.");
      return;
    }

    quickSave(
      {
        entityId: numericId,
        entityType,
        purpose,
        criticalityLevel: sensitivityToLevel(sensitivity),
      },
      {
        onSuccess: () => {
          setOpen(false);
          // Invalidate context queries
          queryClient.invalidateQueries({ queryKey: ["context"] });
          onSuccess?.();
        },
      },
    );
  };

  // Reset form when dialog opens
  const handleOpenChange = (newOpen: boolean) => {
    if (newOpen) {
      setPurpose(currentPurpose || "");
      setSensitivity(currentSensitivity || "PUBLIC");
    }
    setOpen(newOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        {trigger || (
          <Button variant="outline" size="sm">
            <Sparkles className="h-3 w-3 mr-1" />
            Quick Context
          </Button>
        )}
      </DialogTrigger>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Quick Context: {entityName}</DialogTitle>
          <DialogDescription>
            Add essential documentation to help your team understand this{" "}
            {entityType.toLowerCase()}.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="purpose">
              Purpose{" "}
              <span className="text-muted-foreground text-xs">(Required)</span>
            </Label>
            <Textarea
              id="purpose"
              placeholder={`Why does this ${entityType.toLowerCase()} exist? What business need does it serve?`}
              value={purpose}
              onChange={(e) => setPurpose(e.target.value)}
              className="min-h-[100px] resize-none"
              autoFocus
            />
            <p className="text-xs text-muted-foreground">
              {purpose.length}/500 characters
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="sensitivity">Data Sensitivity</Label>
            <Select value={sensitivity} onValueChange={setSensitivity}>
              <SelectTrigger id="sensitivity">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="PUBLIC">
                  <div className="flex flex-col">
                    <span>Public</span>
                    <span className="text-xs text-muted-foreground">
                      No restrictions
                    </span>
                  </div>
                </SelectItem>
                <SelectItem value="INTERNAL">
                  <div className="flex flex-col">
                    <span>Internal</span>
                    <span className="text-xs text-muted-foreground">
                      Company use only
                    </span>
                  </div>
                </SelectItem>
                <SelectItem value="PII">
                  <div className="flex flex-col">
                    <span>PII</span>
                    <span className="text-xs text-muted-foreground">
                      Personal information
                    </span>
                  </div>
                </SelectItem>
                <SelectItem value="FINANCIAL">
                  <div className="flex flex-col">
                    <span>Financial</span>
                    <span className="text-xs text-muted-foreground">
                      Financial data
                    </span>
                  </div>
                </SelectItem>
                <SelectItem value="SENSITIVE">
                  <div className="flex flex-col">
                    <span>Sensitive</span>
                    <span className="text-xs text-muted-foreground">
                      Highly restricted
                    </span>
                  </div>
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div className="flex justify-between">
          <div className="flex gap-2">
            <Button
              variant="outline"
              onClick={() => setOpen(false)}
              disabled={isPending}
            >
              Cancel
            </Button>
            <Button
              onClick={handleSave}
              disabled={!purpose.trim() || isPending}
            >
              {isPending ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Saving...
                </>
              ) : (
                "Save Context"
              )}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
