import { Database, History, Palette, Users, Settings, ChevronDown, LogOut, ChevronsUpDown, User, Layers, Sparkles, BrainCircuit } from "lucide-react"
import { NavLink, useLocation, useNavigate } from "react-router-dom"
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarHeader,
  SidebarFooter,
  SidebarRail,
} from "../ui/sidebar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu"
import { useProject } from "../../hooks/useProject"
import { useAuth } from "../../hooks/useAuth"
import { toast } from "sonner"

// components/layout/AppSidebar.tsx
const SIDEBAR_ITEMS = [
  { icon: Sparkles, label: 'Projects', href: '/projects' },
  { icon: Database, label: "SP Generator", href: "/sp-builder" },
  { icon: Palette, label: "Form Builder", href: "/form-builder" },
  { icon: BrainCircuit, label: "Context Dashboard", href: "/context" },
  { icon: Users, label: "Client Management", href: "/clients" },
  { icon: History, label: "Generation History", href: "/history" },
  { icon: Settings, label: "Settings", href: "/settings" },
]

export default function AppSidebar() {
  const location = useLocation()
  const navigate = useNavigate()
  const { 
    projects, 
    selectedProject, 
    selectProject, 
    isLoadingProjects 
  } = useProject()
  const { user, logout, isLoggingOut } = useAuth()

  const handleProjectSelect = (projectId: number) => {
    const project = projects?.find(p => p.projectId === projectId)
    if (project) {
      try {
        selectProject(project)
        toast.success(`Switched to ${project.projectName}`)
      } catch (err) {
        toast.error("Failed to switch project")
        console.error("Project switch error:", err)
      }
    }
  }

  const handleLogout = () => {
    logout()
    toast.success("Logged out successfully")
    navigate("/login")
  }
  
  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton tooltip={selectedProject?.projectName || "Select Project"}>
                  <Layers className="!size-5" />
                  <div className="flex flex-col gap-0.5 leading-none">
                    <span className="font-semibold">
                      {selectedProject ? selectedProject.projectName : "Select Project"}
                    </span>
                    {selectedProject?.databaseName && (
                      <span className="text-xs text-muted-foreground">
                        {selectedProject.databaseName}
                      </span>
                    )}
                  </div>
                  <ChevronDown className="ml-auto" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent className="w-[--radix-popper-anchor-width]" align="start">
                {isLoadingProjects ? (
                  <DropdownMenuItem disabled>
                    <span className="text-muted-foreground">Loading projects...</span>
                  </DropdownMenuItem>
                ) : projects?.length === 0 ? (
                  <DropdownMenuItem disabled>
                    <span className="text-muted-foreground">No projects available</span>
                  </DropdownMenuItem>
                ) : (
                  projects?.map((project) => (
                    <DropdownMenuItem
                      key={project.projectId}
                      onClick={() => handleProjectSelect(project.projectId)}
                      className="flex flex-col items-start gap-0.5"
                    >
                      <span className="font-medium">{project.projectName}</span>
                      {project.databaseName && (
                        <span className="text-xs text-muted-foreground">
                          {project.databaseName}
                        </span>
                      )}
                    </DropdownMenuItem>
                  ))
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {SIDEBAR_ITEMS.map(item => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    asChild
                    isActive={location.pathname === item.href}
                    tooltip={item.label}
                  >
                    <NavLink to={item.href}>
                      <item.icon />
                      <span>{item.label}</span>
                    </NavLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
      
      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton
                  tooltip={user ? `${user.username} (${user.role})` : "User"}
                >
                  <User className="!size-5" />
                  <div className="flex flex-col gap-0.5 leading-none">
                    <span className="font-semibold text-sm">
                      {user?.username || "Guest"}
                    </span>
                    <span className="text-xs text-muted-foreground">
                      {user?.role || "No role"}
                    </span>
                  </div>
                  <ChevronsUpDown className="ml-auto" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                className="w-[--radix-popper-anchor-width]"
                align="start"
                side="top"
              >
                <DropdownMenuItem
                  onClick={handleLogout}
                  disabled={isLoggingOut}
                  className="cursor-pointer"
                >
                  <LogOut className="mr-2 h-4 w-4" />
                  <span>{isLoggingOut ? "Logging out..." : "Logout"}</span>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  )
}