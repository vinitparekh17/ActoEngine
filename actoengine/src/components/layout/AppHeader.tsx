import { Bell, LogOut, User, Database, ChevronDown } from "lucide-react";
import { Button } from "../ui/button";
import { Separator } from "../ui/separator";
import ThemeToggle from "./ThemeToggle";
import { useProject } from "../../hooks/useProject";
import { useAuth } from "../../hooks/useAuth";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";

export default function AppHeader() {
  const navigate = useNavigate();
  const { user, logout, isLoggingOut } = useAuth();
  const { 
    projects, 
    selectedProject, 
    selectProject, 
    isLoadingProjects 
  } = useProject();
  
  const [showProjectDropdown, setShowProjectDropdown] = useState(false);
  const [showUserMenu, setShowUserMenu] = useState(false);

  const handleProjectSelect = (projectId: number) => {
    const project = projects?.find(p => p.projectId === projectId);
    if (project) {
      selectProject(project);
      setShowProjectDropdown(false);
      toast.success(`Switched to ${project.projectName}`);
    }
  };

  // const user = { username: "johndoe", role: "Admin" }; // Mocked user data
  // const isLoggingOut = false; // Mocked logout state

  const handleLogout = () => {
    logout();
    toast.success("Logged out successfully");
    navigate("/login");
  };

  return (
    <header className="w-full border-b bg-background sticky top-0 z-50">
      <div className="mx-auto px-4 py-3 flex items-center justify-between">
        {/* Left: Logo + Project Selector */}
        <div className="flex items-center gap-3">
          <div className="font-semibold text-lg">ActoX</div>
          <Separator orientation="vertical" className="h-6" />
          
          {/* Project Selector Dropdown - Always show */}
          <div className="relative">
            <button
              onClick={() => setShowProjectDropdown(!showProjectDropdown)}
              className="flex items-center gap-2 rounded-lg border px-3 py-1.5 hover:bg-accent transition-colors"
            >
              <Database className="h-4 w-4 text-muted-foreground" />
              <span className="text-sm font-medium">
                {selectedProject ? selectedProject.projectName : "Select Project"}
              </span>
              <ChevronDown className="h-4 w-4 text-muted-foreground" />
            </button>

            {showProjectDropdown && (
              <>
                {/* Backdrop */}
                <div
                  className="fixed inset-0 z-10"
                  onClick={() => setShowProjectDropdown(false)}
                />
                
                {/* Dropdown Menu */}
                <div className="absolute left-0 top-full z-20 mt-2 w-64 rounded-lg border bg-popover shadow-lg">
                  <div className="max-h-96 overflow-y-auto p-2">
                    {isLoadingProjects ? (
                      <div className="px-3 py-2 text-sm text-muted-foreground">
                        Loading projects...
                      </div>
                    ) : projects?.length === 0 ? (
                      <div className="px-3 py-2 text-sm text-muted-foreground">
                        No projects available
                      </div>
                    ) : (
                      projects?.map((project) => (
                        <button
                          key={project.projectId}
                          onClick={() => handleProjectSelect(project.projectId)}
                          className={`w-full rounded-lg p-3 text-left transition-colors ${
                            selectedProject?.projectId === project.projectId
                              ? "bg-accent text-accent-foreground"
                              : "hover:bg-accent/50"
                          }`}
                        >
                          <div className="font-medium text-sm">{project.projectName}</div>
                          {project.databaseName && (
                            <div className="text-xs text-muted-foreground mt-0.5">
                              {project.databaseName}
                            </div>
                          )}
                        </button>
                      ))
                    )}
                  </div>
                </div>
              </>
            )}
          </div>
        </div>

        {/* Right: Actions */}
        <div className="flex items-center gap-2">
          {/* Theme Toggle */}
          <ThemeToggle />

          {/* Notifications */}
          <Button variant="ghost" size="icon">
            <Bell className="h-4 w-4" />
          </Button>

          {/* User Menu */}
          {user && (
            <div className="relative">
              <button
                onClick={() => setShowUserMenu(!showUserMenu)}
                className="flex items-center gap-2 rounded-lg border px-3 py-1.5 hover:bg-accent transition-colors"
              >
                <User className="h-4 w-4 text-muted-foreground" />
                <div className="text-left">
                  <div className="text-sm font-medium">{user.username}</div>
                  <div className="text-xs text-muted-foreground">{user.role}</div>
                </div>
              </button>

              {showUserMenu && (
                <>
                  {/* Backdrop */}
                  <div
                    className="fixed inset-0 z-10"
                    onClick={() => setShowUserMenu(false)}
                  />
                  
                  {/* Dropdown Menu */}
                  <div className="absolute right-0 top-full z-20 mt-2 w-48 rounded-lg border bg-popover shadow-lg">
                    <div className="p-2">
                      <button
                        onClick={handleLogout}
                        disabled={isLoggingOut}
                        className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm hover:bg-accent transition-colors disabled:opacity-50"
                      >
                        <LogOut className="h-4 w-4" />
                        {isLoggingOut ? "Logging out..." : "Logout"}
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>
          )}
        </div>
      </div>
    </header>
  );
}