import { Outlet } from "react-router-dom";
import AppHeader from "./AppHeader";
import AppSidebar from "./AppSidebar";
import { ConfirmDialog } from "./ConfirmDialog";
import { SidebarProvider, SidebarInset } from "../ui/sidebar";

import { useFullscreen } from "@/hooks/useFullscreen";

// components/layout/AppLayout.tsx
export default function AppLayout() {
  const { isFullscreen } = useFullscreen();

  return (
    <SidebarProvider defaultOpen={true}>
      <div className="flex min-h-screen w-full">
        {/* Global Sidebar - Navigation between features */}
        {!isFullscreen && <AppSidebar />}

        {/* Main content area */}
        <SidebarInset>
          {/* Global Header */}
          {!isFullscreen && <AppHeader />}

          {/* Feature content area */}
          <main className="flex-1">
            <ConfirmDialog />
            <div className={isFullscreen ? "" : "p-6"}>
              <Outlet />
            </div>
          </main>
        </SidebarInset>
      </div>
    </SidebarProvider>
  );
}
