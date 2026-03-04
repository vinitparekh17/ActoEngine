import { useCallback, useMemo, useState } from "react";
import { useToast } from "../hooks/useToast";
import { useAuthorization } from "../hooks/useAuth";
import { ArrowLeft } from "lucide-react";

import CodeExportButton from "../components/spgen/CodeExportButton";
import type { SPType } from "@/components/spgen/SPTypeCard";

import { Skeleton, FormSkeleton } from "../components/ui/skeletons";
import { Button } from "@/components/ui/button";
import { cn } from "../lib/utils";
import { useProject, useProjectTables, useTableSchema } from "../hooks/useProject";
import { useApiMutation } from "../hooks/useApi";

// modular imports
import StepperHeader, { type Step } from "@/components/spgen/StepperHeader";
import StepSelectTable from "@/components/spgen/StepSelectTable";
import SPConfigPanel from "@/components/spgen/SPConfigPanel";
import SPPreviewPane from "@/components/spgen/SPPreviewPanel";
import { getDefaultSpConfig } from "@/components/spgen/spConfigDefaults";
import type { SPConfigValues, TreeNode } from "@/schema/spBuilderSchema";

export default function SpBuilder() {
  const { showToast: toast } = useToast();
  const canCreate = useAuthorization("StoredProcedures:Create");

  const [step, setStep] = useState<Step>(0);
  const [completedUpTo, setCompletedUpTo] = useState<Step>(0);
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const [spType, setSpType] = useState<SPType>("CUD");
  const [sqlCode, setSqlCode] = useState<string>("-- Generated SQL will appear here");
  const [isGenerating, setIsGenerating] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false); // Used in layout wrappers if any

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

  const [config, setConfig] = useState<SPConfigValues>(getDefaultSpConfig("CUD"));

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

  const schema = useMemo(() => {
    const colsSource = tableSchema?.schema?.columns ?? tableSchema?.columns ?? [];
    if (!tableSchema || !selectedTable || colsSource.length === 0) {
      return { tableName: selectedTable || "", schemaName: "", columns: [] };
    }
    return {
      tableName: tableSchema.tableName || selectedTable || "",
      schemaName: tableSchema.schemaName || "",
      columns: colsSource.map((col: any) => {
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
      // Persist user's selections into parent state so SPConfigPanel re-mounts
      // with the correct values if the user navigates back from the preview step.
      setConfig(values);

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
        columns: (tableSchema.schema?.columns || tableSchema.columns || []).map((col: any) => ({
          columnName: col.columnName,
          dataType: col.dataType,
          maxLength: col.maxLength,
          precision: col.precision,
          scale: col.scale,
          isNullable: col.isNullable,
          isPrimaryKey: col.isPrimaryKey,
          isIdentity: col.isIdentity,
          includeInCreate: values.mode === "CUD" ? (values.includeInCreate[col.columnName] ?? true) : true,
          includeInUpdate: values.mode === "CUD" ? (values.includeInUpdate[col.columnName] ?? true) : true,
          defaultValue: col.defaultValue || "",
        })),
        cudOptions: values.mode === "CUD" ? {
          spPrefix: values.spPrefix || "usp",
          includeErrorHandling: values.includeErrorHandling ?? true,
          includeTransaction: values.includeTransaction ?? true,
          actionParamName: values.actionParamName || "Action",
          generateCreate: values.generateCreate ?? true,
          generateUpdate: values.generateUpdate ?? true,
          generateDelete: values.generateDelete ?? true,
        } : undefined,
        selectOptions: values.mode === "SELECT" ? {
          spPrefix: "usp",
          filters: (values as any).filters?.map((f: any) => ({
            columnName: f.column,
            operator:
              f.operator === "="
                ? "Equals"
                : f.operator === "LIKE"
                  ? "Like"
                  : f.operator === ">"
                    ? "GreaterThan"
                    : f.operator === "<"
                      ? "LessThan"
                      : f.operator === "IN"
                        ? "In"
                        : "Between",
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
    setConfig(getDefaultSpConfig(t));
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
        <div className={cn("h-full overflow-y-auto custom-scrollbar", step !== 1 && "hidden")}>
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
                  schema={schema as any}
                  onSubmit={handleConfigSubmit}
                  onChangeType={onChangeType}
                />
              </div>
            )}
          </div>
        </div>

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
                Back to Config
              </Button>
              <div className="flex w-full sm:w-auto items-center gap-2">
                <CodeExportButton
                  onExport={handleExport}
                />
              </div>
            </div>

            <div className="flex-1 min-h-0 relative rounded-2xl overflow-hidden border border-border/40 shadow-xl">
              <SPPreviewPane
                sqlCode={sqlCode}
                onChange={setSqlCode}
                isLoading={isGenerating}
              />
            </div>
          </div>
        )}

      </main>
    </div>
  );
}
