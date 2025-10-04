"use client"

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { Badge } from "../../components/ui/badge"
import { Check, X, Key, Link, Zap } from "lucide-react"


export type TableSchema = {
  tableName: string
  schemaName?: string
  columns: {
    name: string
    dataType: string
    constraints?: string[]
  }[]
}

export default function TableSchemaViewer({
  schema,
  selectedTable,
}: {
  schema: TableSchema
  selectedTable: string
}) {
  const pkCols = schema.columns.filter((c) => (c.constraints || []).some((s) => s.toUpperCase().includes("PK")))
  const renderIcons = (constraints: string[] = []) => {
    const upper = constraints.map((s) => s.toUpperCase())
    return (
      <div className="inline-flex items-center gap-1">
        {upper.some((s) => s.includes("PK")) && (
          <Key className="h-3.5 w-3.5 text-foreground/80" aria-label="Primary key" />
        )}
        {upper.some((s) => s.includes("FK")) && (
          <Link className="h-3.5 w-3.5 text-foreground/60" aria-label="Foreign key" />
        )}
        {upper.some((s) => s.includes("IDENTITY")) && (
          <Zap className="h-3.5 w-3.5 text-foreground/60" aria-label="Identity" />
        )}
      </div>
    )
  }
  const isNullable = (constraints: string[] = []) => !constraints.some((s) => s.toUpperCase().includes("NOT NULL"))
  const defaultValue = (constraints: string[] = []) => {
    const d = constraints.find((s) => s.toUpperCase().startsWith("DEFAULT"))
    return d ? d.replace(/^DEFAULT\s*/i, "") : "NULL"
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="text-sm text-muted-foreground">
          {selectedTable ? `Selected: ${selectedTable}` : "Select a table to view schema"}
        </div>
        <Badge variant="outline" className="rounded-full">
          {schema.schemaName ?? "dbo"}
        </Badge>
      </div>

      <div className="border rounded-xl overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Column</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Nullable</TableHead>
              <TableHead>Default</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {schema.columns.map((col) => (
              <TableRow key={col.name}>
                <TableCell className="font-medium">
                  <div className="flex items-center gap-2">
                    {renderIcons(col.constraints)}
                    <span>{col.name}</span>
                  </div>
                </TableCell>
                <TableCell className="font-mono">{col.dataType}</TableCell>
                <TableCell>
                  {isNullable(col.constraints) ? (
                    <span className="inline-flex items-center gap-1 text-muted-foreground">
                      <Check className="h-3.5 w-3.5" /> Yes
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-muted-foreground">
                      <X className="h-3.5 w-3.5" /> No
                    </span>
                  )}
                </TableCell>
                <TableCell className="font-mono text-muted-foreground">{defaultValue(col.constraints)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="text-xs text-muted-foreground">
        PK: {pkCols.length ? pkCols.map((c) => c.name).join(", ") : "—"}
      </div>

      {/* TODO: Optional checkboxes for "Include in Create" / "Include in Update" */}
    </div>
  )
}