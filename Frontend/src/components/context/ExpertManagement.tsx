// components/context/ExpertManagement.tsx
import React, { useState } from "react";
import { formatRelativeTime } from "@/lib/utils";
import { useProject } from "@/hooks/useProject";
import { useApi, useApiPost, useApiDelete } from "@/hooks/useApi";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { CardSkeleton } from "@/components/ui/skeletons";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  UserPlus,
  X,
  Lightbulb,
  Crown,
  Star,
  User,
  GitCommit,
  AlertCircle,
  Loader2,
} from "lucide-react";
import { toast } from "sonner";
import type { ProjectUser } from "../../types/project";

// Types
interface Expert {
  userId: number;
  expertiseLevel: "OWNER" | "EXPERT" | "FAMILIAR" | "CONTRIBUTOR";
  notes?: string;
  addedAt: string;
  user: {
    userId: number;
    fullName?: string;
    username: string;
    email: string;
  };
}

interface ContextResponse {
  context: any;
  experts: Expert[];
  completenessScore: number;
  isStale: boolean;
}

interface UserManagementResponse {
  users: ProjectUser[];
  totalCount: number;
}

interface ExpertManagementProps {
  entityType: "TABLE" | "COLUMN" | "SP";
  entityId: number;
  entityName: string;
}

// helper (put near your component)
function getInitials(name?: string): string {
  const normalized = (name ?? "").normalize("NFC").trim();

  if (normalized === "") {
    return "?";
  }

  const chars = Array.from(normalized);

  if (chars.length === 1) {
    return chars[0].toUpperCase();
  }

  return (chars[0] + chars[1]).toUpperCase();
}

/**
 * Widget for managing entity experts
 * Endpoints:
 * - GET /projects/{projectId}/context/{type}/{id} - Get current experts
 * - POST /projects/{projectId}/context/{type}/{id}/experts - Add expert
 * - DELETE /projects/{projectId}/context/{type}/{id}/experts/{userId} - Remove expert
 * - GET /projects/{projectId}/users - Get all project users
 */
export const ExpertManagement: React.FC<ExpertManagementProps> = ({
  entityType,
  entityId,
  entityName,
}) => {
  const { selectedProjectId, hasProject } = useProject();

  const [isAddDialogOpen, setIsAddDialogOpen] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedLevel, setSelectedLevel] = useState<string>("EXPERT");
  const [notes, setNotes] = useState("");
  // Track which expert is currently being removed to avoid disabling all buttons
  const [removedExpertId, setRemovedExpertId] = useState<number | null>(null);

  // Fetch context with experts
  const {
    data: contextResponse,
    isLoading: isLoadingContext,
    error: contextError,
    refetch: refetchContext,
  } = useApi<ContextResponse>(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}`,
    {
      enabled: hasProject && !!selectedProjectId && !!entityId,
      staleTime: 30 * 1000, // 30 seconds
    },
  );

  // Fetch project members (for dropdown) - only when dialog is open
  // Uses the /projects/{projectId}/users endpoint that returns only project members
  const { data: projectMembers, isLoading: isLoadingUsers } = useApi<
    ProjectUser[]
  >(`/projects/${selectedProjectId}/users`, {
    enabled: hasProject && !!selectedProjectId && isAddDialogOpen,
    staleTime: 10 * 60 * 1000, // 10 minutes
  });

  // Use project members directly (no filtering needed)
  const allUsers = projectMembers || [];

  // Add expert mutation
  const { mutate: addExpert, isPending: isAddingExpert } = useApiPost<
    any,
    {
      userId: number;
      expertiseLevel: string;
      notes?: string;
    }
  >(
    `/projects/${selectedProjectId}/context/${entityType}/${entityId}/experts`,
    {
      onSuccess: () => {
        toast.success("Expert added successfully");
        setIsAddDialogOpen(false);
        resetForm();
      },
      onError: (error) => {
        toast.error(`Failed to add expert: ${error.message}`);
      },
      invalidateKeys: [
        [
          "projects",
          String(selectedProjectId),
          "context",
          entityType,
          String(entityId),
        ],
      ],
    },
  );

  // Delete expert mutation
  const { mutateAsync: deleteExpert, isPending: isDeletingExpert } =
    useApiDelete<any, { userId: number }>(
      `/projects/${selectedProjectId}/context/${entityType}/${entityId}/experts/:userId`,
      {
        onSuccess: () => {
          setRemovedExpertId(null);
          refetchContext();
        },
        onError: () => {
          setRemovedExpertId(null);
        },
        invalidateKeys: [
          [
            "projects",
            String(selectedProjectId),
            "context",
            entityType,
            String(entityId),
          ],
        ],
      },
    );

  const removeExpert = async (userId: number) => {
    try {
      await deleteExpert({ userId });
    } catch {
      // Error handling is managed by the hook callbacks
    }
  };

  const experts = contextResponse?.experts || [];

  const handleAddExpert = () => {
    if (selectedUserId == null) {
      toast.error("Please select a user");
      return;
    }

    // Check if user is already an expert
    const existingExpert = experts.find(
      (expert) => expert.userId === selectedUserId,
    );
    if (existingExpert) {
      toast.error("This user is already assigned as an expert");
      return;
    }

    addExpert({
      userId: selectedUserId,
      expertiseLevel: selectedLevel,
      notes: notes || undefined,
    });
  };

  const handleRemoveExpert = async (userId: number) => {
    const expert = experts.find((e) => e.userId === userId);
    const userName =
      expert?.user?.fullName || expert?.user?.username || "this expert";

    if (confirm(`Are you sure you want to remove ${userName}?`)) {
      // mark which expert is being removed so only that button shows loading
      setRemovedExpertId(userId);
      await removeExpert(userId);
    }
  };

  const resetForm = () => {
    setSelectedUserId(null);
    setSelectedLevel("EXPERT");
    setNotes("");
  };

  const getExpertIcon = (level: string) => {
    switch (level) {
      case "OWNER":
        return <Crown className="w-3 h-3 text-yellow-500" />;
      case "EXPERT":
        return <Star className="w-3 h-3 text-blue-500" />;
      case "FAMILIAR":
        return <User className="w-3 h-3 text-gray-500" />;
      case "CONTRIBUTOR":
        return <GitCommit className="w-3 h-3 text-green-500" />;
      default:
        return null;
    }
  };

  const getExpertBadgeVariant = (
    level: string,
  ): "default" | "secondary" | "outline" | "destructive" => {
    switch (level) {
      case "OWNER":
        return "default";
      case "EXPERT":
        return "secondary";
      case "FAMILIAR":
      case "CONTRIBUTOR":
        return "outline";
      default:
        return "outline";
    }
  };

  const getExpertLevelLabel = (level: string): string => {
    switch (level) {
      case "OWNER":
        return "Owner";
      case "EXPERT":
        return "Expert";
      case "FAMILIAR":
        return "Familiar";
      case "CONTRIBUTOR":
        return "Contributor";
      default:
        return level;
    }
  };

  // Loading state
  if (isLoadingContext) {
    return <CardSkeleton lines={5} showAvatar />;
  }

  // Error state
  if (contextError) {
    return (
      <Card>
        <CardContent className="py-6">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Failed to load experts: {contextError.message}
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  // No project selected
  if (!hasProject) {
    return (
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="text-base">
                Subject Matter Experts
              </CardTitle>
              <CardDescription className="text-sm">
                People who can help with "{entityName}"
              </CardDescription>
            </div>

            <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
              <DialogTrigger asChild>
                <Button size="sm" variant="outline">
                  <UserPlus className="w-4 h-4 mr-2" />
                  Add
                </Button>
              </DialogTrigger>
              <DialogContent className="max-w-lg">
                <DialogHeader>
                  <DialogTitle>Add Expert</DialogTitle>
                  <DialogDescription>
                    Assign someone who has knowledge about this{" "}
                    {entityType.toLowerCase()}
                  </DialogDescription>
                </DialogHeader>

                <div className="space-y-4 py-4">
                  {/* User Selection */}
                  <div className="space-y-2">
                    <Label>User</Label>
                    <Select
                      value={selectedUserId?.toString() || ""}
                      onValueChange={(value) =>
                        setSelectedUserId(parseInt(value))
                      }
                      disabled={isLoadingUsers}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            isLoadingUsers
                              ? "Loading users..."
                              : "Select a user"
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {allUsers?.map((user) => (
                          <SelectItem
                            key={user.userId}
                            value={user.userId.toString()}
                            disabled={experts.some(
                              (e) => e.userId === user.userId,
                            )}
                          >
                            <div className="flex items-center gap-2">
                              <Avatar className="w-5 h-5">
                                <AvatarFallback className="text-xs">
                                  {user.fullName
                                    ?.substring(0, 2)
                                    .toUpperCase() ||
                                    user.username
                                      ?.substring(0, 2)
                                      .toUpperCase() ||
                                    "U"}
                                </AvatarFallback>
                              </Avatar>
                              <span>{user.fullName || user.username}</span>
                              <span className="text-muted-foreground text-xs">
                                ({user.email})
                              </span>
                              {experts.some(
                                (e) => e.userId === user.userId,
                              ) && (
                                <Badge variant="outline" className="text-xs">
                                  Already assigned
                                </Badge>
                              )}
                            </div>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Expertise Level */}
                  <div className="space-y-2">
                    <Label>Expertise Level</Label>
                    <Select
                      value={selectedLevel}
                      onValueChange={setSelectedLevel}
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="OWNER">
                          <div className="flex items-center gap-2">
                            <Crown className="w-4 h-4 text-yellow-500" />
                            <div>
                              <div className="font-medium">Owner</div>
                              <div className="text-xs text-muted-foreground">
                                Built it, maintains it
                              </div>
                            </div>
                          </div>
                        </SelectItem>
                        <SelectItem value="EXPERT">
                          <div className="flex items-center gap-2">
                            <Star className="w-4 h-4 text-blue-500" />
                            <div>
                              <div className="font-medium">Expert</div>
                              <div className="text-xs text-muted-foreground">
                                Deep knowledge
                              </div>
                            </div>
                          </div>
                        </SelectItem>
                        <SelectItem value="FAMILIAR">
                          <div className="flex items-center gap-2">
                            <User className="w-4 h-4 text-gray-500" />
                            <div>
                              <div className="font-medium">Familiar</div>
                              <div className="text-xs text-muted-foreground">
                                Can answer questions
                              </div>
                            </div>
                          </div>
                        </SelectItem>
                        <SelectItem value="CONTRIBUTOR">
                          <div className="flex items-center gap-2">
                            <GitCommit className="w-4 h-4 text-green-500" />
                            <div>
                              <div className="font-medium">Contributor</div>
                              <div className="text-xs text-muted-foreground">
                                Has made changes
                              </div>
                            </div>
                          </div>
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Notes */}
                  <div className="space-y-2">
                    <Label>Notes (Optional)</Label>
                    <Input
                      placeholder="e.g., Maintains the payment integration"
                      value={notes}
                      onChange={(e) => setNotes(e.target.value)}
                    />
                  </div>
                </div>

                <div className="flex justify-end gap-2">
                  <Button
                    variant="outline"
                    onClick={() => {
                      setIsAddDialogOpen(false);
                      resetForm();
                    }}
                  >
                    Cancel
                  </Button>
                  <Button
                    onClick={handleAddExpert}
                    disabled={selectedUserId == null || isAddingExpert}
                  >
                    {isAddingExpert ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Adding...
                      </>
                    ) : (
                      "Add Expert"
                    )}
                  </Button>
                </div>
              </DialogContent>
            </Dialog>
          </div>
        </CardHeader>

        <CardContent>
          {experts.length === 0 ? (
            <Alert>
              <AlertDescription className="text-sm">
                No experts assigned yet. Add someone who knows about this{" "}
                {entityType.toLowerCase()}.
              </AlertDescription>
            </Alert>
          ) : (
            <div className="space-y-3">
              {experts.map((expert) => (
                <div
                  key={expert.userId}
                  className="flex items-center justify-between p-3 rounded-lg border bg-card hover:bg-accent/50 transition-colors"
                >
                  <div className="flex items-center gap-3 flex-1 min-w-0">
                    <Avatar>
                      <AvatarFallback>
                        {expert.user?.fullName?.substring(0, 2).toUpperCase() ||
                          expert.user?.username
                            ?.substring(0, 2)
                            .toUpperCase() ||
                          "U"}
                      </AvatarFallback>
                    </Avatar>

                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <p className="text-sm font-medium truncate">
                          {expert.user?.fullName || expert.user?.username}
                        </p>
                        <Badge
                          variant={getExpertBadgeVariant(expert.expertiseLevel)}
                          className="gap-1 shrink-0"
                        >
                          {getExpertIcon(expert.expertiseLevel)}
                          {getExpertLevelLabel(expert.expertiseLevel)}
                        </Badge>
                      </div>
                      <p className="text-xs text-muted-foreground truncate">
                        {expert.user?.email}
                      </p>
                      {expert.notes && (
                        <p className="text-xs text-muted-foreground mt-1 italic truncate">
                          {expert.notes}
                        </p>
                      )}
                      <p className="text-xs text-muted-foreground mt-1">
                        Added {formatRelativeTime(expert.addedAt, "recently")}
                      </p>
                    </div>
                  </div>

                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8 text-muted-foreground hover:text-destructive shrink-0"
                    onClick={() => handleRemoveExpert(expert.userId)}
                    disabled={
                      isDeletingExpert && removedExpertId === expert.userId
                    }
                  >
                    {isDeletingExpert && removedExpertId === expert.userId ? (
                      <Loader2 className="w-4 h-4 animate-spin" />
                    ) : (
                      <X className="w-4 h-4" />
                    )}
                  </Button>
                </div>
              ))}
            </div>
          )}

          {/* Expert Count Summary */}
          {experts.length > 0 && (
            <div className="mt-4 pt-4 border-t text-xs text-muted-foreground">
              {experts.filter((e) => e.expertiseLevel === "OWNER").length}{" "}
              owner(s),{" "}
              {experts.filter((e) => e.expertiseLevel === "EXPERT").length}{" "}
              expert(s),{" "}
              {experts.filter((e) => e.expertiseLevel === "FAMILIAR").length}{" "}
              familiar,{" "}
              {experts.filter((e) => e.expertiseLevel === "CONTRIBUTOR").length}{" "}
              contributor(s)
            </div>
          )}
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-base">Subject Matter Experts</CardTitle>
            <CardDescription className="text-sm">
              People who can help with "{entityName}"
            </CardDescription>
          </div>

          <Dialog open={isAddDialogOpen} onOpenChange={setIsAddDialogOpen}>
            <DialogTrigger asChild>
              <Button size="sm" variant="outline">
                <UserPlus className="w-4 h-4 mr-2" />
                Add
              </Button>
            </DialogTrigger>
            <DialogContent className="max-w-lg">
              <DialogHeader>
                <DialogTitle>Add Expert</DialogTitle>
                <DialogDescription>
                  Assign someone who has knowledge about this{" "}
                  {entityType.toLowerCase()}
                </DialogDescription>
              </DialogHeader>

              <div className="space-y-4 py-4">
                {/* User Selection */}
                <div className="space-y-2">
                  <Label>User</Label>
                  <Select
                    value={selectedUserId?.toString() || ""}
                    onValueChange={(value) =>
                      setSelectedUserId(parseInt(value))
                    }
                    disabled={isLoadingUsers}
                  >
                    <SelectTrigger>
                      <SelectValue
                        placeholder={
                          isLoadingUsers ? "Loading users..." : "Select a user"
                        }
                      />
                    </SelectTrigger>
                    <SelectContent>
                      {allUsers?.map((user) => (
                        <SelectItem
                          key={user.userId}
                          value={user.userId.toString()}
                          disabled={experts.some(
                            (e) => e.userId === user.userId,
                          )}
                        >
                          <div className="flex items-center gap-2">
                            <Avatar className="w-5 h-5">
                              <AvatarFallback className="text-xs">
                                {user.fullName?.substring(0, 2).toUpperCase() ||
                                  user.username
                                    ?.substring(0, 2)
                                    .toUpperCase() ||
                                  "U"}
                              </AvatarFallback>
                            </Avatar>
                            <span>{user.fullName || user.username}</span>
                            <span className="text-muted-foreground text-xs">
                              ({user.email})
                            </span>
                            {experts.some((e) => e.userId === user.userId) && (
                              <Badge variant="outline" className="text-xs">
                                Already assigned
                              </Badge>
                            )}
                          </div>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Expertise Level */}
                <div className="space-y-2">
                  <Label>Expertise Level</Label>
                  <Select
                    value={selectedLevel}
                    onValueChange={setSelectedLevel}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="OWNER">
                        <div className="flex items-center gap-2">
                          <Crown className="w-4 h-4 text-yellow-500" />
                          <div>
                            <div className="font-medium">Owner</div>
                            <div className="text-xs text-muted-foreground">
                              Built it, maintains it
                            </div>
                          </div>
                        </div>
                      </SelectItem>
                      <SelectItem value="EXPERT">
                        <div className="flex items-center gap-2">
                          <Star className="w-4 h-4 text-blue-500" />
                          <div>
                            <div className="font-medium">Expert</div>
                            <div className="text-xs text-muted-foreground">
                              Deep knowledge
                            </div>
                          </div>
                        </div>
                      </SelectItem>
                      <SelectItem value="FAMILIAR">
                        <div className="flex items-center gap-2">
                          <User className="w-4 h-4 text-gray-500" />
                          <div>
                            <div className="font-medium">Familiar</div>
                            <div className="text-xs text-muted-foreground">
                              Can answer questions
                            </div>
                          </div>
                        </div>
                      </SelectItem>
                      <SelectItem value="CONTRIBUTOR">
                        <div className="flex items-center gap-2">
                          <GitCommit className="w-4 h-4 text-green-500" />
                          <div>
                            <div className="font-medium">Contributor</div>
                            <div className="text-xs text-muted-foreground">
                              Has made changes
                            </div>
                          </div>
                        </div>
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                {/* Notes */}
                <div className="space-y-2">
                  <Label>Notes (Optional)</Label>
                  <Input
                    placeholder="e.g., Maintains the payment integration"
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2">
                <Button
                  variant="outline"
                  onClick={() => {
                    setIsAddDialogOpen(false);
                    resetForm();
                  }}
                >
                  Cancel
                </Button>
                <Button
                  onClick={handleAddExpert}
                  disabled={!selectedUserId || isAddingExpert}
                >
                  {isAddingExpert ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                      Adding...
                    </>
                  ) : (
                    "Add Expert"
                  )}
                </Button>
              </div>
            </DialogContent>
          </Dialog>
        </div>
      </CardHeader>

      <CardContent>
        {experts.length === 0 ? (
          <Alert>
            <AlertDescription className="text-sm">
              No experts assigned yet. Add someone who knows about this{" "}
              {entityType.toLowerCase()}.
            </AlertDescription>
          </Alert>
        ) : (
          <div className="space-y-3">
            {experts.map((expert) => (
              <div
                key={expert.userId}
                className="flex items-center justify-between p-3 rounded-lg border bg-card hover:bg-accent/50 transition-colors"
              >
                <div className="flex items-center gap-3 flex-1 min-w-0">
                  <Avatar>
                    <AvatarFallback>
                      {expert.user?.fullName?.substring(0, 2).toUpperCase() ||
                        expert.user?.username?.substring(0, 2).toUpperCase() ||
                        "U"}
                    </AvatarFallback>
                  </Avatar>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <p className="text-sm font-medium truncate">
                        {expert.user?.fullName || expert.user?.username}
                      </p>
                      <Badge
                        variant={getExpertBadgeVariant(expert.expertiseLevel)}
                        className="gap-1 shrink-0"
                      >
                        {getExpertIcon(expert.expertiseLevel)}
                        {getExpertLevelLabel(expert.expertiseLevel)}
                      </Badge>
                    </div>
                    <p className="text-xs text-muted-foreground truncate">
                      {expert.user?.email}
                    </p>
                    {expert.notes && (
                      <p className="text-xs text-muted-foreground mt-1 italic truncate">
                        {expert.notes}
                      </p>
                    )}
                    <p className="text-xs text-muted-foreground mt-1">
                      Added {formatRelativeTime(expert.addedAt, "recently")}
                    </p>
                  </div>
                </div>

                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-muted-foreground hover:text-destructive shrink-0"
                  onClick={() => handleRemoveExpert(expert.userId)}
                  disabled={
                    isDeletingExpert && removedExpertId === expert.userId
                  }
                >
                  {isDeletingExpert && removedExpertId === expert.userId ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <X className="w-4 h-4" />
                  )}
                </Button>
              </div>
            ))}
          </div>
        )}

        {/* Expert Count Summary */}
        {experts.length > 0 && (
          <div className="mt-4 pt-4 border-t text-xs text-muted-foreground">
            {experts.filter((e) => e.expertiseLevel === "OWNER").length}{" "}
            owner(s),{" "}
            {experts.filter((e) => e.expertiseLevel === "EXPERT").length}{" "}
            expert(s),{" "}
            {experts.filter((e) => e.expertiseLevel === "FAMILIAR").length}{" "}
            familiar,{" "}
            {experts.filter((e) => e.expertiseLevel === "CONTRIBUTOR").length}{" "}
            contributor(s)
          </div>
        )}
      </CardContent>
    </Card>
  );
};

// Removed local time formatter; using date-fns formatDistanceToNow
