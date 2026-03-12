import { Separator } from "../ui/separator";
import ThemeToggle from "./ThemeToggle";
import { SidebarTrigger } from "../ui/sidebar";
import NotificationDropdown from "./NotificationDropdown";

export default function AppHeader() {
  return (
    <header className="w-full border-b bg-background sticky top-0 z-50">
      <div className="mx-auto px-4 py-3 flex items-center justify-between">
        {/* Left: Sidebar Trigger + Logo */}
        <div className="flex items-center gap-3">
          <SidebarTrigger />
          <Separator orientation="vertical" className="h-6" />
          <div className="font-semibold text-lg">Acto Engine</div>
        </div>

        {/* Right: Actions */}
        <div className="flex items-center gap-2">
          {/* Theme Toggle */}
          <ThemeToggle />

          {/* Notifications */}
          <NotificationDropdown />
        </div>
      </div>
    </header>
  );
}
