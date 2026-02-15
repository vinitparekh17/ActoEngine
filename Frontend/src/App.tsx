import { useEffect } from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { Toaster } from "sonner";
import SPGeneratorPage from "@/pages/SpBuilder";
import AppLayout from "@/components/layout/Layout";
import { QueryProvider } from "@/providers/QueryProvider";
import { useAuth, useAuthStore } from "@/hooks/useAuth";
import { useCsrfInit } from "@/hooks/useCsrf";
import { ApiErrorBoundary } from "@/components/errors/ApiErrorBoundary";
import { PermissionRoute } from "@/components/routing/PermissionRoute";
import FormBuilderPage from "@/pages/FormBuilder";
import LoginPage from "@/pages/Login";
import ClientManagementPage from "@/pages/ClientManagement";
import ProjectDashboard from "@/pages/ProjectDashboard";
import ProjectSetup from "@/pages/ProjectSetup";
import ProjectHub from "@/pages/ProjectHub";
import ProjectSettings from "@/pages/ProjectSetting";
import { ContextDashboard } from "@/pages/ContextDashboard";
import EntityExplorer from "@/pages/EntityExplorer";
import ImpactAnalysisPage from "@/pages/ImpactAnalysis";
import ERDiagramPage from "@/pages/ERDiagram";
import UserManagementPage from "@/pages/UserManagement";
import RoleManagementPage from "@/pages/RoleManagement";
import EntityDetailPage from "@/pages/EntityDetailPage";
import { AccessDenied } from "./components/feedback/AccessDenied";
import { initializeApiClient } from "./lib/api";
import NotFoundPage from "./pages/NotFoundPage";

// ============================================
// Protected Route Wrapper
// ============================================
function ProtectedRoute({ children }: Readonly<{ children: React.ReactNode }>) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    );
  }

  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
}

function AppRoutes() {
  useEffect(() => {
    initializeApiClient(() => {
      useAuthStore.getState().clearAuth();
    });
  }, []);

  useCsrfInit();

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <ApiErrorBoundary>
              <AppLayout />
            </ApiErrorBoundary>
          </ProtectedRoute>
        }
      >
        {/* Default landing page */}
        <Route
          index
          element={
            <PermissionRoute permission="Contexts:Read">
              <ContextDashboard />
            </PermissionRoute>
          }
        />
        {/* Project routes */}
        <Route
          path="projects"
          element={
            <PermissionRoute permission="Projects:Read">
              <ProjectDashboard />
            </PermissionRoute>
          }
        />
        <Route
          path="project/:projectId"
          element={
            <PermissionRoute permission="Projects:Read">
              <ProjectHub />
            </PermissionRoute>
          }
        />
        <Route
          path="project/:projectId/settings"
          element={
            <PermissionRoute permission="Projects:Read">
              <ProjectSettings />
            </PermissionRoute>
          }
        />
        <Route
          path="project/new"
          element={
            <PermissionRoute permission="Projects:Create">
              <ProjectSetup />
            </PermissionRoute>
          }
        />
        {/* General routes */}
        {/* Legacy route - redirects to / */}
        <Route
          path="clients"
          element={
            <PermissionRoute permission="Clients:Read">
              <ClientManagementPage />
            </PermissionRoute>
          }
        />
        {/* Admin routes */}
        <Route
          path="admin/users"
          element={
            <PermissionRoute permission="Users:Read">
              <UserManagementPage />
            </PermissionRoute>
          }
        />
        <Route
          path="admin/roles"
          element={
            <PermissionRoute permission="Roles:Read">
              <RoleManagementPage />
            </PermissionRoute>
          }
        />
        {/* Builder routes */}
        <Route
          path="form-builder"
          element={
            <PermissionRoute permission="Forms:Read">
              <FormBuilderPage />
            </PermissionRoute>
          }
        />
        <Route
          path="sp-builder"
          element={
            <PermissionRoute permission="StoredProcedures:Read">
              <SPGeneratorPage />
            </PermissionRoute>
          }
        />

        {/* New Entity Explorer routes */}
        <Route
          path="project/:projectId/entities"
          element={
            <PermissionRoute permission="Contexts:Read">
              <EntityExplorer />
            </PermissionRoute>
          }
        />
        <Route
          path="project/:projectId/entities/:entityType/:entityId"
          element={
            <PermissionRoute permission="Contexts:Read">
              <EntityExplorer />
            </PermissionRoute>
          }
        />
        <Route
          path="project/:projectId/entities/:entityType/:entityId/:tab"
          element={
            <PermissionRoute permission="Contexts:Read">
              <EntityExplorer />
            </PermissionRoute>
          }
        />

        {/* Entity detail routes - unified with entityType param */}
        <Route
          path="project/:projectId/:entityType/:entityId/detail"
          element={
            <PermissionRoute permission="Contexts:Read">
              <EntityDetailPage />
            </PermissionRoute>
          }
        />

        {/* Impact Analysis route */}
        <Route
          path="project/:projectId/impact/:entityType/:entityId"
          element={
            <PermissionRoute permission="Contexts:Read">
              <ImpactAnalysisPage />
            </PermissionRoute>
          }
        />

        {/* ER Diagram route */}
        <Route
          path="project/:projectId/er-diagram"
          element={
            <PermissionRoute permission="Schema:Read">
              <ERDiagramPage />
            </PermissionRoute>
          }
        />
        <Route path="access-denied" element={<AccessDenied />} />
        {/* Standalone column route for direct navigation from context dialogs */}

        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}

function App() {
  return (
    <QueryProvider>
      <BrowserRouter>
        <AppRoutes />
        <Toaster position="top-right" richColors />
      </BrowserRouter>
    </QueryProvider>
  );
}

export default App;
