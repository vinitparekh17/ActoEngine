import React, { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useSchemaDiff, useApplyDiff } from "@/hooks/useProject";
import { GitCompare, Loader2, Check, Save } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ResyncEntityItem } from "@/types/project";
import { Eye, EyeOff } from "lucide-react";

interface SchemaDiffPanelProps {
  projectId: number;
}

export function SchemaDiffPanel({ projectId }: SchemaDiffPanelProps) {
  const [server, setServer] = useState("");
  const [port, setPort] = useState("1433");
  const [username, setUsername] = useState("sa");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [comparedConnectionString, setComparedConnectionString] = useState<string | null>(null);

  const diffMutation = useSchemaDiff();
  const applyMutation = useApplyDiff();

  const connectionString = useMemo(
    () => `Server=${server},${port};User Id=${username};Password=${password};TrustServerCertificate=True;`,
    [server, port, username, password],
  );

  const clearComparedDiff = () => {
    setComparedConnectionString(null);
    if (diffMutation.data) {
      diffMutation.reset();
    }
  };

  const handleCompare = () => {
    diffMutation.mutate(
      { projectId, connectionString },
      {
        onSuccess: () => {
          setComparedConnectionString(connectionString);
        },
      },
    );
  };

  const handleApply = () => {
    if (!diffMutation.data || !comparedConnectionString) return;
    if (connectionString !== comparedConnectionString) return;

    // Map diff output to apply entities payload (Defaulting to applying EVERYTHING for this MVP Tier 2 UI)
    const addEntities: ResyncEntityItem[] = [];
    const removeEntities: ResyncEntityItem[] = [];
    const updateEntities: ResyncEntityItem[] = [];

    // Tables
    diffMutation.data.tables.added.forEach((t) => {
      addEntities.push({ entityType: "TABLE", schemaName: t.schemaName, entityName: t.entityName });
    });
    diffMutation.data.tables.removed.forEach((t) => {
      removeEntities.push({ entityType: "TABLE", schemaName: t.schemaName, entityName: t.entityName });
    });
    diffMutation.data.tables.modified.forEach((t) => {
      updateEntities.push({ entityType: "TABLE", schemaName: t.schemaName, entityName: t.entityName });
    });

    // SPs
    diffMutation.data.storedProcedures.added.forEach((t) => {
      addEntities.push({ entityType: "SP", schemaName: t.schemaName, entityName: t.entityName });
    });
    diffMutation.data.storedProcedures.removed.forEach((t) => {
      removeEntities.push({ entityType: "SP", schemaName: t.schemaName, entityName: t.entityName });
    });
    diffMutation.data.storedProcedures.modified.forEach((t) => {
      updateEntities.push({ entityType: "SP", schemaName: t.schemaName, entityName: t.entityName });
    });

    applyMutation.mutate(
      {
        projectId,
        connectionString,
        addEntities,
        removeEntities,
        updateEntities,
      },
      {
        onSuccess: () => {
          // Clear diff output after successful apply
          diffMutation.reset();
          setComparedConnectionString(null);
        },
      },
    );
  };

  const diffCount = diffMutation.data
    ? diffMutation.data.tables.added.length +
      diffMutation.data.tables.removed.length +
      diffMutation.data.tables.modified.length +
      diffMutation.data.storedProcedures.added.length +
      diffMutation.data.storedProcedures.removed.length +
      diffMutation.data.storedProcedures.modified.length
    : 0;

  const canApplyComparedDiff = Boolean(
    diffMutation.data && comparedConnectionString && comparedConnectionString === connectionString,
  );

  return (
    <Card className="border-blue-200 dark:border-blue-900 border-2">
      <CardHeader>
        <CardTitle>Schema Diff</CardTitle>
        <CardDescription>
          Compare current source database schema against the synced ActoEngine metadata.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {/* Connection Form */}
        <div className="grid gap-4 mb-6">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1">
              <Label htmlFor="diff-server" className="text-xs">Server</Label>
              <Input
                id="diff-server"
                value={server}
                onChange={(e) => {
                  setServer(e.target.value);
                  clearComparedDiff();
                }}
                placeholder="localhost"
                className="h-9"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="diff-port" className="text-xs">Port</Label>
              <Input
                id="diff-port"
                type="number"
                value={port}
                onChange={(e) => {
                  setPort(e.target.value);
                  clearComparedDiff();
                }}
                className="h-9"
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1">
              <Label htmlFor="diff-username" className="text-xs">Username</Label>
              <Input
                id="diff-username"
                value={username}
                onChange={(e) => {
                  setUsername(e.target.value);
                  clearComparedDiff();
                }}
                placeholder="sa"
                className="h-9"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="diff-password" className="text-xs">Password</Label>
              <div className="relative">
                <Input
                  id="diff-password"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value);
                    clearComparedDiff();
                  }}
                  placeholder="********"
                  className="pr-10 h-9"
                />
                <button type="button" onClick={() => setShowPassword(!showPassword)} className="absolute right-3 top-1/2 -translate-y-1/2">
                  {showPassword ? <EyeOff className="w-4 h-4 text-muted-foreground" /> : <Eye className="w-4 h-4 text-muted-foreground" />}
                </button>
              </div>
            </div>
          </div>
        </div>

        <Button
          variant="secondary"
          onClick={handleCompare}
          disabled={diffMutation.isPending || !server || !username || !password}
        >
          {diffMutation.isPending ? (
            <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Analyzing...</>
          ) : (
            <><GitCompare className="w-4 h-4 mr-2" /> Compare Schema</>
          )}
        </Button>

        {/* Diff Results */}
        {diffMutation.data && (
          <div className="mt-6 space-y-4">
            {diffCount === 0 ? (
              <div className="p-4 bg-green-50 text-green-700 dark:bg-green-950/20 dark:text-green-400 rounded-md border border-green-200 flex items-center gap-2">
                <Check className="w-5 h-5" />
                <span>You are up to date! No structural differences found.</span>
              </div>
            ) : (
              <div className="p-4 border rounded-md">
                <div className="flex justify-between items-center mb-4 pb-2 border-b">
                  <h3 className="font-semibold">Found {diffCount} difference(s)</h3>
                  <Button size="sm" onClick={handleApply} disabled={applyMutation.isPending || !canApplyComparedDiff}>
                    {applyMutation.isPending ? (
                      <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Applying...</>
                    ) : (
                      <><Save className="w-4 h-4 mr-2" /> Apply All Changes</>
                    )}
                  </Button>
                </div>

                <div className="grid grid-cols-2 gap-6 text-sm">
                  {/* TABLES */}
                  <div className="space-y-2 border-r pr-4">
                    <h4 className="font-semibold text-muted-foreground uppercase text-xs tracking-wider">Tables</h4>
                    {diffMutation.data.tables.added.map((t, idx) => (
                      <div key={'t-a-'+idx} className="flex gap-2 text-green-600 items-center"><span className="font-mono text-xs px-1 bg-green-100 dark:bg-green-900 rounded">+</span> {t.schemaName}.{t.entityName}</div>
                    ))}
                    {diffMutation.data.tables.removed.map((t, idx) => (
                       <div key={'t-r-'+idx} className="flex gap-2 text-red-600 items-center"><span className="font-mono text-xs px-1 bg-red-100 dark:bg-red-900 rounded">-</span> {t.schemaName}.{t.entityName}</div>
                    ))}
                    {diffMutation.data.tables.modified.map((t, idx) => (
                       <div key={'t-m-'+idx} className="flex gap-2 text-blue-600 items-center"><span className="font-mono text-xs px-1 bg-blue-100 dark:bg-blue-900 rounded">~</span> {t.schemaName}.{t.entityName}</div>
                    ))}
                    {diffMutation.data.tables.added.length === 0 && diffMutation.data.tables.removed.length === 0 && diffMutation.data.tables.modified.length === 0 && (
                      <div className="text-muted-foreground text-xs italic">No table differences</div>
                    )}
                  </div>
                  {/* STORED PROCEDURES */}
                  <div className="space-y-2">
                    <h4 className="font-semibold text-muted-foreground uppercase text-xs tracking-wider">Stored Procedures</h4>
                    {diffMutation.data.storedProcedures.added.map((t, idx) => (
                      <div key={'sp-a-'+idx} className="flex gap-2 text-green-600 items-center"><span className="font-mono text-xs px-1 bg-green-100 dark:bg-green-900 rounded">+</span> {t.schemaName}.{t.entityName}</div>
                    ))}
                    {diffMutation.data.storedProcedures.removed.map((t, idx) => (
                       <div key={'sp-r-'+idx} className="flex gap-2 text-red-600 items-center"><span className="font-mono text-xs px-1 bg-red-100 dark:bg-red-900 rounded">-</span> {t.schemaName}.{t.entityName}</div>
                    ))}
                    {diffMutation.data.storedProcedures.modified.map((t, idx) => (
                       <div key={'sp-m-'+idx} className="flex gap-2 text-blue-600 items-center"><span className="font-mono text-xs px-1 bg-blue-100 dark:bg-blue-900 rounded">~</span> {t.schemaName}.{t.entityName}</div>
                    ))}
                    {diffMutation.data.storedProcedures.added.length === 0 && diffMutation.data.storedProcedures.removed.length === 0 && diffMutation.data.storedProcedures.modified.length === 0 && (
                      <div className="text-muted-foreground text-xs italic">No procedure differences</div>
                    )}
                  </div>
                </div>
              </div>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
