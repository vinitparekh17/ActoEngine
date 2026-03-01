import { lazy, useCallback, useMemo, useState, useEffect } from "react";
import { useForm, useFieldArray } from "react-hook-form";
import { z } from "zod";
import { useToast } from "../hooks/useToast";
import { useAuthorization } from "../hooks/useAuth";
import {
  Maximize2, Minimize2, Trash2, Plus, Settings2,
  Database, Check, ChevronRight, ArrowLeft, ArrowRight,
  Table2, Code2, SlidersHorizontal,
} from "lucide-react";

import CodeExportButton from "../components/spgen/CodeExportButton";
import TreeView from "../components/database/TreeView";
import SPTypeCard, { type SPType } from "@/components/spgen/SPTypeCard";
import TableSchemaViewer, { type TableSchema } from "@/components/database/TableSchemaViewer";

import { Card } from "../components/ui/card";
import { Skeleton, FormSkeleton } from "../components/ui/skeletons";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { cn } from "../lib/utils";
import { useProject, useProjectTables, useTableSchema } from "../hooks/useProject";
import { useApiMutation } from "../hooks/useApi";

// --- Types & Schemas ---

type TreeNode = {
  id: string;
  name: string;
  children?: TreeNode[];
  type?:
  | "database" | "table" | "column" | "index"
  | "stored-procedure" | "scalar-function" | "table-function"
  | "tables-folder" | "programmability-folder"
  | "stored-procedures-folder" | "functions-folder";
};

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

// --- Step definitions ---

type Step = 0 | 1 | 2;

const STEPS = [
  { id: 0, label: "Select Table", icon: Table2, desc: "Choose database table" },
  { id: 1, label: "Configure", icon: SlidersHorizontal, desc: "Set procedure options" },
  { id: 2, label: "Preview", icon: Code2, desc: "Review & export SQL" },
] as const;

// --- Stepper Header ---

function StepperHeader({
  current,
  completedUpTo,
  onStepClick,
  selectedTable,
  spType,
}: {
  current: Step;
  completedUpTo: Step;
  onStepClick: (step: Step) => void;
  selectedTable: string | null;
  spType: SPType;
}) {
  return (
    <div className="shrink-0 border-b border-border/40 bg-card/95 backdrop-blur-md sticky top-0 z-20 shadow-sm">
      <div className="flex flex-col md:flex-row md:items-center justify-between max-w-7xl mx-auto px-4 sm:px-6">
        <div className="flex items-center gap-1 overflow-x-auto hide-scrollbar py-2">
          {STEPS.map((step, i) => {
            const Icon = step.icon;
            const isCompleted = completedUpTo > step.id;
            const isCurrent = current === step.id;
            const isClickable = step.id <= completedUpTo;

            return (
              <div key={step.id} className="flex items-center">
                <button
                  disabled={!isClickable}
                  onClick={() => isClickable && onStepClick(step.id as Step)}
                  className={cn(
                    "flex items-center gap-3 py-3 px-4 relative transition-all rounded-lg text-left",
                    "focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50",
                    isCurrent ? "bg-primary/5 text-foreground" : "hover:bg-muted/50",
                    !isCurrent && !isCompleted && "opacity-50 cursor-not-allowed",
                  )}
                >
                  {/* Step circle */}
                  <span
                    className={cn(
                      "flex items-center justify-center w-8 h-8 rounded-full text-xs font-bold shrink-0 transition-all duration-300",
                      isCurrent && "bg-primary text-primary-foreground shadow-md shadow-primary/20 scale-110",
                      isCompleted && !isCurrent && "bg-primary/15 text-primary",
                      !isCurrent && !isCompleted && "bg-muted text-muted-foreground border border-border/50",
                    )}
                  >
                    {isCompleted ? <Check className="w-4 h-4" /> : <Icon className="w-4 h-4" />}
                  </span>

                  <div className="hidden sm:block min-w-[120px]">
                    <p className={cn("text-sm font-semibold tracking-tight transition-colors", isCurrent ? "text-primary" : "text-foreground")}>
                      {step.label}
                    </p>
                    <p className="text-xs text-muted-foreground mt-0.5">{step.desc}</p>
                  </div>
                </button>

                {/* Connector */}
                {i < STEPS.length - 1 && (
                  <ChevronRight className={cn(
                    "w-4 h-4 shrink-0 mx-2 transition-colors",
                    completedUpTo > step.id ? "text-primary/50" : "text-border"
                  )} />
                )}
              </div>
            );
          })}
        </div>

        {/* Context pills */}
        <div className="flex items-center gap-3 py-3 md:py-0 border-t md:border-t-0 border-border/40">
          <div className="flex items-center gap-2">
            <span className="text-xs font-medium text-muted-foreground">Target:</span>
            {selectedTable ? (
              <Badge variant="outline" className="text-xs gap-1.5 font-mono bg-background border-border/60 py-1 px-2.5">
                <Table2 className="w-3.5 h-3.5 text-primary" />
                {selectedTable}
              </Badge>
            ) : (
              <Badge variant="outline" className="text-xs text-muted-foreground/50 border-dashed">
                None selected
              </Badge>
            )}
          </div>

          {current >= 1 && (
            <Badge
              variant="secondary"
              className={cn(
                "text-xs font-medium py-1 px-2.5",
                spType === "CUD"
                  ? "bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-500/20"
                  : "bg-violet-500/10 text-violet-600 dark:text-violet-400 border border-violet-500/20"
              )}
            >
              {spType === "CUD" ? "CUD Generator" : "SELECT Generator"}
            </Badge>
          )}
        </div>
      </div>
    </div>
  );
}

// --- Step 1: Table Selector ---

function StepSelectTable({
  treeData,
  selectedTable,
  onSelect,
  isLoading,
  onNext,
}: {
  treeData: TreeNode[];
  selectedTable: string | null;
  onSelect: (node: TreeNode) => void;
  isLoading: boolean;
  onNext: () => void;
}) {
  const [treeSearch, setTreeSearch] = useState("");

  return (
    <div className="flex flex-col h-[calc(100vh-200px)] max-w-3xl mx-auto w-full p-4 gap-3 animate-in fade-in slide-in-from-bottom-4 duration-500">

      <Card className="flex-1 min-h-0 h-[calc(100vh-200px)] flex flex-col overflow-hidden border-border/40 shadow-sm rounded-2xl bg-card">
        <div className="py-2 px-4 border-b border-border/40 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="p-1.5 bg-primary/10 rounded-md">
              <Database className="h-4 w-4 text-primary" />
            </div>
            <span className="text-sm font-semibold">Table Explorer</span>
          </div>
        </div>
        <div className="flex-1 overflow-auto p-3">
          <TreeView
            treeData={treeData}
            onSelectNode={onSelect}
            searchQuery={treeSearch}
            onSearchChange={setTreeSearch}
            isLoading={isLoading}
          />
        </div>
      </Card>

      <div className="flex justify-end shrink-0">
        <Button
          size="lg"
          onClick={onNext}
          disabled={!selectedTable}
          className="gap-2 rounded-xl px-8 h-12 shadow-sm transition-all hover:scale-[1.02] active:scale-[0.98]"
        >
          Continue to Configuration
          <ArrowRight className="w-4 h-4" />
        </Button>
      </div>
    </div>
  );
}

// --- Step 2: Configure ---

export function SPConfigPanel({
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
    if (values.mode === "SELECT") {
      const ob = (form.getValues("orderBy") || []) as string[];
      const normalized = ob.flatMap((v) => v.split(",")).map((s) => s.trim()).filter(Boolean);
      onSubmit({ ...values, orderBy: normalized });
    } else {
      onSubmit(values);
    }
  });

  const availableColumns = schema.columns.map((c) => c.name);

  return (
    <div className="flex flex-col gap-8 animate-in fade-in slide-in-from-bottom-4 duration-500">

      {/* Schema Viewer Section */}
      <div className="bg-card rounded-2xl border border-border/40 p-1 shadow-sm overflow-hidden">
        <TableSchemaViewer schema={schema} selectedTable={schema.tableName} />
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
                          {["=", "LIKE", ">", "<", "BETWEEN"].map((op) => (
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

// --- Step 3: Preview ---

const MonacoEditor = lazy(() => import("@monaco-editor/react"));

function SPPreviewPane({
  sqlCode,
  onChange,
  isLoading = false,
}: {
  sqlCode: string;
  onChange: (value: string) => void;
  isLoading?: boolean;
}) {
  return (
    <div className="border border-border/40 rounded-2xl overflow-hidden h-full bg-[#1e1e1e] shadow-inner">
      {isLoading ? (
        <div className="p-8 space-y-4">
          {Array.from({ length: 12 }).map((_, i) => (
            <Skeleton
              key={i}
              className="h-4 opacity-10 bg-primary"
              style={{ width: `${Math.random() * 40 + 40}%` }}
            />
          ))}
        </div>
      ) : (
        <MonacoEditor
          height="100%"
          defaultLanguage="sql"
          theme="vs-dark"
          value={sqlCode}
          onChange={(v) => onChange(v || "")}
          options={{
            minimap: { enabled: false },
            fontSize: 13,
            fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
            wordWrap: "on",
            scrollBeyondLastLine: false,
            padding: { top: 24, bottom: 24 },
            renderLineHighlight: "all",
            lineHeight: 1.6,
          }}
        />
      )}
    </div>
  );
}

// --- Main Page ---

export default function SpBuilder() {
  const { showToast: toast } = useToast();
  const canCreate = useAuthorization("StoredProcedures:Create");

  const [step, setStep] = useState<Step>(0);
  const [completedUpTo, setCompletedUpTo] = useState<Step>(0);
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const [spType, setSpType] = useState<SPType>("CUD");
  const [sqlCode, setSqlCode] = useState<string>("-- Generated SQL will appear here");
  const [isGenerating, setIsGenerating] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const { selectedProject } = useProject();
  const { tables, isLoading: isLoadingTables } = useProjectTables();
  const { schema: tableSchema, isLoading: isLoadingSchema, error: schemaError } = useTableSchema(selectedTable || undefined);

  const generateMutation = useApiMutation("/SpBuilder/generate", "POST", {
    onSuccess: (result: any) => {
      const generatedCode = result.storedProcedure?.code || "-- No code generated";
      setSqlCode(generatedCode);
      // Advance to preview
      setStep(2);
      setCompletedUpTo(2);
      toast({ title: "Success", description: `Generated ${spType} stored procedure` });
    },
    onError: (error) => {
      setSqlCode(`-- Error generating SQL: ${error.message}`);
      toast({ title: "Error", description: "Failed to generate stored procedure", type: "error" });
    },
  });

  const [config, setConfig] = useState<SPConfigValues>({
    mode: "CUD",
    generateCreate: true,
    generateUpdate: true,
    generateDelete: true,
    spPrefix: "usp",
    includeErrorHandling: true,
    includeTransaction: true,
    actionParamName: "Action",
  });

  const treeData = useMemo<TreeNode[]>(() => {
    if (!selectedProject || !tables.length) return [];
    return [{
      id: `db-${selectedProject.projectId}`,
      name: selectedProject.databaseName || selectedProject.projectName,
      type: "database",
      children: [{
        id: `db-${selectedProject.projectId}-tables`,
        name: "Tables",
        type: "tables-folder",
        children: tables.map((tableName, index) => ({
          id: `table-${index}`,
          name: tableName,
          type: "table" as const,
          children: [],
        })),
      }],
    }];
  }, [selectedProject, tables]);

  const schema = useMemo<TableSchema>(() => {
    // defensively handle cases where the API returns a schema object but columns
    // is missing or null. we previously only checked `tableSchema` which meant
    // `tableSchema.columns` could still be undefined and cause a crash when
    // calling `.map`.
    if (!tableSchema || !selectedTable || !tableSchema.columns) {
      return { tableName: selectedTable || "", schemaName: "", columns: [] };
    }
    return {
      tableName: tableSchema.tableName,
      schemaName: tableSchema.schemaName,
      columns: tableSchema.schema.columns.map((col) => {
        let dataType = col.dataType;
        if (col.maxLength && col.maxLength > 0 && col.maxLength !== -1) {
          dataType += `(${col.maxLength})`;
        } else if (col.precision && col.scale !== undefined) {
          dataType += `(${col.precision},${col.scale})`;
        } else if (col.precision && col.scale === 0) {
          dataType += `(${col.precision})`;
        }
        return {
          name: col.columnName,
          dataType,
          constraints: [
            col.isPrimaryKey ? "PK" : "",
            !col.isNullable ? "NOT NULL" : "",
            col.isIdentity ? "IDENTITY" : "",
            col.isForeignKey ? "FK" : "",
            col.defaultValue ? `DEFAULT ${col.defaultValue}` : "",
          ].filter(Boolean),
        };
      }),
    };
  }, [tableSchema, selectedTable]);

  const handleTreeSelect = useCallback((node: TreeNode) => {
    if (node.type === "table") setSelectedTable(node.name);
  }, []);

  const handleTableNext = useCallback(() => {
    if (!selectedTable) return;
    setStep(1);
    setCompletedUpTo((prev) => (prev < 1 ? 1 : prev) as Step);
  }, [selectedTable]);

  const handleConfigSubmit = useCallback(
    (values: SPConfigValues) => {
      if (!canCreate) {
        toast({ title: "Error", description: "You don't have permission to generate stored procedures", type: "error" });
        return;
      }
      if (!selectedProject || !selectedTable || !tableSchema) {
        toast({ title: "Error", description: "Please select a project and table first" });
        return;
      }
      setIsGenerating(true);

      let schemaName = "dbo";
      let actualTableName = selectedTable;
      if (selectedTable.includes(".")) {
        const parts = selectedTable.split(".");
        schemaName = parts[0];
        actualTableName = parts.slice(1).join(".");
      }

      const requestData = {
        projectId: selectedProject.projectId,
        tableName: actualTableName,
        schemaName: schemaName,
        type: values.mode === "CUD" ? "Cud" : "Select",
        columns: (tableSchema.schema?.columns || []).map((col: any) => ({
          columnName: col.columnName,
          dataType: col.dataType,
          maxLength: col.maxLength,
          isNullable: col.isNullable,
          isPrimaryKey: col.isPrimaryKey,
          isIdentity: col.isIdentity,
          includeInCreate: values.mode === "CUD" ? ((values as any).generateCreate ?? true) : true,
          includeInUpdate: values.mode === "CUD" ? ((values as any).generateUpdate ?? true) : true,
          defaultValue: col.defaultValue || "",
        })),
        cudOptions: values.mode === "CUD" ? {
          spPrefix: (values as any).spPrefix || "usp",
          includeErrorHandling: (values as any).includeErrorHandling ?? true,
          includeTransaction: (values as any).includeTransaction ?? true,
          actionParamName: (values as any).actionParamName || "Action",
        } : undefined,
        selectOptions: values.mode === "SELECT" ? {
          spPrefix: "usp",
          filters: (values as any).filters?.map((f: any) => ({
            columnName: f.column,
            operator: f.operator === "=" ? "Equals" : f.operator === "LIKE" ? "Like" : f.operator === ">" ? "GreaterThan" : f.operator === "<" ? "LessThan" : "Between",
            isOptional: f.optional,
          })) || [],
          orderByColumns: (values as any).orderBy || [],
          includePagination: (values as any).includePagination ?? true,
        } : undefined,
      };

      generateMutation.mutate(requestData as any, { onSettled: () => setIsGenerating(false) });
    },
    [selectedProject, selectedTable, tableSchema, toast, generateMutation, canCreate]
  );

  const handleExport = useCallback((format: "sql" | "copy" | "zip") => {
    toast({ title: "Export", description: `Requested ${format.toUpperCase()}` });
  }, [toast]);

  const onChangeType = useCallback((t: SPType) => {
    setSpType(t);
    setConfig(
      t === "CUD"
        ? { mode: "CUD", generateCreate: true, generateUpdate: true, generateDelete: true, spPrefix: "usp", includeErrorHandling: true, includeTransaction: true, actionParamName: "Action" }
        : { mode: "SELECT", includePagination: true, orderBy: [], filters: [] }
    );
  }, []);

  return (
    <div className="flex flex-col h-auto bg-background/50 overflow-hidden font-sans">

      {/* Stepper Header */}
      <StepperHeader
        current={step}
        completedUpTo={completedUpTo}
        onStepClick={setStep}
        selectedTable={selectedTable}
        spType={spType}
      />

      {/* Step Content */}
      <main className="flex-1 overflow-hidden">

        {/* Step 0 — Select Table */}
        {step === 0 && (
          <div className="h-full overflow-hidden">
            <StepSelectTable
              treeData={treeData}
              selectedTable={selectedTable}
              onSelect={handleTreeSelect}
              isLoading={isLoadingTables}
              onNext={handleTableNext}
            />
          </div>
        )}

        {/* Step 1 — Configure */}
        {step === 1 && (
          <div className="h-full overflow-y-auto custom-scrollbar">
            <div className="max-w-6xl mx-auto w-full py-8 px-4 sm:px-6">
              <div className="mb-8 animate-in fade-in slide-in-from-left-4 duration-300">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setStep(0)}
                  className="mb-4 -ml-3 text-muted-foreground hover:text-foreground gap-2 h-8 px-3 rounded-lg"
                >
                  <ArrowLeft className="w-4 h-4" />
                  Back to selection
                </Button>
                <h2 className="text-2xl font-bold tracking-tight text-foreground">Configure Procedure</h2>
                <p className="text-sm text-muted-foreground mt-1">
                  Adjust options for your stored procedure generation. Your schema is loaded below.
                </p>
              </div>

              {isLoadingSchema ? (
                <div className="space-y-8 py-8 max-w-5xl">
                  <Skeleton className="h-10 w-1/4 rounded-xl" />
                  <FormSkeleton fields={4} />
                </div>
              ) : schemaError ? (
                <div className="flex flex-col items-center justify-center py-24 text-destructive space-y-3 bg-destructive/5 rounded-2xl border border-destructive/20">
                  <span className="text-4xl">⚠️</span>
                  <span className="font-semibold text-lg">Failed to load schema</span>
                  <span className="text-sm text-destructive/80 text-center max-w-md">{schemaError.message}</span>
                </div>
              ) : (
                <div className="max-w-5xl">
                  <SPConfigPanel
                    spType={spType}
                    config={config}
                    schema={schema}
                    onSubmit={handleConfigSubmit}
                    onChangeType={onChangeType}
                  />
                </div>
              )}
            </div>
          </div>
        )}

        {/* Step 2 — Preview */}
        {step === 2 && (
          <div className="h-[calc(100vh-12rem)] flex flex-col p-4 sm:p-6 max-w-[1920px] mx-auto w-full animate-in fade-in zoom-in-95 duration-300">
            {/* Toolbar */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-4 shrink-0 bg-card p-3 rounded-xl border border-border/40 shadow-sm">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setStep(1)}
                className="text-muted-foreground hover:text-foreground gap-2 h-9 self-start sm:self-auto"
              >
                <ArrowLeft className="w-4 h-4" />
                Back to Configuration
              </Button>
              <div className="flex items-center gap-3 self-end sm:self-auto">
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-2 text-sm h-9 px-4 border-border/60 hover:bg-muted"
                  onClick={() => setIsFullscreen(true)}
                >
                  <Maximize2 className="w-4 h-4" />
                  <span className="hidden sm:inline">Fullscreen</span>
                </Button>
                <CodeExportButton onExport={handleExport} />
              </div>
            </div>

            {/* Editor */}
            <div className="flex-1 min-h-0 rounded-2xl ring-1 ring-border/50 shadow-sm">
              <SPPreviewPane sqlCode={sqlCode} onChange={setSqlCode} isLoading={isGenerating} />
            </div>
          </div>
        )}
      </main>

      {/* Fullscreen Modal */}
      {isFullscreen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-8 bg-background/95 backdrop-blur-md animate-in fade-in duration-200">
          <Card className="w-full h-full max-w-[1920px] rounded-3xl flex flex-col shadow-2xl border-border/40 overflow-hidden ring-1 ring-white/10">
            <div className="px-6 py-4 border-b border-border/40 bg-card/50 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <Code2 className="w-5 h-5 text-primary" />
                <h2 className="text-base font-semibold tracking-tight">SQL Preview</h2>
                <Badge variant="secondary" className="bg-primary/10 text-primary hover:bg-primary/10 ml-2">Fullscreen Mode</Badge>
              </div>
              <div className="flex items-center gap-3">
                <CodeExportButton onExport={handleExport} />
                <Button variant="ghost" size="icon" className="h-9 w-9 hover:bg-destructive/10 hover:text-destructive transition-colors rounded-full" onClick={() => setIsFullscreen(false)} title="Exit Fullscreen">
                  <Minimize2 className="w-4 h-4" />
                </Button>
              </div>
            </div>
            <div className="p-4 sm:p-6 flex-1 bg-muted/20">
              <SPPreviewPane sqlCode={sqlCode} onChange={setSqlCode} isLoading={isGenerating} />
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}