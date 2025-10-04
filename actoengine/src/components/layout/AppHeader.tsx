import { Bell } from "lucide-react";
import ProjectSelector, { type ProjectOption } from "../project/ProjectSelector";
import { Button } from "../ui/button";
import { Separator } from "../ui/separator";
import ThemeToggle from "./ThemeToggle";
import { useCallback, useMemo, useState } from "react";
import { useToast } from "../../hooks/useToast";

// components/layout/AppHeader.tsx
export default function AppHeader() {

    const { showToast: toast } = useToast()
    const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
      const projects = useMemo<ProjectOption[]>(
        () => [
          { id: "proj_1", name: "Marketing DB" },
          { id: "proj_2", name: "Product DB" },
        ],
        [],
      )
    const handleProjectSelect = useCallback(
        (id: string) => {
            setSelectedProjectId(id)
            toast({ title: "Project selected", description: id })
        },
        [toast],
    )
    return (
        <header className="w-full border-b bg-background sticky top-0 z-50">
            <div className="mx-auto px-4 py-3 flex items-center justify-between">
                <div className="flex items-center gap-3">
                    <div className="font-semibold text-lg">ActoX</div>
                    <Separator orientation="vertical" className="h-6" />
                    <ProjectSelector projects={projects} onSelect={handleProjectSelect} />
                </div>

                <div className="flex items-center gap-2">
                    <ThemeToggle />
                    <Button variant="ghost" size="icon">
                        <Bell className="h-4 w-4" />
                    </Button>
                    {/* <UserMenu /> */}
                </div>
            </div>
        </header>
    )
}