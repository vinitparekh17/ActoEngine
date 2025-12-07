// Frontend/src/components/context/ContextEditorPanel.tsx
import React, { useState, useEffect, useCallback, useRef } from "react";
import { formatRelativeTime } from "@/lib/utils";
import { debounce } from "lodash";
import { Card, CardHeader, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Progress } from "@/components/ui/progress";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
  TooltipProvider,
} from "@/components/ui/tooltip";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import {
  Info,
  Check,
  AlertCircle,
  Edit,
  X,
  AlertTriangle,
  Plus,
} from "lucide-react";
import { toast } from "sonner";
import { useApi, useApiPut } from "@/hooks/useApi";
import { useAuthorization } from "../../hooks/useAuth";

// Types
interface ContextData {
  context: {
    purpose?: string;
    businessImpact?: string;
    businessDomain?: string;
    dataOwner?: string;
    criticalityLevel?: number;
    sensitivity?: string;
    reviewedBy?: string;
  };
  completenessScore: number;
  isStale: boolean;
  suggestions?: {
    purpose?: string;
  };
  experts?: Array<{
    userId: string;
    name: string;
    email: string;
    expertiseLevel: string;
  }>;
  lastReviewed?: string;
}

interface ContextEditorProps {
  projectId: number;
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
  isReadOnly?: boolean;
}

export const ContextEditor: React.FC<ContextEditorProps> = ({
  projectId,
  entityType,
  entityId,
  entityName,
  isReadOnly = false,
}) => {
  const canUpdate = useAuthorization("Contexts:Update");
  const effectiveReadOnly = isReadOnly || !canUpdate;

  const [isEditing, setIsEditing] = useState(false);
  const [localContext, setLocalContext] = useState<ContextData["context"]>({});
  const [lastSavedContext, setLastSavedContext] = useState<
    ContextData["context"] | null
  >(null);
  const hasUnsavedChanges = useRef(false);

  const {
    data: contextData,
    isLoading,
    error,
  } = useApi<ContextData>(
    `/projects/${projectId}/context/${entityType}/${entityId}`,
    {
      staleTime: 5 * 60 * 1000,
      retry: 2,
    },
  );

  // Save mutation using your existing hook
  const { mutate: saveContext, isPending: isSaving } = useApiPut<
    ContextData,
    ContextData["context"]
  >(`/projects/${projectId}/context/${entityType}/${entityId}`, {
    showSuccessToast: false, // We'll handle this manually
    showErrorToast: true,
    onSuccess: (savedData) => {
      toast.success("Context saved");
      setLastSavedContext(savedData.context);
      hasUnsavedChanges.current = false;
      setIsEditing(false);
    },
  });

  // Stable debounced save that always calls the latest saveContext
  const saveContextRef = useRef(saveContext);
  useEffect(() => {
    saveContextRef.current = saveContext;
  }, [saveContext]);

  const debouncedSaveRef = useRef(
    debounce((data: ContextData["context"]) => {
      // call the latest saveContext from ref
      saveContextRef.current(data);
    }, 1000),
  );

  // Initialize local context and handle updates intelligently
  useEffect(() => {
    if (!contextData?.context) return;

    // Initial load - no existing context or saved state
    if (!lastSavedContext || Object.keys(localContext).length === 0) {
      setLocalContext(contextData.context);
      setLastSavedContext(contextData.context);
      return;
    }

    // Ignore updates if user is actively editing or has unsaved changes
    if (isEditing || hasUnsavedChanges.current) {
      return;
    }

    // Update only if the incoming context is different from what we last saved
    const incomingStr = JSON.stringify(contextData.context);
    const lastSavedStr = JSON.stringify(lastSavedContext);
    if (incomingStr !== lastSavedStr) {
      setLocalContext(contextData.context);
      setLastSavedContext(contextData.context);
    }
  }, [contextData?.context]);

  // Cleanup debounce on unmount
  useEffect(() => {
    return () => {
      debouncedSaveRef.current.cancel();
    };
  }, []);

  const handleChange = useCallback(
    (field: keyof ContextData["context"], value: unknown) => {
      setLocalContext((prev: ContextData["context"]) => {
        const updated = { ...prev, [field]: value };
        if (isEditing) {
          hasUnsavedChanges.current = true;
          debouncedSaveRef.current(updated);
        }
        return updated;
      });
    },
    [isEditing],
  );

  const handleCriticalityClick = useCallback(
    (level: number) => {
      handleChange("criticalityLevel", level);
    },
    [handleChange],
  );

  const completeness = contextData?.completenessScore || 0;
  const isStale = contextData?.isStale || false;

  // Error state
  if (error) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          Failed to load context: {error.message}
        </AlertDescription>
      </Alert>
    );
  }

  // Loading state
  if (isLoading) {
    return (
      <Card>
        <CardContent className="pt-6">
          <Progress value={undefined} className="w-full" />
        </CardContent>
      </Card>
    );
  }

  return (
    <TooltipProvider>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex flex-col space-y-1">
              <div className="flex items-center space-x-3">
                <h3 className="text-lg font-semibold">
                  Context & Documentation
                </h3>
                <Badge
                  variant={
                    completeness >= 80
                      ? "default"
                      : completeness >= 50
                        ? "secondary"
                        : "destructive"
                  }
                >
                  {completeness}% Complete
                </Badge>
                {isStale && (
                  <Badge
                    variant="outline"
                    className="border-orange-500 text-orange-600"
                  >
                    <AlertTriangle className="w-3 h-3 mr-1" />
                    Needs Review
                  </Badge>
                )}
                {isSaving && (
                  <Badge
                    variant="outline"
                    className="border-blue-500 text-blue-600"
                  >
                    Saving...
                  </Badge>
                )}
              </div>
              <p className="text-sm text-muted-foreground">
                Help your team understand "{entityName}"
              </p>
            </div>

            {!effectiveReadOnly && (
              <Button
                size="sm"
                variant={isEditing ? "default" : "outline"}
                onClick={() => setIsEditing(!isEditing)}
              >
                {isEditing ? (
                  <>
                    <Check className="w-4 h-4 mr-2" />
                    Done Editing
                  </>
                ) : (
                  <>
                    <Edit className="w-4 h-4 mr-2" />
                    Edit Context
                  </>
                )}
              </Button>
            )}
          </div>
        </CardHeader>

        <CardContent>
          {isStale && (
            <Alert className="mb-4 border-orange-200 bg-orange-50">
              <AlertTriangle className="h-4 w-4 text-orange-600" />
              <AlertDescription>
                The schema has changed since this context was last updated.
                Please review.
              </AlertDescription>
            </Alert>
          )}

          <div className="space-y-6">
            {/* Purpose - Always shown */}
            <div className="space-y-2">
              <Label className="flex items-center space-x-2">
                <span>Purpose</span>
                <Tooltip>
                  <TooltipTrigger>
                    <Info className="w-4 h-4 text-muted-foreground" />
                  </TooltipTrigger>
                  <TooltipContent>
                    <p>Why does this exist? What problem does it solve?</p>
                  </TooltipContent>
                </Tooltip>
              </Label>
              {isEditing ? (
                <Textarea
                  placeholder={getPurposePlaceholder(entityType)}
                  value={localContext?.purpose || ""}
                  onChange={(e) => handleChange("purpose", e.target.value)}
                  rows={3}
                />
              ) : (
                <div className="p-3 bg-muted rounded-md min-h-[60px]">
                  <p
                    className={
                      localContext?.purpose
                        ? "text-foreground"
                        : "text-muted-foreground"
                    }
                  >
                    {localContext?.purpose || "No purpose documented yet"}
                  </p>
                </div>
              )}
              {contextData?.suggestions?.purpose && !localContext?.purpose && (
                <p className="text-sm text-blue-600">
                  💡 Suggestion: {contextData.suggestions.purpose}
                </p>
              )}
            </div>

            {/* Business Impact - Critical for "what breaks" */}
            {(entityType === "TABLE" || entityType === "COLUMN") && (
              <div className="space-y-2">
                <Label className="flex items-center space-x-2">
                  <span>Business Impact</span>
                  <Tooltip>
                    <TooltipTrigger>
                      <Info className="w-4 h-4 text-muted-foreground" />
                    </TooltipTrigger>
                    <TooltipContent>
                      <p>What happens if this is changed or removed?</p>
                    </TooltipContent>
                  </Tooltip>
                </Label>
                {isEditing ? (
                  <Textarea
                    placeholder="e.g., Breaks order processing for all retail clients"
                    value={localContext?.businessImpact || ""}
                    onChange={(e) =>
                      handleChange("businessImpact", e.target.value)
                    }
                    rows={2}
                  />
                ) : (
                  <div className="p-3 bg-muted rounded-md">
                    <p
                      className={
                        localContext?.businessImpact
                          ? "text-foreground"
                          : "text-muted-foreground"
                      }
                    >
                      {localContext?.businessImpact || "Impact not documented"}
                    </p>
                  </div>
                )}
              </div>
            )}

            <Separator />

            {/* Metadata Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Business Domain */}
              {entityType === "TABLE" && (
                <div className="space-y-2">
                  <Label className="text-sm">Business Domain</Label>
                  {isEditing ? (
                    <Select
                      value={localContext?.businessDomain || ""}
                      onValueChange={(value) =>
                        handleChange("businessDomain", value)
                      }
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select domain" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="ORDERS">Orders</SelectItem>
                        <SelectItem value="FINANCE">Finance</SelectItem>
                        <SelectItem value="USERS">Users</SelectItem>
                        <SelectItem value="INVENTORY">Inventory</SelectItem>
                        <SelectItem value="REPORTING">Reporting</SelectItem>
                        <SelectItem value="INTEGRATION">Integration</SelectItem>
                      </SelectContent>
                    </Select>
                  ) : (
                    <Badge variant="secondary">
                      {localContext?.businessDomain || "Not set"}
                    </Badge>
                  )}
                </div>
              )}

              {/* Data Owner */}
              <div className="space-y-2">
                <Label className="text-sm">Data Owner</Label>
                {isEditing ? (
                  <Input
                    placeholder="Team or person name"
                    value={localContext?.dataOwner || ""}
                    onChange={(e) => handleChange("dataOwner", e.target.value)}
                  />
                ) : (
                  <p className="text-sm">
                    {localContext?.dataOwner || "Unassigned"}
                  </p>
                )}
              </div>

              {/* Criticality */}
              <div className="space-y-2">
                <Label className="text-sm">Criticality</Label>
                {isEditing ? (
                  <div className="flex space-x-2">
                    {[1, 2, 3, 4, 5].map((level) => (
                      <Badge
                        key={level}
                        variant={
                          level === localContext?.criticalityLevel
                            ? "default"
                            : "outline"
                        }
                        className={`cursor-pointer px-3 py-1 ${
                          level === localContext?.criticalityLevel && level >= 4
                            ? "bg-destructive hover:bg-destructive/90"
                            : ""
                        }`}
                        onClick={() => handleCriticalityClick(level)}
                      >
                        {level}
                      </Badge>
                    ))}
                  </div>
                ) : (
                  <Badge
                    variant={
                      (localContext?.criticalityLevel ?? 0) >= 4
                        ? "destructive"
                        : (localContext?.criticalityLevel ?? 0) >= 3
                          ? "secondary"
                          : "outline"
                    }
                  >
                    {localContext?.criticalityLevel || 3}/5
                  </Badge>
                )}
              </div>

              {/* Sensitivity - for columns */}
              {entityType === "COLUMN" && (
                <div className="space-y-2">
                  <Label className="text-sm">Sensitivity</Label>
                  {isEditing ? (
                    <Select
                      value={localContext?.sensitivity || "PUBLIC"}
                      onValueChange={(value) =>
                        handleChange("sensitivity", value)
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="PUBLIC">Public</SelectItem>
                        <SelectItem value="INTERNAL">Internal</SelectItem>
                        <SelectItem value="PII">PII</SelectItem>
                        <SelectItem value="FINANCIAL">Financial</SelectItem>
                        <SelectItem value="SENSITIVE">Sensitive</SelectItem>
                      </SelectContent>
                    </Select>
                  ) : (
                    <Badge
                      variant={
                        localContext?.sensitivity === "PII" ||
                        localContext?.sensitivity === "FINANCIAL"
                          ? "destructive"
                          : "outline"
                      }
                    >
                      {localContext?.sensitivity || "PUBLIC"}
                    </Badge>
                  )}
                </div>
              )}
            </div>

            {/* Experts Section */}
            <div className="space-y-3">
              <Label className="text-sm">Subject Matter Experts</Label>
              <div className="space-y-2">
                {contextData?.experts?.map((expert) => (
                  <div
                    key={expert.userId}
                    className="flex items-center space-x-3 p-2 bg-muted rounded-md"
                  >
                    <Avatar className="h-8 w-8">
                      <AvatarFallback>
                        {getInitials(expert.name)}
                      </AvatarFallback>
                    </Avatar>
                    <div className="flex-1">
                      <p className="text-sm font-medium">{expert.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {expert.email}
                      </p>
                    </div>
                    <Badge variant="outline" className="text-xs">
                      {expert.expertiseLevel}
                    </Badge>
                    {isEditing && (
                      <Button variant="ghost" size="sm">
                        <X className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                ))}
                {isEditing && (
                  <Button variant="outline" size="sm" className="w-full">
                    <Plus className="h-4 w-4 mr-2" />
                    Add Expert
                  </Button>
                )}
              </div>
            </div>

            {/* Last Updated Info */}
            {contextData?.lastReviewed && (
              <div className="pt-4 border-t">
                <p className="text-xs text-muted-foreground">
                  Last reviewed{" "}
                  {formatRelativeTime(contextData.lastReviewed, "unknown")}
                  {contextData?.context?.reviewedBy &&
                    ` by ${contextData.context.reviewedBy}`}
                </p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>
    </TooltipProvider>
  );
};

// Helper functions
function getPurposePlaceholder(entityType: string): string {
  switch (entityType) {
    case "TABLE":
      return "e.g., Stores customer order headers with shipping and billing info";
    case "COLUMN":
      return "e.g., Unique identifier for each order, used for lookups and joins";
    case "SP":
      return "e.g., Processes daily order batch and updates inventory";
    default:
      return "Describe the purpose...";
  }
}

/**
 * Safely compute up to two initials from a name string.
 * - Trims the input
 * - Splits on whitespace and filters empty segments
 * - Maps to the first character of each segment
 * - Takes the first two initials (or single if only one segment)
 * - Returns uppercased initials or '?' when no valid characters found
 */
function getInitials(name?: string): string {
  if (!name) return "?";
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const initials = parts.map((p) => p[0]).filter(Boolean);
  if (initials.length === 0) return "?";
  const result =
    initials.length === 1 ? initials[0] : `${initials[0]}${initials[1]}`;
  return result.toUpperCase();
}

// Removed local time formatter; using date-fns formatDistanceToNow
