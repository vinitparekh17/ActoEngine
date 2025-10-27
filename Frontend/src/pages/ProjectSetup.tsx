import { useState } from "react"
import { useNavigate } from "react-router-dom"
import {
  Database,
  CheckCircle,
  AlertCircle,
  Loader2,
  ArrowRight,
  ArrowLeft,
  Sparkles,
} from "lucide-react"
import { useApiPost } from "../hooks/useApi"
import { Input } from "../components/ui/input"
import { Button } from "../components/ui/button"
import { Label } from "../components/ui/label"
import { Textarea } from "../components/ui/textarea"
import { useForm } from "react-hook-form"
import type {
  VerifyConnectionRequest,
  ConnectionResponse,
  CreateProjectRequest,
  LinkProjectRequest,
  ProjectResponse,
} from "../types/api"
import { ScrollArea } from "../components/ui/scroll-area"

interface FormData {
  server: string
  databaseName: string
  username: string
  password: string
  port: number
  databaseType: string
  projectName: string
  description: string
}

type Step = "connection" | "details"

export default function ProjectSetup() {
  const navigate = useNavigate()
  const [step, setStep] = useState<Step>("connection")
  const [isLinking, setIsLinking] = useState(false)
  const [verificationResult, setVerificationResult] = useState<{
    success: boolean
    message: string
  } | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors },
    getValues,
    trigger,
  } = useForm<FormData>({
    defaultValues: {
      server: "",
      databaseName: "",
      username: "",
      password: "",
      port: 1433,
      databaseType: "SqlServer",
      projectName: "",
      description: "",
    },
  })

  const buildConnectionString = (data: FormData) => {
    return `Server=${data.server},${data.port};Database=${data.databaseName};User Id=${data.username};Password=${data.password};TrustServerCertificate=True;`
  }

  const verifyMutation = useApiPost<
    ConnectionResponse,
    VerifyConnectionRequest
  >("/Project/verify", {
    showSuccessToast: false,
    showErrorToast: false,
    onSuccess: (data) => {
      if (data.isValid) {
        setVerificationResult({
          success: true,
          message: "Connection successful!",
        })
        setStep("details")
      } else {
        setVerificationResult({
          success: false,
          message: data.message || "Connection failed",
        })
      }
    },
    onError: (error) => {
      setVerificationResult({
        success: false,
        message: error.message || "Connection failed",
      })
    },
  })

  const createMutation = useApiPost<ProjectResponse, CreateProjectRequest>(
    "/Project",
    {
      successMessage: "Project created successfully",
      invalidateKeys: [["projects"]],
      onSuccess: (data) => {
        navigate(`/project/${data.projectId}`)
      },
    }
  )

  const linkMutation = useApiPost<ProjectResponse, LinkProjectRequest>(
    "/Project/link",
    {
      successMessage: "Project linked successfully",
      invalidateKeys: [["projects"]],
      onSuccess: (data) => {
        navigate(`/project/${data.projectId}`)
      },
    }
  )

  const handleVerifyConnection = async () => {
    const isValid = await trigger([
      "server",
      "databaseName",
      "username",
      "password",
      "port",
    ])
    if (!isValid) return

    setVerificationResult(null)
    const values = getValues()
    verifyMutation.mutate({
      server: values.server,
      databaseName: values.databaseName,
      username: values.username,
      password: values.password,
      port: values.port,
      databaseType: values.databaseType,
    })
  }

  const handleCreateProject = (data: FormData) => {
    const baseData = {
      projectName: data.projectName,
      description: data.description,
      databaseName: data.databaseName,
      connectionString: buildConnectionString(data),
      databaseType: data.databaseType,
    }

    if (isLinking) {
      linkMutation.mutate({
        projectId: 0,
        ...baseData,
      })
    } else {
      createMutation.mutate(baseData)
    }
  }

  const isCreating = createMutation.isPending || linkMutation.isPending

  return (
    <div className="h-[calc(100vh-110px)] bg-background flex items-center justify-center p-4 overflow-hidden">
      <div className="w-full max-w-2xl flex flex-col h-full max-h-[calc(100vh-2rem)]">
        {/* Header */}
        <div className="text-center mb-6 flex-shrink-0">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-primary/10 mb-3">
            <Sparkles className="w-6 h-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-foreground mb-1">
            {isLinking ? "Link Existing Database" : "Create New Project"}
          </h1>
          <p className="text-muted-foreground">
            Connect your project's database and get started in minutes
          </p>
        </div>

        {/* Progress Indicator */}
        <div className="mb-6 flex-shrink-0">
          <div className="flex items-center justify-between max-w-md mx-auto">
            <div className="flex flex-col items-center">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-semibold text-sm transition-all ${
                  step === "connection"
                    ? "bg-primary text-primary-foreground shadow-md scale-110"
                    : "bg-primary/20 text-primary"
                }`}
              >
                1
              </div>
              <p
                className={`mt-1.5 text-xs font-medium transition-colors ${
                  step === "connection"
                    ? "text-primary"
                    : "text-primary/60"
                }`}
              >
                Connection
              </p>
            </div>

            <div
              className={`flex-1 h-0.5 mx-4 rounded-full transition-all ${
                step === "details" ? "bg-primary" : "bg-border"
              }`}
            />

            <div className="flex flex-col items-center">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-semibold text-sm transition-all ${
                  step === "details"
                    ? "bg-primary text-primary-foreground shadow-md scale-110"
                    : "bg-muted text-muted-foreground"
                }`}
              >
                2
              </div>
              <p
                className={`mt-1.5 text-xs font-medium transition-colors ${
                  step === "details"
                    ? "text-primary"
                    : "text-muted-foreground"
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
                <div>
                  <Label
                    htmlFor="server"
                    className="text-foreground font-semibold mb-1.5 block"
                  >
                    Server Address
                  </Label>
                  <Input
                    id="server"
                    {...register("server", {
                      required: "Server address is required",
                    })}
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
                      {...register("databaseName", {
                        required: "Database name is required",
                      })}
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
                      {...register("port", {
                        required: "Port is required",
                        min: {
                          value: 1,
                          message: "Port must be greater than 0",
                        },
                        max: {
                          value: 65535,
                          message: "Port must be less than 65536",
                        },
                        valueAsNumber: true,
                      })}
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
                      {...register("username", {
                        required: "Username is required",
                      })}
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
                      {...register("password", {
                        required: "Password is required",
                      })}
                      placeholder="••••••••"
                    />
                    {errors.password && (
                      <p className="mt-1.5 text-sm text-destructive">
                        {errors.password.message}
                      </p>
                    )}
                  </div>
                </div>

                {verificationResult && (
                  <div
                    className={`flex items-start gap-3 p-3 rounded-md border transition-all ${
                      verificationResult.success
                        ? "bg-primary/5 border-primary/20"
                        : "bg-destructive/5 border-destructive/20"
                    }`}
                  >
                    {verificationResult.success ? (
                      <CheckCircle className="w-5 h-5 text-primary mt-0.5 flex-shrink-0" />
                    ) : (
                      <AlertCircle className="w-5 h-5 text-destructive mt-0.5 flex-shrink-0" />
                    )}
                    <p
                      className={`text-sm ${
                        verificationResult.success
                          ? "text-primary"
                          : "text-destructive"
                      }`}
                    >
                      {verificationResult.message}
                    </p>
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
                  <div className="space-y-2">
                    <div className="flex items-start gap-2 text-sm text-muted-foreground mb-3">
                      <div className="w-1.5 h-1.5 rounded-full bg-muted-foreground mt-1.5 flex-shrink-0" />
                      <p>
                        {isLinking
                          ? "Link mode: Connect an existing database that already has ActoX metadata tables."
                          : "Create mode: Set up a new project and initialize ActoX tracking tables in the database."}
                      </p>
                    </div>
                    <Button
                      variant="link"
                      type="button"
                      onClick={() => setIsLinking(!isLinking)}
                      className="text-sm px-0 h-auto"
                    >
                      {isLinking
                        ? "← Switch to Create New Project"
                        : "Already have ActoX tables? Switch to Link Mode →"}
                    </Button>
                  </div>
                </div>
              </div>
            )}

            {/* Step 2: Project Details */}
            {step === "details" && (
              <form
                onSubmit={handleSubmit(handleCreateProject)}
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
                    {...register("projectName", {
                      required: "Project name is required",
                    })}
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
                        {getValues("server")}:{getValues("port")}
                      </span>
                    </div>
                    <div className="h-px bg-border" />
                    <div className="flex justify-between items-center">
                      <span className="text-muted-foreground">Database:</span>
                      <span className="font-medium text-foreground">
                        {getValues("databaseName")}
                      </span>
                    </div>
                    <div className="h-px bg-border" />
                    <div className="flex justify-between items-center">
                      <span className="text-muted-foreground">Username:</span>
                      <span className="font-medium text-foreground">
                        {getValues("username")}
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
                        {isLinking ? "Linking..." : "Creating..."}
                      </>
                    ) : (
                      <>
                        {isLinking ? "Link Database" : "Create Project"}
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
          Your connection details are encrypted and stored securely
        </p>
      </div>
    </div>
  )
}