import { create } from "zustand";
import { persist } from "zustand/middleware";
import { useCallback, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useApi } from "./useApi";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Project } from "../types/project";

// ============================================
// Types
// ============================================
export interface TableSchemaResponse {
  tableName: string;
  schemaName: string;
  description?: string;
  schema: {
    columns: ColumnSchema[];
  };
  columns: ColumnSchema[];
  primaryKeys: string[];
}

export interface ColumnSchema {
  schemaName: string;
  columnName: string;
  dataType: string;
  maxLength?: number;
  precision?: number;
  scale?: number;
  isNullable: boolean;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  isForeignKey: boolean;
  defaultValue?: string;
  description?: string;
  columnOrder?: number;
}

// Sanitized project for display (no sensitive data)
export type SafeProject = Project;

interface ProjectStore {
  selectedProjectId: number | null;
  setSelectedProjectId: (projectId: number) => void;
  clearSelectedProject: () => void;
}

// ============================================
// Zustand Store (Minimal Persistence)
// ============================================
const useProjectStore = create<ProjectStore>()(
  persist(
    (set) => ({
      selectedProjectId: null,

      setSelectedProjectId: (projectId: number) =>
        set({ selectedProjectId: projectId }),

      clearSelectedProject: () => set({ selectedProjectId: null }),
    }),
    {
      name: "actox-project",
      version: 1,
      partialize: (state) => ({
        selectedProjectId: state.selectedProjectId,
      }),
      migrate: (persistedState: any, version: number) => {
        if (version === 0) {
          return {
            selectedProjectId: persistedState.selectedProjectId || null,
          };
        }
        return persistedState as ProjectStore;
      },
    },
  ),
);

// ============================================
// Query Keys (Structured)
// ============================================
export const projectQueryKeys = {
  all: () => ["projects"] as const,
  detail: (id: number) => ["projects", id] as const,
  tables: (id: number) => ["projects", id, "tables"] as const,
};

// ============================================
// Main Hook - useProject
// ============================================
export function useProject() {
  const store = useProjectStore();
  const queryClient = useQueryClient();

  // Fetch all projects
  const {
    data: projects,
    isLoading: isLoadingProjects,
    error: projectsError,
    refetch: refetchProjects,
  } = useApi<Project[]>("/projects", {
    queryKey: [...projectQueryKeys.all()],
    staleTime: 5 * 60 * 1000,
    retry: 2,
    refetchOnMount: "always",
  });

  // const mockProjects: Project[] = [
  //   {
  //     id: 1,
  //     name: 'Sample Project',
  //     connectionString: 'Server=localhost;Database=SampleDB;User Id=sa;Password=your_password;',
  //     databaseName: 'SampleDB',
  //     serverName: 'localhost',
  //     createdAt: new Date(),
  //     updatedAt: new Date(),
  //   },
  // ];

  // Fetch selected project details
  const {
    data: projectDetails,
    isLoading: isLoadingDetails,
    error: projectDetailsError,
  } = useApi<Project>(`/projects/${store.selectedProjectId}`, {
    queryKey: [...projectQueryKeys.detail(store.selectedProjectId!)],
    enabled: !!store.selectedProjectId,
    staleTime: 10 * 60 * 1000,
    retry: 2,
  });

  // Get current selected project from cache
  const selectedProject =
    projectDetails ||
    queryClient.getQueryData<Project>(
      projectQueryKeys.detail(store.selectedProjectId!),
    );

  const safeProject: SafeProject | null = useMemo(() => {
    if (!selectedProject) return null;
    return {
      projectId: selectedProject.projectId,
      projectName: selectedProject.projectName,
      description: selectedProject.description,
      databaseType: selectedProject.databaseType,
      databaseName: selectedProject.databaseName,
      serverName: selectedProject.serverName,
      syncStatus: selectedProject.syncStatus,
      syncProgress: selectedProject.syncProgress,
      lastSyncAttempt: selectedProject.lastSyncAttempt,
      createdAt: selectedProject.createdAt,
      updatedAt: selectedProject.updatedAt,
    };
  }, [
    selectedProject?.projectId,
    selectedProject?.syncStatus,
    selectedProject?.syncProgress,
  ]);

  // Select project and invalidate dependent queries
  const selectProject = useCallback(
    (project: Project | number) => {
      const projectId =
        typeof project === "number" ? project : project.projectId;

      store.setSelectedProjectId(projectId);

      queryClient.invalidateQueries({
        queryKey: projectQueryKeys.tables(projectId),
      });
      queryClient.invalidateQueries({
        queryKey: ["DatabaseBrowser", "projects", projectId],
      });
      queryClient.invalidateQueries({
        queryKey: ["SpBuilder", "history"],
      });
    },
    [store, queryClient],
  );

  // Clear project and invalidate queries
  const clearProject = useCallback(() => {
    store.clearSelectedProject();

    // Invalidate all DatabaseBrowser and SpBuilder queries
    queryClient.invalidateQueries({
      queryKey: ["DatabaseBrowser"],
    });
    queryClient.invalidateQueries({
      queryKey: ["SpBuilder"],
    });
  }, [store, queryClient]);

  return {
    // State (sanitized - no connectionString)
    projects: projects?.map(
      (p) =>
        ({
          projectId: p.projectId,
          projectName: p.projectName,
          description: p.description,
          databaseType: p.databaseType,
          databaseName: p.databaseName,
          serverName: p.serverName,
          isActive: p.isActive,
          lastSyncAttempt: p.lastSyncAttempt,
          syncStatus: p.syncStatus,
          syncProgress: p.syncProgress,
          createdAt: p.createdAt,
          updatedAt: p.updatedAt,
        }) as SafeProject,
    ),
    selectedProject: safeProject,
    selectedProjectId: store.selectedProjectId,

    // Loading states
    isLoadingProjects,
    isLoadingDetails,
    isLoading: isLoadingProjects || isLoadingDetails,

    // Errors
    error: projectsError || projectDetailsError,

    // Actions
    selectProject,
    clearProject,
    refetchProjects,

    // Computed
    hasProject: !!store.selectedProjectId && !!selectedProject,
    databaseName: selectedProject?.databaseName,
    databaseType: selectedProject?.databaseType,
  } as const;
}

// ============================================
// Hook - useRequireProject (with redirect)
// ============================================

export function useRequireProject(redirectTo = "/projects") {
  const { selectedProject, isLoading, hasProject } = useProject();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isLoading && !hasProject) {
      toast.warning("Please select a project first");
      navigate(redirectTo, { replace: true });
    }
  }, [hasProject, isLoading, navigate, redirectTo]);

  return {
    selectedProject,
    isLoading,
    hasProject,
  } as const;
}

// ============================================
// Hook - useProjectTables (with error handling)
// ============================================
export function useProjectTables() {
  const { selectedProjectId, hasProject } = useProject();

  const {
    data: tables,
    isLoading,
    error,
  } = useApi<string[]>(`/SpBuilder/schema/${selectedProjectId}`, {
    queryKey: [...projectQueryKeys.tables(selectedProjectId!)],
    enabled: hasProject && !!selectedProjectId,
    staleTime: 2 * 60 * 1000,
    retry: 2,
    placeholderData: [],
  });

  return {
    tables: tables || [],
    isLoading,
    error,
    hasTables: (tables?.length || 0) > 0,
  } as const;
}

// ============================================
// Hook - useTableSchema (with connection string)
// ============================================
export function useTableSchema(
  tableName: string | undefined,
  schemaName?: string,
) {
  const { selectedProjectId, hasProject } = useProject();
  const effectiveSchemaName = schemaName || "dbo";
  const schemaUrl =
    tableName && selectedProjectId
      ? `/DatabaseBrowser/projects/${selectedProjectId}/tables/${tableName}/schema?schemaName=${encodeURIComponent(effectiveSchemaName)}`
      : "";
  const {
    data: schema,
    isLoading,
    error,
  } = useApi<TableSchemaResponse>(schemaUrl, {
    queryKey: [
      "projects",
      selectedProjectId,
      "tables",
      tableName,
      "schema",
      effectiveSchemaName,
    ],
    enabled: hasProject && !!tableName && !!selectedProjectId,
    staleTime: 5 * 60 * 1000,
    retry: 2,
  });

  // Normalize the response so that callers never have to worry about
  // `columns` being undefined. Some backend endpoints historically returned
  // `null` when hitting errors, which would break consumers with
  // "cannot read properties of undefined" bugs. This defensive transform
  // mirrors the guard logic in SpBuilder.
  const normalizedSchema = schema
    ? { ...schema, columns: schema.columns || [] }
    : undefined;

  return {
    schema: normalizedSchema,
    isLoading,
    error,
    hasSchema: !!normalizedSchema,
  } as const;
}

// ============================================
// Helper: Parse dates from API
// ============================================
export function parseProjectDates(project: any): Project {
  return {
    ...project,
    createdAt: project.createdAt ? new Date(project.createdAt) : undefined,
    updatedAt: project.updatedAt ? new Date(project.updatedAt) : undefined,
  };
}
