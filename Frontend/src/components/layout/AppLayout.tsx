import AppHeader from "./AppHeader"
import AppSidebar from "./AppSidebar"
import { Outlet } from "react-router-dom"

// components/layout/AppLayout.tsx
export default function AppLayout() {

  return (
    <div className="min-h-screen flex flex-col">
      {/* Global Header */}
      <AppHeader />

      <div className="flex-1 flex">
        {/* Global Sidebar - Navigation between features */}
        <AppSidebar />

        {/* Feature content area */}
        <main className="flex-1">
          <Outlet />
        </main>
      </div>
    </div>
  )
}