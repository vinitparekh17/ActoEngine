"use client";

import { useEffect } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { z } from "zod";
// import { zodResolver } from "@hookform/resolvers/zod"
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import { Checkbox } from "../ui/checkbox";
import { Label } from "../ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../ui/select";
import type { SPType } from "./SPTypeCard";
import { Trash2, Plus } from "lucide-react";
import SPTypeCard from "./SPTypeCard";
import TableSchemaViewer, {
  type TableSchema,
} from "../database/TableSchemaViewer";

const CUDSchema = z.object({
  mode: z.literal("CUD"),
  generateCreate: z.boolean().default(true),
  generateUpdate: z.boolean().default(true),
  generateDelete: z.boolean().default(true),
  spPrefix: z.string().min(1).default("usp"),
  includeErrorHandling: z.boolean().default(true),
  includeTransaction: z.boolean().default(true),
  actionParamName: z.string().min(1).default("Action"),
});

const FilterSchema = z.object({
  column: z.string().min(1, "Column required"),
  operator: z.enum(["=", "LIKE", ">", "<", "BETWEEN"]),
  optional: z.boolean().default(false),
});

const SELECTSchema = z.object({
  mode: z.literal("SELECT"),
  includePagination: z.boolean().default(true),
  orderBy: z.array(z.string()).default([]),
  filters: z.array(FilterSchema).default([]),
});

const ConfigSchema = z.discriminatedUnion("mode", [CUDSchema, SELECTSchema]);

export type SPConfigValues = z.infer<typeof ConfigSchema>;

export default function SPConfigPanel({
  spType,
  config,
  onSubmit,
  schema,
  onChangeType,
}: {
  spType: SPType;
  config: SPConfigValues;
  onSubmit: (values: SPConfigValues) => void;
  schema: TableSchema;
  onChangeType: (type: SPType) => void;
}) {
  const form = useForm<SPConfigValues>({
    // resolver: zodResolver(ConfigSchema),
    defaultValues: config,
    mode: "onBlur",
  });

  // For SELECT: field array for filters
  const { fields, append, remove } = useFieldArray({
    control: form.control as any,
    name: "filters" as any,
  });

  // Reset form to correct mode defaults whenever spType changes
  useEffect(() => {
    if (spType === "CUD") {
      form.reset({
        mode: "CUD",
        generateCreate: true,
        generateUpdate: true,
        generateDelete: true,
        spPrefix: "usp",
        includeErrorHandling: true,
        includeTransaction: true,
        actionParamName: "Action",
      } as any);
    } else {
      form.reset({
        mode: "SELECT",
        includePagination: true,
        orderBy: [],
        filters: [],
      } as any);
    }
  }, [spType, form.reset]);

  const submit = form.handleSubmit((values) => {
    // Normalize orderBy if user typed comma-separated string into hidden field
    if (values.mode === "SELECT") {
      const ob = (form.getValues("orderBy") || []) as string[];
      const normalized = ob
        .flatMap((v) => v.split(","))
        .map((s) => s.trim())
        .filter(Boolean);
      onSubmit({ ...values, orderBy: normalized });
    } else {
      onSubmit(values);
    }
  });

  const availableColumns = schema.columns.map((c) => c.name);
  return (
    <>
      <TableSchemaViewer schema={schema} selectedTable={schema.tableName} />
      <div className="mb-6">
        <label className="text-sm font-medium mb-3 block">Procedure Type</label>
        <div className="grid grid-cols-2 gap-3">
          <SPTypeCard
            type="CUD"
            selected={spType === "CUD"}
            onChange={onChangeType}
          />
          <SPTypeCard
            type="SELECT"
            selected={spType === "SELECT"}
            onChange={onChangeType}
          />
        </div>
      </div>
      <form onSubmit={submit} className="space-y-6">
        {form.watch("mode") === "CUD" ? (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-4">
              <div className="space-y-2">
                <Label className="text-sm" htmlFor="spPrefix">
                  SP Prefix
                </Label>
                <Input
                  id="spPrefix"
                  value={(form.watch("spPrefix") as string) ?? ""}
                  onChange={(e) =>
                    form.setValue("spPrefix" as any, e.target.value as any)
                  }
                />
              </div>

              <div className="flex flex-wrap gap-4">
                <div className="flex items-center gap-2">
                  <Checkbox
                    checked={
                      (form.watch("includeErrorHandling") as boolean) ?? false
                    }
                    onCheckedChange={(v) =>
                      form.setValue(
                        "includeErrorHandling" as any,
                        Boolean(v) as any,
                      )
                    }
                    id="includeErrorHandling"
                  />
                  <Label htmlFor="includeErrorHandling" className="text-sm">
                    Include Error Handling
                  </Label>
                </div>
                <div className="flex items-center gap-2">
                  <Checkbox
                    checked={
                      (form.watch("includeTransaction") as boolean) ?? false
                    }
                    onCheckedChange={(v) =>
                      form.setValue(
                        "includeTransaction" as any,
                        Boolean(v) as any,
                      )
                    }
                    id="includeTransaction"
                  />
                  <Label htmlFor="includeTransaction" className="text-sm">
                    Include Transaction
                  </Label>
                </div>
              </div>

              <div className="space-y-3">
                <Label className="text-sm">Operations to Generate</Label>
                <div className="flex flex-wrap gap-4">
                  <div className="flex items-center gap-2">
                    <Checkbox
                      checked={
                        (form.watch("generateCreate") as boolean) ?? true
                      }
                      onCheckedChange={(v) =>
                        form.setValue(
                          "generateCreate" as any,
                          Boolean(v) as any,
                        )
                      }
                      id="generateCreate"
                    />
                    <Label htmlFor="generateCreate" className="text-sm">
                      Create
                    </Label>
                  </div>
                  <div className="flex items-center gap-2">
                    <Checkbox
                      checked={
                        (form.watch("generateUpdate") as boolean) ?? true
                      }
                      onCheckedChange={(v) =>
                        form.setValue(
                          "generateUpdate" as any,
                          Boolean(v) as any,
                        )
                      }
                      id="generateUpdate"
                    />
                    <Label htmlFor="generateUpdate" className="text-sm">
                      Update
                    </Label>
                  </div>
                  <div className="flex items-center gap-2">
                    <Checkbox
                      checked={
                        (form.watch("generateDelete") as boolean) ?? true
                      }
                      onCheckedChange={(v) =>
                        form.setValue(
                          "generateDelete" as any,
                          Boolean(v) as any,
                        )
                      }
                      id="generateDelete"
                    />
                    <Label htmlFor="generateDelete" className="text-sm">
                      Delete
                    </Label>
                  </div>
                </div>
              </div>

              <div className="space-y-2">
                <Label className="text-sm" htmlFor="actionParamName">
                  Action Parameter Name
                </Label>
                <Input
                  id="actionParamName"
                  value={(form.watch("actionParamName") as string) ?? ""}
                  onChange={(e) =>
                    form.setValue(
                      "actionParamName" as any,
                      e.target.value as any,
                    )
                  }
                />
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-6">
            <div className="flex flex-wrap items-center gap-6">
              <div className="flex items-center gap-2">
                <Checkbox
                  checked={
                    (form.watch("includePagination") as boolean) ?? false
                  }
                  onCheckedChange={(v) =>
                    form.setValue("includePagination" as any, Boolean(v) as any)
                  }
                  id="includePagination"
                />
                <Label htmlFor="includePagination" className="text-sm">
                  Include Pagination
                </Label>
              </div>

              <div className="space-y-2 min-w-[260px]">
                <Label className="text-sm" htmlFor="orderBy">
                  Order By Columns (comma-separated)
                </Label>
                <Input
                  id="orderBy"
                  placeholder="e.g. created_at DESC, email ASC"
                  value={(form.watch("orderBy") as string[]).join(", ")}
                  onChange={(e) =>
                    form.setValue(
                      "orderBy" as any,
                      [e.target.value] as any, // store raw, will normalize on submit
                    )
                  }
                />
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <Label className="text-sm">Filters</Label>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  onClick={() =>
                    append({
                      column: availableColumns[0] ?? "",
                      operator: "=",
                      optional: false,
                    })
                  }
                >
                  <Plus className="h-4 w-4 mr-1" />
                  Add Filter
                </Button>
              </div>

              <div className="space-y-2">
                {fields.length === 0 && (
                  <div className="text-sm text-muted-foreground">
                    No filters added.
                  </div>
                )}
                {fields.map((field, idx) => (
                  <div
                    key={field.id}
                    className="grid grid-cols-1 md:grid-cols-[1fr_160px_110px_40px] items-end gap-3"
                  >
                    <div className="space-y-1">
                      <Label className="text-xs">Column</Label>
                      <Select
                        value={
                          (form.watch(`filters.${idx}.column`) as string) ?? ""
                        }
                        onValueChange={(v) =>
                          form.setValue(
                            `filters.${idx}.column` as any,
                            v as any,
                          )
                        }
                      >
                        <SelectTrigger className="rounded-xl">
                          <SelectValue placeholder="Select column" />
                        </SelectTrigger>
                        <SelectContent>
                          {availableColumns.map((c) => (
                            <SelectItem key={c} value={c}>
                              {c}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="space-y-1">
                      <Label className="text-xs">Operator</Label>
                      <Select
                        value={
                          (form.watch(`filters.${idx}.operator`) as any) ?? "="
                        }
                        onValueChange={(v) =>
                          form.setValue(
                            `filters.${idx}.operator` as any,
                            v as any,
                          )
                        }
                      >
                        <SelectTrigger className="rounded-xl">
                          <SelectValue placeholder="Op" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="=">=</SelectItem>
                          <SelectItem value="LIKE">LIKE</SelectItem>
                          <SelectItem value=">">{">"}</SelectItem>
                          <SelectItem value="<">{"<"}</SelectItem>
                          <SelectItem value="BETWEEN">BETWEEN</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="flex items-center gap-2">
                      <Checkbox
                        checked={
                          (form.watch(`filters.${idx}.optional`) as boolean) ??
                          false
                        }
                        onCheckedChange={(v) =>
                          form.setValue(
                            `filters.${idx}.optional` as any,
                            Boolean(v) as any,
                          )
                        }
                        id={`optional-${idx}`}
                      />
                      <Label htmlFor={`optional-${idx}`} className="text-xs">
                        Optional
                      </Label>
                    </div>

                    <div className="flex items-center justify-end">
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        onClick={() => remove(idx)}
                        aria-label="Remove filter"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        <div className="pt-2">
          <Button type="submit" className="rounded-xl">
            Apply
          </Button>
        </div>
      </form>
    </>
  );
}
