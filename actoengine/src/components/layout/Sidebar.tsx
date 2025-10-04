import { Database, FileCode, PanelLeft, PanelLeftClose, Settings, History } from "lucide-react"
import { Button } from "../ui/button"
import { NavLink, useLocation } from "react-router-dom"
import { cn } from "../../lib/utils"

// components/layout/AppSidebar.tsx
const SIDEBAR_ITEMS = [
  { icon: Database, label: "SP Generator", href: "/sp-generator" },
  { icon: FileCode, label: "Code Patterns", href: "/patterns" },
  { icon: History, label: "Generation History", href: "/history" },
  { icon: Settings, label: "Settings", href: "/settings" },
]

type AppSidebarProps = {
  isCollapsed: boolean
  onToggle: (collapsed: boolean) => void
}

export default function AppSidebar({ isCollapsed, onToggle }: AppSidebarProps) {
  const location = useLocation()
  
  return (
    <aside className={cn(
      "border-r bg-muted/30 transition-all duration-300",
      isCollapsed ? "w-16" : "w-64"
    )}>
      <div className="flex flex-col h-full">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => onToggle(!isCollapsed)}
          className="m-2"
        >
          {isCollapsed ? <PanelLeft /> : <PanelLeftClose />}
        </Button>
        
        <nav className="flex-1 p-2 space-y-1">
          {SIDEBAR_ITEMS.map(item => (
            <NavLink
              key={item.href}
              to={item.href}
              className={cn(
                "flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-accent",
                location.pathname === item.href && "bg-accent"
              )}
            >
              <item.icon className="h-5 w-5" />
              {!isCollapsed && <span>{item.label}</span>}
            </NavLink>
          ))}
        </nav>
      </div>
    </aside>
  )
}