import { Outlet } from "react-router-dom";
import AppHeader from "./AppHeader";
import AppSidebar from "./AppSidebar";
import { ConfirmDialog } from "./ConfirmDialog";
import { SidebarProvider, SidebarInset } from "../ui/sidebar";

/**
 * Application layout that provides sidebar context and renders the global navigation, header, confirmation dialog, and routed content.
 *
 * Wraps children with a SidebarProvider (sidebar open by default), places the global sidebar outside the main content flow, and renders the header, confirmation dialog, and the route Outlet inside the main content area.
 *
 * @returns The root JSX element for the application's layout
 */
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
  );
}