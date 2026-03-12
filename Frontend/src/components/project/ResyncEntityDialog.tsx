import React, { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { useProject, useResyncEntities } from "@/hooks/useProject";
import type { ResyncEntityItem } from "@/types/project";
import { useAuthorization } from "@/hooks/useAuth";
import {
  ChevronDown,
  Database,
  Eye,
  EyeOff,
  Loader2,
  Settings,
} from "lucide-react";

interface ResyncEntityDialogProps {
  projectId: number;
  entityType?: "TABLE" | "SP";
  schemaName?: string;
  entityName?: string;
  entities?: ResyncEntityItem[];
  trigger?: React.ReactNode;
  onSuccess?: () => void;
}

export function ResyncEntityDialog({
  projectId,
  entityType,
  schemaName,
  entityName,
  entities,
  trigger,
  onSuccess,
}: ResyncEntityDialogProps) {
  const { selectedProject } = useProject();
  const canSyncSchema = useAuthorization("Schema:Sync");

  const [open, setOpen] = useState(false);
  const [server, setServer] = useState("");
  const [port, setPort] = useState("1433");
  const [username, setUsername] = useState("sa");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);

  const [encrypt, setEncrypt] = useState(true);
  const [trustServerCertificate, setTrustServerCertificate] = useState(false);
  const [connectionTimeout, setConnectionTimeout] = useState("30");
  const [applicationName, setApplicationName] = useState("");
  const [showAdvancedOptions, setShowAdvancedOptions] = useState(false);

  const resyncMutation = useResyncEntities();

  const resolvedEntities = useMemo<ResyncEntityItem[]>(() => {
    if (entities && entities.length > 0) return entities;

    if (entityType && schemaName && entityName) {
      return [
        {
          entityType,
          schemaName,
          entityName,
        },
      ];
    }

    return [];
  }, [entities, entityName, entityType, schemaName]);

  const isSingleEntity = resolvedEntities.length === 1;
  const singleEntityLabel = isSingleEntity
    ? `${resolvedEntities[0].schemaName}.${resolvedEntities[0].entityName}`
    : "";

  const hasValidConnectionDetails = Boolean(server && username && password);

  const buildConnectionPayload = () => {
    const timeout = Number.parseInt(connectionTimeout, 10);
    const normalizedTimeout =
      Number.isFinite(timeout) && timeout >= 5 && timeout <= 120 ? timeout : 30;

    return {
      server,
      port: Number.parseInt(port, 10) || 1433,
      databaseName: selectedProject?.databaseName,
      username,
      password,
      encrypt,
      trustServerCertificate,
      connectionTimeout: normalizedTimeout,
      applicationName: applicationName.trim() || undefined,
    };
  };

  const buildLegacyConnectionString = (connection: ReturnType<typeof buildConnectionPayload>) => {
    const parts = [
      `Server=${connection.server},${connection.port}`,
      ...(connection.databaseName ? [`Database=${connection.databaseName}`] : []),
      `User Id=${connection.username}`,
      `Password=${connection.password}`,
      `Encrypt=${connection.encrypt}`,
      `TrustServerCertificate=${connection.trustServerCertificate}`,
      `Connection Timeout=${connection.connectionTimeout}`,
    ];

    if (connection.applicationName) {
      parts.push(`Application Name=${connection.applicationName}`);
    }

    return `${parts.join(";")};`;
  };

  const handleResync = () => {
    if (!resolvedEntities.length || !canSyncSchema) return;
    const connection = buildConnectionPayload();

    resyncMutation.mutate(
      {
        projectId,
        connectionString: buildLegacyConnectionString(connection),
        entities: resolvedEntities,
      },
      {
        onSuccess: () => {
          setOpen(false);
          onSuccess?.();
        },
      },
    );
  };

  const defaultTrigger = (
    <Button size="sm" variant="outline" className="h-8">
      <Database className="h-3.5 w-3.5 mr-2" />
      {isSingleEntity ? "Resync Entity" : "Resync Entities"}
    </Button>
  );

  if (!canSyncSchema) {
    return null;
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger || defaultTrigger}</DialogTrigger>
      <DialogContent className="sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle>
            {isSingleEntity ? "Resync Entity" : "Resync Selected Entities"}
          </DialogTitle>
          <DialogDescription>
            Enter database credentials to sync{" "}
            {isSingleEntity ? (
              <span className="font-mono text-foreground">{singleEntityLabel}</span>
            ) : (
              <span className="font-medium text-foreground">
                {resolvedEntities.length} entities
              </span>
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          {selectedProject?.databaseName ? (
            <div className="rounded-md border bg-muted/20 px-3 py-2">
              <p className="text-xs text-muted-foreground">
                Database:{" "}
                <span className="font-medium text-foreground">
                  {selectedProject.databaseName}
                </span>
              </p>
            </div>
          ) : null}

          {!isSingleEntity && resolvedEntities.length > 0 ? (
            <div className="max-h-28 overflow-auto rounded-md border p-2">
              {resolvedEntities.slice(0, 8).map((entity) => (
                <div
                  key={`${entity.entityType}:${entity.schemaName}.${entity.entityName}`}
                  className="text-xs font-mono text-muted-foreground"
                >
                  {entity.schemaName}.{entity.entityName}
                </div>
              ))}
              {resolvedEntities.length > 8 ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  + {resolvedEntities.length - 8} more
                </p>
              ) : null}
            </div>
          ) : null}

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="server" className="text-right text-xs">
              Server
            </Label>
            <Input
              id="server"
              value={server}
              onChange={(e) => setServer(e.target.value)}
              className="col-span-3 text-sm"
              placeholder="localhost"
            />
          </div>

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="port" className="text-right text-xs">
              Port
            </Label>
            <Input
              id="port"
              type="number"
              value={port}
              onChange={(e) => setPort(e.target.value)}
              className="col-span-3 text-sm"
            />
          </div>

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="username" className="text-right text-xs">
              Username
            </Label>
            <Input
              id="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className="col-span-3 text-sm"
            />
          </div>

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="password" className="text-right text-xs">
              Password
            </Label>
            <div className="col-span-3 relative">
              <Input
                id="password"
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full pr-10 text-sm"
                placeholder="********"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
              >
                {showPassword ? (
                  <EyeOff className="h-4 w-4" />
                ) : (
                  <Eye className="h-4 w-4" />
                )}
              </button>
            </div>
          </div>

          <div className="border border-border rounded-md overflow-hidden">
            <button
              type="button"
              onClick={() => setShowAdvancedOptions((prev) => !prev)}
              className="w-full flex items-center justify-between p-3 text-sm text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            >
              <span className="flex items-center gap-2">
                <Settings className="w-4 h-4" />
                Advanced Options
              </span>
              <ChevronDown
                className={`w-4 h-4 transition-transform ${showAdvancedOptions ? "rotate-180" : ""}`}
              />
            </button>

            {showAdvancedOptions ? (
              <div className="p-3 pt-0 space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="flex items-center gap-2">
                    <Checkbox
                      id="resync-encrypt"
                      checked={encrypt}
                      onCheckedChange={(checked) => setEncrypt(checked === true)}
                    />
                    <Label htmlFor="resync-encrypt" className="text-xs">
                      Encrypt Connection
                    </Label>
                  </div>

                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <Checkbox
                        id="resync-trust-cert"
                        checked={trustServerCertificate}
                        onCheckedChange={(checked) =>
                          setTrustServerCertificate(checked === true)
                        }
                      />
                      <Label htmlFor="resync-trust-cert" className="text-xs">
                        Trust Server Certificate
                      </Label>
                    </div>
                    <p className="text-[11px] text-muted-foreground ml-6">
                      Use only for dev or self-signed certs.
                    </p>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label
                      htmlFor="connection-timeout"
                      className="text-xs block mb-1"
                    >
                      Connection Timeout (sec)
                    </Label>
                    <Input
                      id="connection-timeout"
                      type="number"
                      min={5}
                      max={120}
                      value={connectionTimeout}
                      onChange={(e) => setConnectionTimeout(e.target.value)}
                      className="text-sm"
                    />
                  </div>

                  <div>
                    <Label htmlFor="application-name" className="text-xs block mb-1">
                      Application Name
                    </Label>
                    <Input
                      id="application-name"
                      value={applicationName}
                      onChange={(e) => setApplicationName(e.target.value)}
                      className="text-sm"
                      placeholder="ActoEngine"
                    />
                  </div>
                </div>
              </div>
            ) : null}
          </div>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => setOpen(false)}
            disabled={resyncMutation.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleResync}
            disabled={
              resyncMutation.isPending ||
              !hasValidConnectionDetails ||
              resolvedEntities.length === 0
            }
          >
            {resyncMutation.isPending ? (
              <>
                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                Resyncing...
              </>
            ) : (
              `Start Resync${isSingleEntity ? "" : ` (${resolvedEntities.length})`}`
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
