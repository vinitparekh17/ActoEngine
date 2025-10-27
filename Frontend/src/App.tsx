import './App.css'
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom"
import { Toaster } from 'sonner'
import SPGeneratorPage from './pages/SpGen'
import AppLayout from './components/layout/Layout'
import { QueryProvider } from './providers/QueryProvider'
import { useAuth } from './hooks/useAuth'
import { ApiErrorBoundary } from './components/errors/ApiErrorBoundary'
import FormBuilderPage from './pages/FormBuilder'
import LoginPage from './pages/Login'
import DashboardPage from './pages/Dashboard'
import ClientManagementPage from './pages/ClientManagement'
import ProjectDashboard from './pages/ProjectDashboard'
import ProjectSetup from './pages/ProjectSetup'
import ProjectHub from './pages/ProjectHub'
import ProjectSettings from './pages/ProjectSetting'

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
  return (
    <Routes>
      <Route path='/login' element={<LoginPage />} />
      
      <Route
        path='/'
        element={
          <ProtectedRoute>
            <ApiErrorBoundary>
              <AppLayout />
            </ApiErrorBoundary>
          </ProtectedRoute>
        }
      >
        {/* Nested routes - these will render in the <Outlet /> */}
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path='projects' element={<ProjectDashboard />} />
        <Route path='project/:projectId' element={<ProjectHub />} />
        <Route path='project/:projectId/settings' element={<ProjectSettings />} />
        <Route path="project/new" element={<ProjectSetup />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="form-builder" element={<FormBuilderPage />} />
        <Route path="sp-generator" element={<SPGeneratorPage />} />
        <Route path="clients" element={<ClientManagementPage />} />
      </Route>
    </Routes>
  )
}

function App() {
  return (
    <QueryProvider>
      <BrowserRouter>
        <AppRoutes />
        <Toaster position="top-right" richColors />
      </BrowserRouter>
    </QueryProvider>
  )
}

export default App
