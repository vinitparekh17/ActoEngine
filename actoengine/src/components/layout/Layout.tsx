import { useState } from "react"
import { Outlet } from 'react-router-dom';
import AppHeader from "./AppHeader"
import AppSidebar from "./Sidebar"
import { ConfirmDialog } from "./ConfirmDialog";

// components/layout/AppLayout.tsx
export default function AppLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false)
  
  return (
    <div className="min-h-screen flex flex-col">
      {/* Global Header */}
      <AppHeader />
      
      <div className="flex-1 flex">
        {/* Global Sidebar - Navigation between features */}
        <AppSidebar 
          isCollapsed={isSidebarCollapsed} 
          onToggle={setIsSidebarCollapsed} 
        />
        
        {/* Feature content area */}
        <main className="flex-1">
          <ConfirmDialog />
          <div className="p-6">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}