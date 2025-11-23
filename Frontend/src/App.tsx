import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { Toaster } from "sonner";
import SPGeneratorPage from "@/pages/SpBuilder";
import AppLayout from "@/components/layout/Layout";
import { QueryProvider } from "@/providers/QueryProvider";
import { useAuth, useAuthStore } from "@/hooks/useAuth";
// import { useCsrfInit } from "@/hooks/useCsrf";
import { ApiErrorBoundary } from "@/components/errors/ApiErrorBoundary";
import FormBuilderPage from "@/pages/FormBuilder";
import LoginPage from "@/pages/Login";
import DashboardPage from "@/pages/Dashboard";
import ClientManagementPage from "@/pages/ClientManagement";
import ProjectDashboard from "@/pages/ProjectDashboard";
import ProjectSetup from "@/pages/ProjectSetup";
import ProjectHub from "@/pages/ProjectHub";
import ProjectSettings from "@/pages/ProjectSetting";
import { ContextDashboard } from "@/pages/ContextDashboard";
import ContextExperts from "./pages/ContextExpert";
import ContextBrowse from "./pages/ContextBrowser";
import TableDetail from "./pages/TableDetail";
import StoredProcedureDetail from "./pages/StoredProcedureDetail";
import ColumnDetail from "./pages/ColumnDetail";
import { initializeApiClient } from "./lib/api";

// ============================================
// Protected Route Wrapper
// ============================================
function ProtectedRoute({ children }: { children: React.ReactNode }) {
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
  initializeApiClient(() => {
    useAuthStore.getState().clearAuth();
  });
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
        <Route index element={<Navigate to="/dashboard" replace />} />
        {/* Project routes */}
        <Route path="projects" element={<ProjectDashboard />} />
        <Route path="project/:projectId" element={<ProjectHub />} />
        <Route
          path="project/:projectId/settings"
          element={<ProjectSettings />}
        />
        <Route path="project/new" element={<ProjectSetup />} />
        {/* General routes */}
        <Route path="dashboard" element={<ContextDashboard />} />
        <Route path="clients" element={<ClientManagementPage />} />
        {/* Builder routes */}
        <Route path="form-builder" element={<FormBuilderPage />} />
        <Route path="sp-builder" element={<SPGeneratorPage />} />
        {/* Context routes */}
        <Route path="context" element={<ContextDashboard />} />
        <Route
          path="project/:projectId/context/browse"
          element={<ContextBrowse />}
        />
        <Route
          path="project/:projectId/context/experts"
          element={<ContextExperts />}
        />
        {/* Entity detail routes */}
        <Route
          path="project/:projectId/tables/:tableId"
          element={<TableDetail />}
        />
        <Route
          path="project/:projectId/stored-procedures/:procedureId"
          element={<StoredProcedureDetail />}
        />
        {/* Standalone column route for direct navigation from context dialogs */}
        <Route
          path="project/:projectId/columns/:columnId"
          element={
            <ApiErrorBoundary>
              <ColumnDetail />
            </ApiErrorBoundary>
          }
        />
        {/* Nested column route for navigation from table detail pages */}
        <Route
          path="project/:projectId/tables/:tableId/columns/:columnId"
          element={<ColumnDetail />}
        />
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
