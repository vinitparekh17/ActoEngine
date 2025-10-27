"use client"

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "../ui/select"

export type ProjectOption = { id: string; name: string }

export default function ProjectSelector({
  projects,
  onSelect,
}: {
  projects: ProjectOption[]
  onSelect: (projectId: string) => void
}) {
  return (
    <Select onValueChange={onSelect}>
      <SelectTrigger className="w-56 rounded-xl">
        <SelectValue placeholder="Select project" />
      </SelectTrigger>
      <SelectContent>
        {projects.map((p) => (
          <SelectItem key={p.id} value={p.id}>
            {p.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
