import {
  Palette,
  Users,
  ChevronDown,
  LogOut,
  ChevronsUpDown,
  User,
  Layers,
  Sparkles,
  Home,
  Shield,
  UserCog,
  Search,
  Binary,
  Key,
} from "lucide-react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";
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
} from "../ui/sidebar";
import { Skeleton } from "../ui/skeleton";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import { useProject } from "../../hooks/useProject";
import { useAuth } from "../../hooks/useAuth";

import { toast } from "sonner";
import { PasswordChangeModal } from "../../pages/UserManagement/PasswordChangeModal";
import { useApiMutation, queryKeys } from "../../hooks/useApi";
import { useState } from "react";

// components/layout/AppSidebar.tsx
const SIDEBAR_ITEMS = [
  { icon: Home, label: "Dashboard", href: "/" },
  { icon: Sparkles, label: "Projects", href: "/projects" },
  { icon: Binary, label: "SP Generator", href: "/sp-builder" },
  { icon: Palette, label: "Form Builder", href: "/form-builder" },
  { icon: Users, label: "Client Management", href: "/clients" },
  {
    icon: UserCog,
    label: "User Management",
    href: "/admin/users",
    requiresRole: "Admin",
  },
  {
    icon: Shield,
    label: "Role Management",
    href: "/admin/roles",
    requiresRole: "Admin",
  },
];

export default function AppSidebar() {
  const location = useLocation();
  const navigate = useNavigate();
  const { projects, selectedProject, selectProject, isLoadingProjects } =
    useProject();
  const { user, logout, isLoggingOut } = useAuth();
  const [isPasswordModalOpen, setIsPasswordModalOpen] = useState(false);

  // Password change mutation
  const changePasswordMutation = useApiMutation<
    void,
    { userId: number; newPassword: string }
  >("/UserManagement/:userId/change-password", "POST", {
    successMessage: "Password changed successfully",
    invalidateKeys: [Array.from(queryKeys.users.all())],
  });

  const handlePasswordChange = (data: { newPassword: string }) => {
    if (!user?.userId) return;

    changePasswordMutation.mutate(
      { userId: user.userId, newPassword: data.newPassword },
      {
        onSuccess: () => {
          setIsPasswordModalOpen(false);
          handleLogout();
        },
      },
    );
  };

  const handleProjectSelect = (projectId: number) => {
    const project = projects?.find((p) => p.projectId === projectId);
    if (project) {
      try {
        selectProject(project);
        toast.success(`Switched to ${project.projectName}`);
      } catch (err) {
        toast.error("Failed to switch project");
        console.error("Project switch error:", err);
      }
    }
  };

  const handleLogout = async () => {
    try {
      logout();
      toast.success("Logged out successfully");
      navigate("/login");
    } catch (error) {
      toast.error("Failed to logout. Please try again.");
      console.error("Logout error:", error);
    }
  };

  const renderProjectDropdownItems = () => {
    if (isLoadingProjects) {
      return (
        <DropdownMenuItem disabled>
          <span className="text-muted-foreground">
            <Skeleton className="h-4 w-32" />
          </span>
        </DropdownMenuItem>
      );
    }

    if (projects?.length === 0) {
      return (
        <DropdownMenuItem disabled>
          <span className="text-muted-foreground">No projects available</span>
        </DropdownMenuItem>
      );
    }

    return projects?.map((project) => (
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
    ));
  };

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton
                  tooltip={selectedProject?.projectName || "Select Project"}
                >
                  <Layers className="!size-5" />
                  <div className="flex flex-col gap-0.5 leading-none">
                    <span className="font-semibold">
                      {selectedProject
                        ? selectedProject.projectName
                        : "Select Project"}
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
              <DropdownMenuContent
                className="w-[--radix-popper-anchor-width]"
                align="start"
              >
                {renderProjectDropdownItems()}
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {SIDEBAR_ITEMS.filter(
                (item) =>
                  !item.requiresRole || user?.role === item.requiresRole,
              ).map((item) => (
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

              {/* Project Specific Links */}
              {selectedProject && (
                <>
                  <SidebarMenuItem>
                    <SidebarMenuButton
                      asChild
                      isActive={location.pathname.includes("/entities")}
                      tooltip="Entity Explorer"
                    >
                      <NavLink
                        to={`/project/${selectedProject.projectId}/entities`}
                      >
                        <Search />
                        <span>Entity Explorer</span>
                      </NavLink>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                </>
              )}
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
                  onClick={() => setIsPasswordModalOpen(true)}
                  disabled={!user?.userId}
                  className="cursor-pointer"
                >
                  <Key className="mr-2 h-4 w-4" />
                  <span>Change Password</span>
                </DropdownMenuItem>
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
      <PasswordChangeModal
        isOpen={isPasswordModalOpen}
        user={user}
        isPending={changePasswordMutation.isPending}
        onClose={setIsPasswordModalOpen}
        onSubmit={handlePasswordChange}
      />
    </Sidebar>
  );
}
