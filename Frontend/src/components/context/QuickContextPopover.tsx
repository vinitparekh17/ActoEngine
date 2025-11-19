// components/context/QuickContextPopover.tsx
import { useState } from 'react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Sparkles, FileText } from 'lucide-react';
import { useQuickSaveContext } from '@/hooks/useContext';

interface QuickContextPopoverProps {
  entityId: number;
  entityType: 'TABLE' | 'COLUMN' | 'SP';
  entityName: string;
  currentPurpose?: string;
  currentCriticalityLevel?: number;
  onSuccess?: () => void;
  trigger?: React.ReactNode;
}

/**
 * Renders a popover UI that lets users add or edit a short "purpose" and a criticality level for an entity.
 *
 * @param entityId - The numeric ID of the entity to document
 * @param entityType - The entity type (e.g., 'TABLE', 'COLUMN', 'SP'); used in the description text
 * @param entityName - The human-readable name shown in the popover title
 * @param currentPurpose - Optional initial purpose text to populate the textarea
 * @param currentCriticalityLevel - Optional initial criticality level (1-4) to preselect in the selector
 * @param onSuccess - Optional callback invoked after a successful save
 * @param trigger - Optional custom trigger element; when omitted a default badge trigger is used
 * @returns The Quick Context popover React element for adding or updating the entity's purpose and criticality
 */
export function QuickContextPopover({
  entityId,
  entityType,
  entityName,
  currentPurpose,
  currentCriticalityLevel,
  onSuccess,
  trigger
}: QuickContextPopoverProps) {
  const [open, setOpen] = useState(false);
  const [purpose, setPurpose] = useState(currentPurpose || '');
  const [criticalityLevel, setCriticalityLevel] = useState<number | undefined>(currentCriticalityLevel);
  
  const { mutate: quickSave, isPending } = useQuickSaveContext();

  const handleSave = () => {
    quickSave({
      entityType,
      entityId,
      purpose,
      criticalityLevel
    }, {
      onSuccess: () => {
        setOpen(false);
        onSuccess?.();
      }
    });
  };

  // Default trigger if none provided
  const defaultTrigger = currentPurpose ? (
    <Badge variant="secondary" className="cursor-pointer hover:bg-secondary/80">
      <FileText className="h-3 w-3 mr-1" />
      Documented
    </Badge>
  ) : (
    <Badge variant="outline" className="cursor-pointer hover:bg-secondary/20">
      <Sparkles className="h-3 w-3 mr-1" />
      Add context
    </Badge>
  );

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        {trigger || defaultTrigger}
      </PopoverTrigger>
      <PopoverContent className="w-96" align="start">
        <div className="space-y-4">
          <div>
            <h4 className="font-medium mb-1">Quick Context: {entityName}</h4>
            <p className="text-sm text-muted-foreground">
              Add a brief purpose to help others understand this {entityType.toLowerCase()}
            </p>
          </div>
          
          <Textarea
            placeholder="Why does this exist? What business purpose does it serve?"
            value={purpose}
            onChange={(e) => setPurpose(e.target.value)}
            className="min-h-[80px]"
            autoFocus
          />
          
          <Select 
            value={criticalityLevel?.toString() || ''} 
            onValueChange={(value) => setCriticalityLevel(value ? parseInt(value) : undefined)}
          >
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Select criticality level" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="1">Low</SelectItem>
              <SelectItem value="2">Medium</SelectItem>
              <SelectItem value="3">High</SelectItem>
              <SelectItem value="4">Critical</SelectItem>
            </SelectContent>
          </Select>
          
          <div className="flex justify-between">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setOpen(false)}
            >
              Cancel
            </Button>
            <div className="space-x-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setOpen(false);
                  // Navigate to full context editor
                  // You can add navigation logic here
                }}
              >
                Full Editor
              </Button>
              <Button
                size="sm"
                onClick={handleSave}
                disabled={!purpose || isPending}
              >
                {isPending ? 'Saving...' : 'Save'}
              </Button>
            </div>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}