"use client";

import * as React from "react";
import {
  type ColumnDef,
  getCoreRowModel,
  useReactTable,
  flexRender,
} from "@tanstack/react-table";
import { Skeleton } from "../ui/skeletons";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../ui/table";
import { cn } from "../../lib/utils";

export type DataTableColumn = { header: string; accessorKey: string };

export default function DataTable<TData extends { id?: string | number }>({
  rows,
  columns,
  onRowClick,
  className,
  isLoading = false,
}: {
  rows: TData[];
  columns: DataTableColumn[];
  onRowClick?: (row: TData) => void;
  className?: string;
  isLoading?: boolean;
}) {
  const colDefs = React.useMemo<ColumnDef<TData>[]>(() => {
    return columns.map((c) => ({
      header: c.header,
      accessorKey: c.accessorKey as any,
      cell: ({ getValue }) => (
        <span className="text-sm">{String(getValue() ?? "")}</span>
      ),
    }));
  }, [columns]);

  const table = useReactTable({
    data: rows,
    columns: colDefs,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div className={cn("border rounded-xl overflow-hidden", className)}>
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((hg) => (
            <TableRow key={hg.id}>
              {hg.headers.map((h) => (
                <TableHead key={h.id} className="text-xs">
                  {h.isPlaceholder
                    ? null
                    : flexRender(h.column.columnDef.header, h.getContext())}
                </TableHead>
              ))}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {isLoading
            ? Array.from({ length: 5 }).map((_, i) => (
              <TableRow key={`skeleton-${i}`}>
                {columns.map((_col, j) => (
                  <TableCell key={`${i}-${j}`}>
                    <Skeleton className="h-4 w-[80%]" />
                  </TableCell>
                ))}
              </TableRow>
            ))
            : table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                className="cursor-pointer hover:bg-accent/50"
                onClick={() => onRowClick?.(row.original)}
              >
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id} className="text-sm">
                    {flexRender(
                      cell.column.columnDef.cell,
                      cell.getContext(),
                    )}
                  </TableCell>
                ))}
              </TableRow>
            ))}
        </TableBody>
      </Table>
    </div>
  );
}
