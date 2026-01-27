import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Database,
  CheckCircle,
  AlertCircle,
  Loader2,
  ArrowRight,
  ArrowLeft,
  Sparkles,
  Info,
  ExternalLink,
  ChevronDown,
  Settings,
} from "lucide-react";
import { useApiPost } from "../hooks/useApi";
import { Input } from "../components/ui/input";
import { Button } from "../components/ui/button";
import { Label } from "../components/ui/label";
import { Textarea } from "../components/ui/textarea";
import { useForm, type Resolver } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import type {
  VerifyConnectionRequest,
  ConnectionResponse,
  CreateProjectRequest,
  LinkProjectRequest,
  ProjectResponse,
} from "../types/project";
import { ScrollArea } from "../components/ui/scroll-area";

const projectSetupSchema = z.object({
  server: z.string().min(1, "Server address is required"),
  databaseName: z.string().min(1, "Database name is required"),
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
  port: z.coerce.number().min(1, "Port must be greater than 0").max(65535, "Port must be less than 65536"),
  databaseType: z.string(),
  projectName: z.string().min(1, "Project name is required"),
  description: z.string().optional(),
  // Advanced connection options
  encrypt: z.boolean(),
  trustServerCertificate: z.boolean(),
  connectionTimeout: z.coerce.number().min(5).max(120),
  applicationName: z.string().optional(),
});

type ProjectSetupValues = z.infer<typeof projectSetupSchema>;

type Step = "connection" | "details";

export default function ProjectSetup() {
  const navigate = useNavigate();
  const [step, setStep] = useState<Step>("connection");
  const [verificationResult, setVerificationResult] = useState<{
    success: boolean;
    message: string;
    errorCode?: string;
    helpLink?: string;
  } | null>(null);
  const [createdProjectId, setCreatedProjectId] = useState<number | null>(null);
  const [showConnectionStringPaste, setShowConnectionStringPaste] = useState(false);
  const [connectionStringInput, setConnectionStringInput] = useState("");
  const [parseError, setParseError] = useState<string | null>(null);
  const [showAdvancedOptions, setShowAdvancedOptions] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
    getValues,
    trigger,
    setValue,
  } = useForm<ProjectSetupValues>({
    resolver: zodResolver(projectSetupSchema) as Resolver<ProjectSetupValues>,
    defaultValues: {
      server: "",
      databaseName: "",
      username: "",
      password: "",
      port: 1433,
      databaseType: "SqlServer",
      projectName: "",
      description: "",
      // Advanced connection options
      encrypt: true,
      trustServerCertificate: false,
      connectionTimeout: 30,
      applicationName: "",
    },
  });

  const buildConnectionString = (data: ProjectSetupValues) => {
    return `Server=${data.server},${data.port};Database=${data.databaseName};User Id=${data.username};Password=${data.password};TrustServerCertificate=True;`;
  };

  /**
   * Parses a SQL Server connection string and populates form fields.
   * Supports common formats: Server=x;Database=y;User Id=z;Password=p;
   */
  const parseConnectionString = (connStr: string) => {
    setParseError(null);

    if (!connStr.trim()) {
      setParseError("Connection string is empty");
      return;
    }

    const pairs = connStr.split(";").filter(p => p.trim());
    const parsed: Record<string, string> = {};

    for (const pair of pairs) {
      const [key, ...valueParts] = pair.split("=");
      if (key && valueParts.length > 0) {
        const normalizedKey = key.trim().toLowerCase();
        const value = valueParts.join("=").trim(); // Handle passwords with = in them
        parsed[normalizedKey] = value;
      }
    }

    // Extract server and port (format: "server,port" or just "server")
    const serverValue = parsed["server"] || parsed["data source"] || "";
    let server = serverValue;
    let port = 1433;

    if (serverValue.includes(",")) {
      const [serverPart, portPart] = serverValue.split(",");
      server = serverPart.trim();
      const parsedPort = parseInt(portPart.trim(), 10);
      if (!isNaN(parsedPort)) {
        port = parsedPort;
      }
    }

    const database = parsed["database"] || parsed["initial catalog"] || "";
    const username = parsed["user id"] || parsed["uid"] || parsed["user"] || "";
    const password = parsed["password"] || parsed["pwd"] || "";

    if (!server && !database) {
      setParseError("Could not parse connection string. Check the format.");
      return;
    }

    // Populate basic form fields
    if (server) setValue("server", server);
    if (database) setValue("databaseName", database);
    if (username) setValue("username", username);
    if (password) setValue("password", password);
    setValue("port", port);

    // Parse advanced connection options
    const encryptValue = parsed["encrypt"];
    if (encryptValue) {
      setValue("encrypt", encryptValue.toLowerCase() === "true");
    }

    const trustCertValue = parsed["trustservercertificate"] || parsed["trust server certificate"];
    if (trustCertValue) {
      setValue("trustServerCertificate", trustCertValue.toLowerCase() === "true");
    }

    const timeoutValue = parsed["connection timeout"] || parsed["connect timeout"];
    if (timeoutValue) {
      const timeout = parseInt(timeoutValue, 10);
      if (!isNaN(timeout) && timeout >= 5 && timeout <= 120) {
        setValue("connectionTimeout", timeout);
      }
    }

    const appName = parsed["application name"] || parsed["app"];
    if (appName) {
      setValue("applicationName", appName);
    }

    // Clear the input and collapse
    setConnectionStringInput("");
    setShowConnectionStringPaste(false);
    setVerificationResult(null);
  };

  const verifyMutation = useApiPost<
    ConnectionResponse,
    VerifyConnectionRequest
  >("/projects/verify", {
    showSuccessToast: false,
    showErrorToast: false,
    onSuccess: (data) => {
      if (data.isValid) {
        setVerificationResult({
          success: true,
          message: "Connection successful!",
        });
        setStep("details");
      } else {
        setVerificationResult({
          success: false,
          message: data.message || "Connection failed",
          errorCode: data.errorCode,
          helpLink: data.helpLink,
        });
      }
    },
    onError: (error) => {
      setVerificationResult({
        success: false,
        message: error.message || "Connection failed",
      });
    },
  });

  const createMutation = useApiPost<ProjectResponse, CreateProjectRequest>(
    "/projects",
    {
      showSuccessToast: false, // Don't show toast yet, wait for link to complete
      invalidateKeys: [["projects"]],
      onSuccess: (data) => {
        // Store the created project ID and trigger link mutation
        setCreatedProjectId(data.projectId);
        const connectionString = buildConnectionString(getValues());
        linkMutation.mutate({
          projectId: data.projectId,
          connectionString: connectionString,
        });
      },
    },
  );

  const linkMutation = useApiPost<ProjectResponse, LinkProjectRequest>(
    "/projects/link",
    {
      successMessage: "Project created and linked successfully!",
      invalidateKeys: [["projects"]],
      onSuccess: (data) => {
        navigate(`/project/${data.projectId}`);
      },
    },
  );

  const handleVerifyConnection = async () => {
    const isValid = await trigger([
      "server",
      "databaseName",
      "username",
      "password",
      "port",
    ]);
    if (!isValid) return;

    setVerificationResult(null);
    const values = getValues();
    verifyMutation.mutate({
      server: values.server,
      databaseName: values.databaseName,
      username: values.username,
      password: values.password,
      port: values.port,
      databaseType: values.databaseType,
      // Advanced options
      encrypt: values.encrypt,
      trustServerCertificate: values.trustServerCertificate,
      connectionTimeout: values.connectionTimeout,
      applicationName: values.applicationName || undefined,
    });
  };

  const handleCreateProject = (data: ProjectSetupValues) => {
    // Create project first (without connection string)
    // On success, it will automatically trigger link mutation with connection string
    createMutation.mutate({
      projectName: data.projectName,
      description: data.description || "",
      databaseName: data.databaseName,
      databaseType: data.databaseType,
    });
  };

  const isCreating = createMutation.isPending || linkMutation.isPending;

  return (
    <div className="h-[calc(100vh-110px)] bg-background flex items-center justify-center p-4 overflow-hidden">
      <div className="w-full max-w-2xl flex flex-col h-full max-h-[calc(100vh-2rem)]">
        {/* Header */}
        <div className="text-center mb-6 flex-shrink-0">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-primary/10 mb-3">
            <Sparkles className="w-6 h-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-foreground mb-1">
            Create New Project
          </h1>
          <p className="text-muted-foreground">
            Set up your project and link your database in one seamless flow
          </p>
        </div>

        {/* Progress Indicator */}
        <div className="mb-6 flex-shrink-0">
          <div className="flex items-center justify-between max-w-md mx-auto">
            <div className="flex flex-col items-center">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-semibold text-sm transition-all ${step === "connection"
                  ? "bg-primary text-primary-foreground shadow-md scale-110"
                  : "bg-primary/20 text-primary"
                  }`}
              >
                1
              </div>
              <p
                className={`mt-1.5 text-xs font-medium transition-colors ${step === "connection" ? "text-primary" : "text-primary/60"
                  }`}
              >
                Connection
              </p>
            </div>

            <div
              className={`flex-1 h-0.5 mx-4 rounded-full transition-all ${step === "details" ? "bg-primary" : "bg-border"
                }`}
            />

            <div className="flex flex-col items-center">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-semibold text-sm transition-all ${step === "details"
                  ? "bg-primary text-primary-foreground shadow-md scale-110"
                  : "bg-muted text-muted-foreground"
                  }`}
              >
                2
              </div>
              <p
                className={`mt-1.5 text-xs font-medium transition-colors ${step === "details" ? "text-primary" : "text-muted-foreground"
                  }`}
              >
                Details
              </p>
            </div>
          </div>
        </div>

        {/* Main Card */}
        <ScrollArea className="bg-card border border-border rounded-lg shadow-sm flex-1 min-h-0">
          <div className="p-6">
            {/* Step 1: Connection Details */}
            {step === "connection" && (
              <div className="space-y-4">
                {/* SQL Server Compatibility Banner */}
                <div className="flex items-start gap-3 p-3 rounded-md bg-primary/5 border border-primary/20">
                  <Info className="w-5 h-5 text-primary mt-0.5 flex-shrink-0" />
                  <div className="text-sm">
                    <p className="font-medium text-foreground">Supported: SQL Server 2012 and later</p>
                    <p className="text-muted-foreground text-xs mt-0.5">
                      SQL Server 2012 requires SP4 with cumulative updates for TLS 1.2 support.
                    </p>
                  </div>
                </div>

                {/* Connection String Paste Section */}
                <div className="border border-border rounded-md overflow-hidden">
                  <button
                    type="button"
                    onClick={() => setShowConnectionStringPaste(!showConnectionStringPaste)}
                    className="w-full flex items-center justify-between p-3 text-sm text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
                  >
                    <span>Or paste a connection string</span>
                    <ChevronDown
                      className={`w-4 h-4 transition-transform ${showConnectionStringPaste ? "rotate-180" : ""}`}
                    />
                  </button>
                  {showConnectionStringPaste && (
                    <div className="p-3 pt-0 space-y-3">
                      <Textarea
                        id="connectionStringPaste"
                        value={connectionStringInput}
                        onChange={(e) => setConnectionStringInput(e.target.value)}
                        placeholder="Server=myserver,1433;Database=mydb;User Id=sa;Password=...;"
                        className="text-sm font-mono min-h-[80px]"
                      />
                      {parseError && (
                        <p className="text-sm text-destructive">{parseError}</p>
                      )}
                      <Button
                        type="button"
                        variant="secondary"
                        size="sm"
                        onClick={() => parseConnectionString(connectionStringInput)}
                        disabled={!connectionStringInput.trim()}
                      >
                        Parse & Fill Form
                      </Button>
                    </div>
                  )}
                </div>

                <div>
                  <Label
                    htmlFor="server"
                    className="text-foreground font-semibold mb-1.5 block"
                  >
                    Server Address
                  </Label>
                  <Input
                    id="server"
                    {...register("server")}
                    placeholder="localhost or 192.168.1.100"
                  />
                  {errors.server && (
                    <p className="mt-1.5 text-sm text-destructive">
                      {errors.server.message}
                    </p>
                  )}
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div className="col-span-2">
                    <Label
                      htmlFor="databaseName"
                      className="text-foreground font-semibold mb-1.5 block"
                    >
                      Database Name
                    </Label>
                    <Input
                      id="databaseName"
                      {...register("databaseName")}
                      placeholder="MyDatabase"
                    />
                    {errors.databaseName && (
                      <p className="mt-1.5 text-sm text-destructive">
                        {errors.databaseName.message}
                      </p>
                    )}
                  </div>
                  <div>
                    <Label
                      htmlFor="port"
                      className="text-foreground font-semibold mb-1.5 block"
                    >
                      Port
                    </Label>
                    <Input
                      id="port"
                      type="number"
                      {...register("port")}
                    />
                    {errors.port && (
                      <p className="mt-1.5 text-sm text-destructive">
                        {errors.port.message}
                      </p>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <Label
                      htmlFor="username"
                      className="text-foreground font-semibold mb-1.5 block"
                    >
                      Username
                    </Label>
                    <Input
                      id="username"
                      {...register("username")}
                      placeholder="sa"
                    />
                    {errors.username && (
                      <p className="mt-1.5 text-sm text-destructive">
                        {errors.username.message}
                      </p>
                    )}
                  </div>

                  <div>
                    <Label
                      htmlFor="password"
                      className="text-foreground font-semibold mb-1.5 block"
                    >
                      Password
                    </Label>
                    <Input
                      id="password"
                      type="password"
                      {...register("password")}
                      placeholder="••••••••"
                    />
                    {errors.password && (
                      <p className="mt-1.5 text-sm text-destructive">
                        {errors.password.message}
                      </p>
                    )}
                  </div>
                </div>

                {/* Advanced Options Section */}
                <div className="border border-border rounded-md overflow-hidden">
                  <button
                    type="button"
                    onClick={() => setShowAdvancedOptions(!showAdvancedOptions)}
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
                  {showAdvancedOptions && (
                    <div className="p-3 pt-0 space-y-4">
                      {/* Encrypt & TrustServerCertificate row */}
                      <div className="grid grid-cols-2 gap-4">
                        <div className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            id="encrypt"
                            {...register("encrypt")}
                            className="h-4 w-4 rounded border-border text-primary focus:ring-primary"
                          />
                          <Label htmlFor="encrypt" className="text-sm font-normal cursor-pointer">
                            Encrypt Connection
                          </Label>
                        </div>
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              id="trustServerCertificate"
                              {...register("trustServerCertificate")}
                              className="h-4 w-4 rounded border-border text-primary focus:ring-primary"
                            />
                            <Label htmlFor="trustServerCertificate" className="text-sm font-normal cursor-pointer">
                              Trust Server Certificate
                            </Label>
                          </div>
                          <p className="text-xs text-muted-foreground ml-6">
                            ⚠️ Only for dev/self-signed certs
                          </p>
                        </div>
                      </div>

                      {/* Timeout & App Name row */}
                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <Label htmlFor="connectionTimeout" className="text-sm font-medium mb-1.5 block">
                            Connection Timeout (sec)
                          </Label>
                          <Input
                            id="connectionTimeout"
                            type="number"
                            min={5}
                            max={120}
                            {...register("connectionTimeout")}
                          />
                        </div>
                        <div>
                          <Label htmlFor="applicationName" className="text-sm font-medium mb-1.5 block">
                            Application Name
                          </Label>
                          <Input
                            id="applicationName"
                            {...register("applicationName")}
                            placeholder="ActoEngine"
                          />
                        </div>
                      </div>
                    </div>
                  )}
                </div>

                {verificationResult && (
                  <div
                    className={`flex items-start gap-3 p-3 rounded-md border transition-all ${verificationResult.success
                      ? "bg-primary/5 border-primary/20"
                      : "bg-destructive/5 border-destructive/20"
                      }`}
                  >
                    {verificationResult.success ? (
                      <CheckCircle className="w-5 h-5 text-primary mt-0.5 flex-shrink-0" />
                    ) : (
                      <AlertCircle className="w-5 h-5 text-destructive mt-0.5 flex-shrink-0" />
                    )}
                    <div className="flex-1">
                      <p
                        className={`text-sm ${verificationResult.success
                          ? "text-primary"
                          : "text-destructive"
                          }`}
                      >
                        {verificationResult.message}
                      </p>
                      {verificationResult.helpLink && (
                        <a
                          href={verificationResult.helpLink}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center gap-1 text-xs text-primary hover:underline mt-1.5"
                        >
                          Learn how to fix this
                          <ExternalLink className="w-3 h-3" />
                        </a>
                      )}
                    </div>
                  </div>
                )}

                <div className="flex gap-3 pt-4">
                  <Button
                    type="button"
                    onClick={handleVerifyConnection}
                    disabled={verifyMutation.isPending}
                    className="flex-1 flex items-center justify-center"
                  >
                    {verifyMutation.isPending ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Verifying...
                      </>
                    ) : (
                      <>
                        Test Connection
                        <ArrowRight className="w-4 h-4 ml-2" />
                      </>
                    )}
                  </Button>
                </div>

                <div className="pt-4 border-t border-border">
                  <div className="flex items-start gap-2 text-sm text-muted-foreground">
                    <div className="w-1.5 h-1.5 rounded-full bg-muted-foreground mt-1.5 flex-shrink-0" />
                    <p>
                      Your project will be created and automatically linked to
                      your database. Connection string is used temporarily and
                      not stored.
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Step 2: Project Details */}
            {step === "details" && (
              <form
                onSubmit={handleSubmit((data) => handleCreateProject(data))}
                className="space-y-4"
              >
                <div>
                  <Label
                    htmlFor="projectName"
                    className="text-foreground font-semibold mb-1.5 block"
                  >
                    Project Name
                  </Label>
                  <Input
                    id="projectName"
                    {...register("projectName")}
                    placeholder="My Awesome Project"
                  />
                  {errors.projectName && (
                    <p className="mt-1.5 text-sm text-destructive">
                      {errors.projectName.message}
                    </p>
                  )}
                  <p className="mt-1.5 text-xs text-muted-foreground">
                    Choose a descriptive name for your project
                  </p>
                </div>

                <div>
                  <Label
                    htmlFor="description"
                    className="text-foreground font-semibold mb-1.5 block"
                  >
                    Description (Optional)
                  </Label>
                  <Textarea
                    id="description"
                    {...register("description")}
                    placeholder="Describe what this project is about..."
                    rows={4}
                  />
                </div>

                <div className="bg-muted/50 rounded-md p-4 border border-border">
                  <h3 className="text-sm font-semibold text-foreground mb-3 flex items-center gap-2">
                    <Database className="w-4 h-4 text-primary" />
                    Connection Details
                  </h3>
                  <div className="space-y-2 text-sm">
                    <div className="flex justify-between items-center">
                      <span className="text-muted-foreground">Server:</span>
                      <span className="font-medium text-foreground">
                        {getValues("server") as string}:{getValues("port") as number}
                      </span>
                    </div>
                    <div className="h-px bg-border" />
                    <div className="flex justify-between items-center">
                      <span className="text-muted-foreground">Database:</span>
                      <span className="font-medium text-foreground">
                        {getValues("databaseName") as string}
                      </span>
                    </div>
                    <div className="h-px bg-border" />
                    <div className="flex justify-between items-center">
                      <span className="text-muted-foreground">Username:</span>
                      <span className="font-medium text-foreground">
                        {getValues("username") as string}
                      </span>
                    </div>
                  </div>
                </div>

                <div className="flex gap-3 pt-4">
                  <Button
                    variant="outline"
                    type="button"
                    onClick={() => setStep("connection")}
                    className="px-6"
                  >
                    <ArrowLeft className="w-4 h-4 mr-2" />
                    Back
                  </Button>
                  <Button
                    type="submit"
                    disabled={isCreating}
                    className="flex-1 flex items-center justify-center"
                  >
                    {isCreating ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        {createdProjectId ? "Linking..." : "Creating..."}
                      </>
                    ) : (
                      <>
                        Create & Link Project
                        <CheckCircle className="w-4 h-4 ml-2" />
                      </>
                    )}
                  </Button>
                </div>
              </form>
            )}
          </div>
        </ScrollArea>

        {/* Footer Note */}
        <p className="text-center text-sm text-muted-foreground mt-6 flex-shrink-0">
          Connection details are used temporarily to link your database and are
          not stored
        </p>
      </div>
    </div>
  );
}
