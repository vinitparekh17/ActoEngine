import { Outlet } from 'react-router-dom';
import AppHeader from "./AppHeader"
import AppSidebar from "./AppSidebar"
import { ConfirmDialog } from "./ConfirmDialog";
import { SidebarProvider, SidebarInset } from "../ui/sidebar";

// components/layout/AppLayout.tsx
export default function AppLayout() {
  return (
    <SidebarProvider defaultOpen={true}>
      <div className="min-h-screen flex w-full">
        {/* Global Sidebar - Navigation between features */}
        <AppSidebar />
        
        {/* Main content area */}
        <SidebarInset>
          {/* Global Header */}
          <AppHeader />
          
          {/* Feature content area */}
          <main className="flex-1">
            <ConfirmDialog />
            <div className="p-6">
              <Outlet />
            </div>
          </main>
        </SidebarInset>
      </div>
    </SidebarProvider>
  )
}