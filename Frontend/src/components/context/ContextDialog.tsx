// components/context/ContextDialog.tsx
import { useState, useEffect } from 'react';
import { 
  Dialog, 
  DialogContent, 
  DialogDescription,
  DialogHeader, 
  DialogTitle, 
  DialogTrigger 
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { 
  Select, 
  SelectContent, 
  SelectItem, 
  SelectTrigger, 
  SelectValue 
} from '@/components/ui/select';
import { Loader2, Sparkles, ArrowRight, AlertCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useQuickSaveContext } from '@/hooks/useContext';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { useProject } from '@/hooks/useProject';
import { CriticalityLevel } from '@/types/context';

interface QuickContextDialogProps {
  entityId: string;
  entityType: 'TABLE' | 'COLUMN' | 'SP';
  entityName: string;
  currentPurpose?: string;
  currentCriticalityLevel?: CriticalityLevel;
  onSuccess?: () => void;
  trigger?: React.ReactNode;
}

/**
 * Render a dialog to add or update brief contextual documentation and data sensitivity for a database entity (table, column, or stored procedure).
 *
 * The dialog initializes fields from the provided current values when opened, validates input (entity id, selected project, and purpose length), and saves via the quick-save mutation. On successful save it closes the dialog, invalidates the project's context queries to refresh data, and invokes `onSuccess` if provided. The dialog also provides a link to open the full editor for the entity.
 *
 * @param props.entityId - Numeric identifier of the target entity (string form accepted); shown/used to route and saved payloads.
 * @param props.entityType - One of 'TABLE', 'COLUMN', or 'SP' to determine placeholders, available fields, and routing for the full editor.
 * @param props.entityName - Display name for the dialog title.
 * @param props.currentPurpose - Optional initial purpose text to prefill the purpose field when the dialog opens.
 * @param props.currentCriticalityLevel - Optional initial sensitivity level (1–5) to prefill the sensitivity select; defaults to 1.
 * @param props.onSuccess - Optional callback invoked after a successful save.
 * @param props.trigger - Optional custom trigger element; when omitted a default "Quick Context" button is rendered.
 * @returns The QuickContextDialog React element.
 */
export function QuickContextDialog({
  entityId,
  entityType,
  entityName,
  currentPurpose,
  currentCriticalityLevel = 1,
  onSuccess,
  trigger
}: QuickContextDialogProps) {
  const [open, setOpen] = useState(false);
  const [purpose, setPurpose] = useState('');
  const [sensitivity, setSensitivity] = useState<CriticalityLevel>(1);
  const [validationError, setValidationError] = useState<string | null>(null);

  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { selectedProjectId } = useProject();

  const { mutate: quickSave, isPending } = useQuickSaveContext();

  // Reset form when dialog opens
  useEffect(() => {
    if (open) {
      setPurpose(currentPurpose || '');
      setSensitivity(currentCriticalityLevel || 1);
      setValidationError(null); // Clear any previous errors
    }
  }, [open, currentPurpose, currentCriticalityLevel]);

  const handleSave = () => {
    const parsedId = parseInt(entityId, 10);
    if (isNaN(parsedId)) {
      console.error('Invalid entity ID');
      setValidationError('Invalid entity ID. Please close and try again, or contact support.');
      return;
    }

    // Clear any previous errors
    setValidationError(null);

    // Guard: only save when project is selected
    if (!selectedProjectId) {
      setValidationError('No project selected. Please select a project and try again.');
      return;
    }

    quickSave({
      entityType,
      entityId: parsedId,
      purpose,
      criticalityLevel: sensitivity
    }, {
      onSuccess: () => {
        setOpen(false);
        // Invalidate context queries for the selected project
        queryClient.invalidateQueries({ 
          queryKey: [`/projects/${selectedProjectId}/context`] 
        });
        onSuccess?.();
      }
    });
  };

  const handleOpenFullEditor = () => {
    if (!selectedProjectId) return;
    
    setOpen(false);
    // Navigate to detail page
    const route = entityType === 'TABLE' 
      ? `/project/${selectedProjectId}/tables/${entityId}`
      : entityType === 'SP'
      ? `/project/${selectedProjectId}/stored-procedures/${entityId}` 
      : `/project/${selectedProjectId}/columns/${entityId}`;
    navigate(route);
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger || (
          <Button variant="outline" size="sm">
            <Sparkles className="h-3 w-3 mr-1" />
            Quick Context
          </Button>
        )}
      </DialogTrigger>
      
      <DialogContent className="sm:max-w-[525px]">
        <DialogHeader>
          <DialogTitle>Quick Context: {entityName}</DialogTitle>
          <DialogDescription>
            Add essential documentation to help your team understand this {entityType.toLowerCase()}.
            {currentPurpose && (
              <span className="block mt-1 text-xs text-green-600">
                ✓ Already has documentation - you're updating it
              </span>
            )}
          </DialogDescription>
        </DialogHeader>

        {/* Validation Error Alert */}
        {validationError && (
          <Alert variant="destructive" className="mt-4">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{validationError}</AlertDescription>
          </Alert>
        )}

        <div className="grid gap-4 py-4">
          {/* Purpose Field */}
          <div className="grid gap-2">
            <Label htmlFor="purpose">
              Purpose <span className="text-muted-foreground text-xs ml-1">(Required)</span>
            </Label>
            <Textarea
              id="purpose"
              placeholder={
                entityType === 'TABLE' 
                  ? "What business data does this table store? Why is it needed?"
                  : entityType === 'COLUMN'
                  ? "What does this column represent? How is it used?"
                  : "What business logic does this procedure implement?"
              }
              value={purpose}
              onChange={(e) => setPurpose(e.target.value)}
              className="min-h-[100px] resize-none"
              autoFocus
            />
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>{purpose.length} characters</span>
              <span>{purpose.length < 10 ? 'Too short' : purpose.length > 500 ? 'Consider being concise' : 'Good length'}</span>
            </div>
          </div>
          
          {/* Sensitivity Field (mainly for columns) */}
          {(entityType === 'COLUMN' || entityType === 'TABLE') && (
            <div className="grid gap-2">
              <Label htmlFor="sensitivity">
                Data Sensitivity
              </Label>
              <Select 
                value={sensitivity.toString()} 
                onValueChange={(val) => {
                  const parsed = parseInt(val, 10);
                  // Validate: must be finite integer between 1 and 5
                  if (!isNaN(parsed) && parsed >= 1 && parsed <= 5) {
                    setSensitivity(parsed as CriticalityLevel);
                  } else {
                    console.error('Invalid criticality level:', val);
                  }
                }}
              >
                <SelectTrigger id="sensitivity">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">
                    <div className="flex flex-col items-start">
                      <span className="font-medium">Public</span>
                      <span className="text-xs text-muted-foreground">No restrictions</span>
                    </div>
                  </SelectItem>
                  <SelectItem value="2">
                    <div className="flex flex-col items-start">
                      <span className="font-medium">Internal</span>
                      <span className="text-xs text-muted-foreground">Company use only</span>
                    </div>
                  </SelectItem>
                  <SelectItem value="3">
                    <div className="flex flex-col items-start">
                      <span className="font-medium">PII</span>
                      <span className="text-xs text-muted-foreground">Personal identifiable information</span>
                    </div>
                  </SelectItem>
                  <SelectItem value="4">
                    <div className="flex flex-col items-start">
                      <span className="font-medium">Financial</span>
                      <span className="text-xs text-muted-foreground">Financial/payment data</span>
                    </div>
                  </SelectItem>
                  <SelectItem value="5">
                    <div className="flex flex-col items-start">
                      <span className="font-medium">Sensitive</span>
                      <span className="text-xs text-muted-foreground">Highly restricted access</span>
                    </div>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}
        </div>
        
        <div className="flex items-center justify-between">
          <Button
            variant="link"
            size="sm"
            onClick={handleOpenFullEditor}
            className="text-muted-foreground"
          >
            Open full editor
            <ArrowRight className="h-3 w-3 ml-1" />
          </Button>
          
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
              disabled={!purpose.trim() || purpose.length < 10 || isPending}
            >
              {isPending ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Saving...
                </>
              ) : (
                'Save Context'
              )}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}