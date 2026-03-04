import { useEffect } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { Settings2, ArrowRight, Plus, SlidersHorizontal, Trash2 } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import SPTypeCard, { type SPType } from "./SPTypeCard";
import TableSchemaViewer, { type TableSchema } from "@/components/database/TableSchemaViewer";
import type { SPConfigValues } from "@/schema/spBuilderSchema";
import { getDefaultSpConfig } from "./spConfigDefaults";

const getDefaultColumnConfigs = (schema: TableSchema) => {
  return schema.columns.reduce(
    (acc, col) => {
      const isPK = (col.constraints || []).some((s) => s.toUpperCase().includes("PK"));
      const isIdentity = (col.constraints || []).some((s) => s.toUpperCase().includes("IDENTITY"));
      const include = !isPK && !isIdentity;

      acc.create[col.name] = include;
      acc.update[col.name] = include;
      return acc;
    },
    { create: {} as Record<string, boolean>, update: {} as Record<string, boolean> }
  );
};

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
    defaultValues: config,
    mode: "onBlur",
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control as any,
    name: "filters" as any,
  });

  // Full reset only when the SP type is switched (CUD ↔ SELECT)
  useEffect(() => {
    if (spType === "CUD") {
      form.reset(getDefaultSpConfig("CUD") as any);
    } else {
      form.reset(getDefaultSpConfig("SELECT") as any);
    }
  }, [spType]); // eslint-disable-line react-hooks/exhaustive-deps

  // Only refresh column include-maps when the selected table actually changes.
  useEffect(() => {
    if (spType === "CUD" && schema.columns.length > 0) {
      const defaultCols = getDefaultColumnConfigs(schema);
      form.setValue("includeInCreate" as any, defaultCols.create);
      form.setValue("includeInUpdate" as any, defaultCols.update);
    }
  }, [schema.tableName, spType]); // eslint-disable-line react-hooks/exhaustive-deps

  const submit = form.handleSubmit((values) => {
    if (values.mode === "SELECT") {
      const ob = (form.getValues("orderBy") || []) as string[];
      const normalized = ob.flatMap((v) => v.split(",")).map((s) => s.trim()).filter(Boolean);
      onSubmit({ ...values, orderBy: normalized });
    } else {
      onSubmit(values); // This will now naturally include includeInCreate/includeInUpdate!
    }
  });

  const availableColumns = schema.columns.map((c) => c.name);

  return (
    <div className="flex flex-col gap-8 animate-in fade-in slide-in-from-bottom-4 duration-500">

      {/* Schema Viewer Section */}
      <div className="bg-card rounded-2xl border border-border/40 p-1 shadow-sm overflow-hidden">
        <TableSchemaViewer
          schema={schema}
          selectedTable={schema.tableName}
          showCrudCheckboxes={spType === "CUD"}
          includeInCreate={(form.watch("includeInCreate") as Record<string, boolean>) || {}}
          includeInUpdate={(form.watch("includeInUpdate") as Record<string, boolean>) || {}}
          onToggleCreate={(colName, checked) => {
            const current = (form.getValues("includeInCreate") as Record<string, boolean>) || {};
            form.setValue("includeInCreate" as any, { ...current, [colName]: checked }, { shouldDirty: true });
          }}
          onToggleUpdate={(colName, checked) => {
            const current = (form.getValues("includeInUpdate") as Record<string, boolean>) || {};
            form.setValue("includeInUpdate" as any, { ...current, [colName]: checked }, { shouldDirty: true });
          }}
        />
      </div>

      {/* Procedure Type Section */}
      <div className="space-y-4">
        <div className="flex items-center gap-2 border-b border-border/40 pb-2">
          <Settings2 className="h-5 w-5 text-primary" />
          <h3 className="text-base font-semibold tracking-tight">Procedure Type</h3>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <SPTypeCard type="CUD" selected={spType === "CUD"} onChange={onChangeType} />
          <SPTypeCard type="SELECT" selected={spType === "SELECT"} onChange={onChangeType} />
        </div>
      </div>

      {/* Form Configuration */}
      <form onSubmit={submit} className="space-y-8">
        {form.watch("mode") === "CUD" ? (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* General Settings */}
            <div className="bg-card border border-border/40 shadow-sm rounded-2xl p-6 space-y-6">
              <div className="space-y-1">
                <h4 className="text-sm font-bold uppercase tracking-wider text-muted-foreground">General Configuration</h4>
                <p className="text-xs text-muted-foreground/80">Configure naming conventions and wrappers.</p>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                <div className="space-y-2">
                  <Label className="text-xs font-semibold text-foreground/80" htmlFor="spPrefix">SP Prefix</Label>
                  <Input
                    id="spPrefix"
                    className="bg-background h-10 transition-colors focus-visible:ring-primary/30"
                    value={(form.watch("spPrefix") as string) ?? ""}
                    onChange={(e) => form.setValue("spPrefix" as any, e.target.value as any)}
                  />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-semibold text-foreground/80" htmlFor="actionParamName">Action Param Name</Label>
                  <Input
                    id="actionParamName"
                    className="bg-background h-10 transition-colors focus-visible:ring-primary/30"
                    value={(form.watch("actionParamName") as string) ?? ""}
                    onChange={(e) => form.setValue("actionParamName" as any, e.target.value as any)}
                  />
                </div>
              </div>

              <div className="flex flex-col sm:flex-row gap-4 pt-2">
                <label className="flex flex-1 items-center gap-3 p-3 rounded-xl border border-border/40 bg-muted/20 cursor-pointer hover:bg-muted/40 transition-colors group">
                  <Checkbox
                    checked={(form.watch("includeErrorHandling") as boolean) ?? false}
                    onCheckedChange={(v) => form.setValue("includeErrorHandling" as any, Boolean(v) as any)}
                  />
                  <span className="text-sm font-medium group-hover:text-primary transition-colors">Error Handling</span>
                </label>
                <label className="flex flex-1 items-center gap-3 p-3 rounded-xl border border-border/40 bg-muted/20 cursor-pointer hover:bg-muted/40 transition-colors group">
                  <Checkbox
                    checked={(form.watch("includeTransaction") as boolean) ?? false}
                    onCheckedChange={(v) => form.setValue("includeTransaction" as any, Boolean(v) as any)}
                  />
                  <span className="text-sm font-medium group-hover:text-primary transition-colors">Transaction Wrap</span>
                </label>
              </div>
            </div>

            {/* Operations */}
            <div className="bg-card border border-border/40 shadow-sm rounded-2xl p-6 space-y-6">
              <div className="space-y-1">
                <h4 className="text-sm font-bold uppercase tracking-wider text-muted-foreground">Operations to Generate</h4>
                <p className="text-xs text-muted-foreground/80">Select which statements to include in the CUD.</p>
              </div>

              <div className="flex flex-col gap-3">
                {(["generateCreate", "generateUpdate", "generateDelete"] as const).map((key) => {
                  const labels: Record<string, { title: string; desc: string }> = {
                    generateCreate: { title: "Create Operation", desc: "Include INSERT statement logic" },
                    generateUpdate: { title: "Update Operation", desc: "Include UPDATE statement logic" },
                    generateDelete: { title: "Delete Operation", desc: "Include DELETE statement logic" },
                  };
                  return (
                    <label key={key} className="flex items-start gap-3 p-4 rounded-xl border border-border/40 bg-background hover:border-primary/40 hover:shadow-sm transition-all cursor-pointer group">
                      <Checkbox
                        className="mt-0.5 data-[state=checked]:bg-primary"
                        checked={(form.watch(key as any) as boolean) ?? true}
                        onCheckedChange={(v) => form.setValue(key as any, Boolean(v) as any)}
                      />
                      <div className="space-y-1">
                        <span className="text-sm font-bold block leading-none group-hover:text-primary transition-colors">{labels[key].title}</span>
                        <span className="text-xs text-muted-foreground block">{labels[key].desc}</span>
                      </div>
                    </label>
                  );
                })}
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-6">
            {/* SELECT Configuration */}
            <div className="bg-card border border-border/40 shadow-sm rounded-2xl p-6 grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-3">
                <Label className="text-sm font-bold text-foreground" htmlFor="orderBy">
                  Order By Clause
                </Label>
                <div className="relative">
                  <Input
                    id="orderBy"
                    className="bg-background h-10 pl-4 pr-4 transition-colors focus-visible:ring-primary/30"
                    placeholder="e.g. created_at DESC, email ASC"
                    value={(form.watch("orderBy") as string[]).join(", ")}
                    onChange={(e) => form.setValue("orderBy" as any, [e.target.value] as any)}
                  />
                  <span className="absolute right-3 top-2.5 text-[10px] text-muted-foreground uppercase font-semibold pointer-events-none">CSV</span>
                </div>
                <p className="text-xs text-muted-foreground">Separate multiple columns with commas.</p>
              </div>

              <div className="flex flex-col justify-center pt-2 md:pt-6">
                <label className="flex items-start gap-4 p-4 w-full rounded-xl border border-border/40 bg-muted/20 hover:bg-muted/40 hover:border-primary/40 transition-all cursor-pointer group">
                  <Checkbox
                    className="mt-0.5"
                    checked={(form.watch("includePagination") as boolean) ?? false}
                    onCheckedChange={(v) => form.setValue("includePagination" as any, Boolean(v) as any)}
                  />
                  <div className="space-y-1">
                    <span className="text-sm font-bold block leading-none group-hover:text-primary transition-colors">Include Pagination</span>
                    <span className="text-xs text-muted-foreground block">Appends OFFSET and FETCH NEXT logic to the query.</span>
                  </div>
                </label>
              </div>
            </div>

            {/* Filters */}
            <div className="bg-card border border-border/40 shadow-sm rounded-2xl p-6 space-y-5">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-border/40">
                <div className="space-y-1">
                  <Label className="text-base font-bold text-foreground">Query Filters</Label>
                  <p className="text-xs text-muted-foreground">Define WHERE clause parameters.</p>
                </div>
                <Button
                  type="button" size="sm" variant="secondary" className="h-9 px-4 rounded-lg bg-primary/10 text-primary hover:bg-primary/20 hover:text-primary"
                  onClick={() => append({ column: availableColumns[0] ?? "", operator: "=", optional: false })}
                >
                  <Plus className="h-4 w-4 mr-1.5" />
                  Add Filter
                </Button>
              </div>

              <div className="space-y-3 pt-2">
                {fields.length === 0 ? (
                  <div className="text-sm text-muted-foreground py-10 text-center border-2 border-dashed border-border/60 rounded-xl bg-muted/10 flex flex-col items-center justify-center gap-2">
                    <SlidersHorizontal className="h-8 w-8 text-muted-foreground/40" />
                    <p>No filters defined. Click "Add Filter" to create search parameters.</p>
                  </div>
                ) : fields.map((field, idx) => (
                  <div key={field.id} className="grid grid-cols-1 md:grid-cols-[1fr_180px_120px_auto] items-end gap-4 bg-muted/20 p-4 rounded-xl border border-border/40 hover:border-border/80 transition-colors">
                    <div className="space-y-2">
                      <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Column</Label>
                      <Select
                        value={(form.watch(`filters.${idx}.column`) as string) ?? ""}
                        onValueChange={(v) => form.setValue(`filters.${idx}.column` as any, v as any)}
                      >
                        <SelectTrigger className="rounded-lg bg-background h-10 border-border/60 shadow-sm"><SelectValue placeholder="Select column" /></SelectTrigger>
                        <SelectContent>
                          {availableColumns.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Operator</Label>
                      <Select
                        value={(form.watch(`filters.${idx}.operator`) as any) ?? "="}
                        onValueChange={(v) => form.setValue(`filters.${idx}.operator` as any, v as any)}
                      >
                        <SelectTrigger className="rounded-lg bg-background h-10 border-border/60 shadow-sm font-mono text-sm"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {["=", "LIKE", ">", "<", "BETWEEN", "IN"].map((op) => (
                            <SelectItem key={op} value={op} className="font-mono">{op}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="flex h-10 items-center pl-2">
                      <label className="flex items-center gap-2.5 cursor-pointer group">
                        <Checkbox
                          checked={(form.watch(`filters.${idx}.optional`) as boolean) ?? false}
                          onCheckedChange={(v) => form.setValue(`filters.${idx}.optional` as any, Boolean(v) as any)}
                        />
                        <span className="text-sm font-medium group-hover:text-primary transition-colors">Optional</span>
                      </label>
                    </div>
                    <div className="flex h-10 items-center justify-end">
                      <Button
                        type="button" size="icon" variant="ghost"
                        className="text-muted-foreground hover:text-destructive hover:bg-destructive/10 rounded-lg h-10 w-10 transition-colors"
                        onClick={() => remove(idx)}
                        aria-label={`Remove filter ${idx + 1}`}
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

        <div className="pt-6 flex justify-between items-center border-t border-border/40">
          <p className="text-sm text-muted-foreground hidden sm:block">
            Review your settings before generating the SQL script.
          </p>
          <Button type="submit" size="lg" className="rounded-xl px-8 h-12 gap-2 shadow-md w-full sm:w-auto transition-all hover:scale-[1.02] active:scale-[0.98]">
            Generate Procedure
            <ArrowRight className="w-4 h-4" />
          </Button>
        </div>
      </form>
    </div>
  );
}
