"use client"

import { useState } from "react"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../ui/table"
import { Badge } from "../ui/badge"
import { Check, X, Key, Link, Zap } from "lucide-react"
import { Checkbox } from "../ui/checkbox"


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
  showCrudCheckboxes = false,
}: {
  schema: TableSchema
  selectedTable: string
  showCrudCheckboxes?: boolean
}) {
  const pkCols = schema.columns.filter((c) => (c.constraints || []).some((s) => s.toUpperCase().includes("PK")))

  // Track which columns are included in Create/Update operations
  const [includeInCreate, setIncludeInCreate] = useState<Record<string, boolean>>(
    schema.columns.reduce((acc, col) => {
      // By default, exclude PK and IDENTITY columns from Create
      const isPK = (col.constraints || []).some((s) => s.toUpperCase().includes("PK"))
      const isIdentity = (col.constraints || []).some((s) => s.toUpperCase().includes("IDENTITY"))
      acc[col.name] = !isPK && !isIdentity
      return acc
    }, {} as Record<string, boolean>)
  )

  const [includeInUpdate, setIncludeInUpdate] = useState<Record<string, boolean>>(
    schema.columns.reduce((acc, col) => {
      // By default, exclude PK and IDENTITY columns from Update
      const isPK = (col.constraints || []).some((s) => s.toUpperCase().includes("PK"))
      const isIdentity = (col.constraints || []).some((s) => s.toUpperCase().includes("IDENTITY"))
      acc[col.name] = !isPK && !isIdentity
      return acc
    }, {} as Record<string, boolean>)
  )
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
              {showCrudCheckboxes && (
                <>
                  <TableHead className="text-center">
                    <div className="flex flex-col items-center gap-0.5">
                      <span>Create</span>
                      <span className="text-xs font-normal text-muted-foreground">(INSERT)</span>
                    </div>
                  </TableHead>
                  <TableHead className="text-center">
                    <div className="flex flex-col items-center gap-0.5">
                      <span>Update</span>
                      <span className="text-xs font-normal text-muted-foreground">(UPDATE)</span>
                    </div>
                  </TableHead>
                </>
              )}
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
                {showCrudCheckboxes && (
                  <>
                    <TableCell className="text-center">
                      <Checkbox
                        checked={includeInCreate[col.name] || false}
                        onCheckedChange={(checked) =>
                          setIncludeInCreate({ ...includeInCreate, [col.name]: checked as boolean })
                        }
                      />
                    </TableCell>
                    <TableCell className="text-center">
                      <Checkbox
                        checked={includeInUpdate[col.name] || false}
                        onCheckedChange={(checked) =>
                          setIncludeInUpdate({ ...includeInUpdate, [col.name]: checked as boolean })
                        }
                      />
                    </TableCell>
                  </>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="text-xs text-muted-foreground">
        PK: {pkCols.length ? pkCols.map((c) => c.name).join(", ") : "—"}
        {showCrudCheckboxes && (
          <span className="ml-4 text-blue-600">
            Create: {Object.values(includeInCreate).filter(Boolean).length} columns |
            Update: {Object.values(includeInUpdate).filter(Boolean).length} columns
          </span>
        )}
      </div>
    </div>
  )
}