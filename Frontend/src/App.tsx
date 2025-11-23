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
import { ReLoginModalProvider, useReLoginModal } from "@/hooks/useReLoginModal";
import { useEffect } from "react";

// ============================================
// Protected Route Wrapper
/**
 * Renders the given children for authenticated users and redirects unauthenticated users to the login page.
 *
 * While the authentication state is being determined, renders a full-screen centered loading spinner.
 *
 * @returns The `children` React nodes when the user is authenticated; otherwise a redirect to `/login`.
 */
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

/**
 * Defines the application's route tree and initializes the API client with a handler that triggers the re-login modal.
 *
 * The component mounts the client-side routes (including protected and error-boundary-wrapped routes) and calls
 * initializeApiClient with a callback that displays the re-login modal when authentication must be refreshed.
 *
 * @returns The React Router route elements for the application.
 */
function AppRoutes() {
  const { showReLoginModal } = useReLoginModal();

  // Initialize API client with re-login modal handler
  useEffect(() => {
    initializeApiClient(() => {
      showReLoginModal();
    });
  }, [showReLoginModal]); // Added dependency

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
        {/* Project routes - Critical routes with individual error boundaries */}
        <Route
          path="projects"
          element={
            <ApiErrorBoundary>
              <ProjectDashboard />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/:projectId"
          element={
            <ApiErrorBoundary>
              <ProjectHub />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/:projectId/settings"
          element={
            <ApiErrorBoundary>
              <ProjectSettings />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/new"
          element={
            <ApiErrorBoundary>
              <ProjectSetup />
            </ApiErrorBoundary>
          }
        />
        {/* General routes */}
        <Route
          path="dashboard"
          element={
            <ApiErrorBoundary>
              <ContextDashboard />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="clients"
          element={
            <ApiErrorBoundary>
              <ClientManagementPage />
            </ApiErrorBoundary>
          }
        />
        {/* Builder routes - Critical complex pages */}
        <Route
          path="form-builder"
          element={
            <ApiErrorBoundary>
              <FormBuilderPage />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="sp-builder"
          element={
            <ApiErrorBoundary>
              <SPGeneratorPage />
            </ApiErrorBoundary>
          }
        />
        {/* Context routes */}
        <Route
          path="context"
          element={
            <ApiErrorBoundary>
              <ContextDashboard />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/:projectId/context/browse"
          element={
            <ApiErrorBoundary>
              <ContextBrowse />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/:projectId/context/experts"
          element={
            <ApiErrorBoundary>
              <ContextExperts />
            </ApiErrorBoundary>
          }
        />
        {/* Entity detail routes */}
        <Route
          path="project/:projectId/tables/:tableId"
          element={
            <ApiErrorBoundary>
              <TableDetail />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/:projectId/stored-procedures/:procedureId"
          element={
            <ApiErrorBoundary>
              <StoredProcedureDetail />
            </ApiErrorBoundary>
          }
        />
        <Route
          path="project/:projectId/tables/:tableId/columns/:columnId"
          element={
            <ApiErrorBoundary>
              <ColumnDetail />
            </ApiErrorBoundary>
          }
        />
      </Route>
    </Routes>
  );
}

/**
 * Root application component that composes global providers, routing, and UI chrome.
 *
 * Wraps the application with QueryProvider (data-fetching context), BrowserRouter (routing), and
 * ReLoginModalProvider (re-authentication modal), then mounts AppRoutes and a top-right Toaster.
 *
 * @returns The root React element for the application.
 */
function App() {
  return (
    <QueryProvider>
      <BrowserRouter>
        <ReLoginModalProvider>
          <AppRoutes />
          <Toaster position="top-right" richColors />
        </ReLoginModalProvider>
      </BrowserRouter>
    </QueryProvider>
  );
}

export default App;