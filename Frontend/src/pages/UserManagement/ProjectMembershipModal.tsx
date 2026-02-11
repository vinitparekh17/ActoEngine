import { useState, useEffect, useMemo } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "../../components/ui/dialog";
import { Switch } from "../../components/ui/switch";
import { Skeleton } from "../../components/ui/skeleton";
import { Input } from "../../components/ui/input";
import { ScrollArea } from "../../components/ui/scroll-area";
import { Search, Loader2, Database, Folder } from "lucide-react";
import { useApi, api } from "../../hooks/useApi";
import type { UserDto } from "../../types/user-management";
import type { Project } from "../../types/project";
import { toast } from "sonner";
import { cn } from "../../lib/utils";

interface ProjectMembershipModalProps {
  isOpen: boolean;
  user: UserDto | null;
  onClose: (open: boolean) => void;
}

export function ProjectMembershipModal({
  isOpen,
  user,
  onClose,
}: ProjectMembershipModalProps) {
  const [searchQuery, setSearchQuery] = useState("");
  const [pendingToggles, setPendingToggles] = useState<Set<number>>(new Set());
  // Track optimistic additions/removals separately from server state
  const [optimisticAdds, setOptimisticAdds] = useState<Set<number>>(new Set());
  const [optimisticRemoves, setOptimisticRemoves] = useState<Set<number>>(
    new Set(),
  );

  // Fetch all projects
  const { data: projects = [], isLoading: isLoadingProjects } = useApi<
    Project[]
  >("/projects", { enabled: isOpen, staleTime: 5 * 60 * 1000 });

  // Fetch user's current memberships
  const {
    data: memberProjectIds = [],
    isLoading: isLoadingMemberships,
    refetch: refetchMemberships,
  } = useApi<number[]>(user ? `/projects/user/${user.userId}` : "", {
    enabled: isOpen && !!user,
    staleTime: 0,
  });

  // Compute effective membership from server state + optimistic updates
  const effectiveMemberships = useMemo(() => {
    const set = new Set(memberProjectIds);
    optimisticAdds.forEach((id) => set.add(id));
    optimisticRemoves.forEach((id) => set.delete(id));
    return set;
  }, [memberProjectIds, optimisticAdds, optimisticRemoves]);

  // Reset state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setSearchQuery("");
      setPendingToggles(new Set());
      setOptimisticAdds(new Set());
      setOptimisticRemoves(new Set());
    }
  }, [isOpen]);

  const handleToggleProject = async (
    projectId: number,
    isCurrentlyMember: boolean,
  ) => {
    if (!user || pendingToggles.has(projectId)) return;

    // Mark as pending
    setPendingToggles((prev) => new Set(prev).add(projectId));

    // Optimistic update
    if (isCurrentlyMember) {
      setOptimisticRemoves((prev) => new Set(prev).add(projectId));
      setOptimisticAdds((prev) => {
        const next = new Set(prev);
        next.delete(projectId);
        return next;
      });
    } else {
      setOptimisticAdds((prev) => new Set(prev).add(projectId));
      setOptimisticRemoves((prev) => {
        const next = new Set(prev);
        next.delete(projectId);
        return next;
      });
    }

    try {
      if (isCurrentlyMember) {
        await api.delete(`/projects/${projectId}/members/${user.userId}`);
        toast.success("Removed from project");
      } else {
        await api.post(`/projects/${projectId}/members`, user.userId);
        toast.success("Added to project");
      }
      // Refresh to sync with server, clear optimistic state
      await refetchMemberships();
      setOptimisticAdds(new Set());
      setOptimisticRemoves(new Set());
    } catch (error: any) {
      // Revert optimistic update on failure
      if (isCurrentlyMember) {
        setOptimisticRemoves((prev) => {
          const next = new Set(prev);
          next.delete(projectId);
          return next;
        });
      } else {
        setOptimisticAdds((prev) => {
          const next = new Set(prev);
          next.delete(projectId);
          return next;
        });
      }
      toast.error(error?.message || "Operation failed");
    } finally {
      setPendingToggles((prev) => {
        const next = new Set(prev);
        next.delete(projectId);
        return next;
      });
    }
  };

  const filteredProjects = projects.filter((p) =>
    p.projectName.toLowerCase().includes(searchQuery.toLowerCase()),
  );

  // Guard: No user
  if (!user) {
    return null;
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader className="pb-4 border-b">
          <DialogTitle>Manage Project Access</DialogTitle>
          <DialogDescription>
            Assign {user.fullName || user.username} to projects. They will
            receive access immediately.
          </DialogDescription>
        </DialogHeader>

        <div className="py-4">
          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Filter projects by name..."
              className="pl-9 bg-muted/30"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
        </div>

        <div className="border rounded-lg bg-card overflow-hidden">
          <ScrollArea className="h-[400px]">
            {isLoadingProjects || isLoadingMemberships ? (
              <div className="divide-y">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div
                    key={i}
                    className="flex items-center justify-between p-4"
                  >
                    <div className="flex items-center gap-4 w-full mr-4">
                      <Skeleton className="h-10 w-10 rounded-md shrink-0" />
                      <div className="space-y-1.5 w-full">
                        <Skeleton className="h-4 w-1/3" />
                        <Skeleton className="h-3 w-1/2" />
                      </div>
                    </div>
                    <Skeleton className="h-5 w-9 rounded-full shrink-0" />
                  </div>
                ))}
              </div>
            ) : filteredProjects.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-16 text-center text-muted-foreground bg-muted/5 h-full">
                <Database className="h-12 w-12 opacity-10 mb-3" />
                <p className="font-medium">
                  {projects.length === 0
                    ? "No projects available"
                    : "No matching projects"}
                </p>
              </div>
            ) : (
              <div className="divide-y">
                {filteredProjects.map((project) => {
                  const isMember = effectiveMemberships.has(project.projectId);
                  const isPending = pendingToggles.has(project.projectId);

                  return (
                    <div
                      key={project.projectId}
                      className={cn(
                        "flex items-center justify-between p-4 transition-colors hover:bg-muted/40",
                        isMember && "bg-muted/10",
                        isPending && "opacity-60",
                      )}
                    >
                      <div className="flex items-center gap-4 min-w-0 pr-4">
                        <div className="h-10 w-10 rounded-md bg-primary/10 flex items-center justify-center shrink-0 text-primary">
                          <Folder className="h-5 w-5" />
                        </div>

                        <div className="flex flex-col min-w-0 gap-0.5">
                          <span className="font-semibold text-sm truncate">
                            {project.projectName}
                          </span>
                          <span className="text-xs text-muted-foreground truncate">
                            {project.description || "No description"}
                          </span>
                        </div>
                      </div>

                      <div className="flex-shrink-0 pl-2">
                        {isPending ? (
                          <div className="h-5 w-9 flex items-center justify-center">
                            <Loader2 className="h-4 w-4 animate-spin text-primary" />
                          </div>
                        ) : (
                          <Switch
                            checked={isMember}
                            onCheckedChange={() =>
                              handleToggleProject(project.projectId, isMember)
                            }
                          />
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </ScrollArea>
        </div>
      </DialogContent>
    </Dialog>
  );
}
