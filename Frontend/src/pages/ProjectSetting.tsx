import { useEffect } from "react";
import { Link, useParams, useNavigate } from "react-router-dom";
import { useProject } from "../hooks/useProject";
import { useApiPut, useApiDelete, useApiPost } from "../hooks/useApi";
import { useAuthorization } from "../hooks/useAuth";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "../components/ui/card";
import { Button } from "../components/ui/button";
import { Input } from "../components/ui/input";
import { Textarea } from "../components/ui/textarea";
import { Badge } from "../components/ui/badge";
import {
  Database,
  Save,
  Trash2,
  RefreshCw,
  AlertCircle,
  Loader2,
  Eye,
  EyeOff,
} from "lucide-react";
import { toast } from "sonner";
import { useForm } from "react-hook-form";
import { utcToLocal } from "../lib/utils";
import type {
  VerifyConnectionRequest,
  ConnectionResponse,
  UpdateProjectRequest,
  UpdateProjectResponse,
  DeleteProjectResponse,
  ProjectFormData,
  LinkProjectRequest,
  ReSyncProjectRequest,
  ProjectResponse,
} from "../types/project";
import { useConfirm } from "../hooks/useConfirm";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "../components/ui/breadcrumb";
import {
  Field,
  FieldDescription,
  FieldGroup,
  FieldLabel,
  FieldSet,
} from "../components/ui/field";

interface ConnectionFormData {
  server: string;
  username: string;
  password: string;
  port: number;
  databaseType: string;
  showPassword: boolean;
}

export default function ProjectSettings() {
  const { projectId } = useParams<{ projectId: string }>();
  const { confirm } = useConfirm();
  const navigate = useNavigate();
  const { selectedProject } = useProject();

  // Project details form
  const {
    register: registerProject,
    handleSubmit: handleSubmitProject,
    reset: resetProject,
    formState: { errors: projectErrors, isDirty: hasChanges },
  } = useForm<ProjectFormData>({
    defaultValues: {
      projectName: "",
      description: "",
      databaseName: "",
      isActive: true,
    },
  });

  // Connection form
  const {
    register: registerConnection,
    handleSubmit: handleSubmitConnection,
    formState: { errors: connectionErrors },
    watch,
    setValue: setConnectionValue,
  } = useForm<ConnectionFormData>({
    defaultValues: {
      server: "",
      username: "",
      password: "",
      port: 1433,
      databaseType: "SqlServer",
      showPassword: false,
    },
  });

  const showPassword = watch("showPassword");

  useEffect(() => {
    if (!selectedProject) return;

    resetProject({
      projectName: selectedProject.projectName,
      description: selectedProject.description || "",
      databaseName: selectedProject.databaseName || "",
      isActive: true,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProject?.projectId]);

  // Update mutation - Remove refetchProjects call
  const updateMutation = useApiPut<UpdateProjectResponse, UpdateProjectRequest>(
    `/projects/${projectId}`,
    {
      successMessage: "Project updated successfully",
      invalidateKeys: [["projects"], ["projects", projectId!]],
    },
  );

  // Delete mutation
  const deleteMutation = useApiDelete<
    DeleteProjectResponse,
    { projectId: string }
  >(`/projects/:projectId`, {
    successMessage: "Project deleted successfully",
    invalidateKeys: [["projects"]],
    onSuccess: () => {
      navigate("/projects");
    },
  });

  // Verify connection mutation
  const verifyMutation = useApiPost<
    ConnectionResponse,
    VerifyConnectionRequest
  >("/projects/verify", {
    showSuccessToast: false,
    showErrorToast: true,
  });

  // Link mutation
  const linkMutation = useApiPost<ProjectResponse, LinkProjectRequest>(
    "/projects/link",
    {
      successMessage: "Database linked successfully! Sync in progress...",
      invalidateKeys: [["projects", projectId!]],
    },
  );

  // Re-sync mutation
  const resyncMutation = useApiPost<ProjectResponse, ReSyncProjectRequest>(
    "/projects/resync",
    {
      successMessage: "Re-sync started successfully!",
      invalidateKeys: [["projects", projectId!]],
    },
  );

  const handleSave = (data: ProjectFormData) => {
    updateMutation.mutate({
      projectName: data.projectName,
      description: data.description,
      isActive: data.isActive,
      databaseName: data.databaseName,
    });
  };

  const handleDelete = async () => {
    const isConfirmed = await confirm({
      title: "Confirm Project Deletion",
      description:
        "This will permanently delete all forms, code generations, and history. Continue?",
      confirmText: "Delete",
      cancelText: "Cancel",
      variant: "destructive",
    });
    if (isConfirmed) {
      deleteMutation.mutate({ projectId: projectId! });
    }
  };

  const handleTestConnection = (data: ConnectionFormData) => {
    if (!selectedProject) return;

    verifyMutation.mutate(
      {
        server: data.server,
        databaseName: selectedProject.databaseName!,
        username: data.username,
        password: data.password,
        port: data.port,
        databaseType: data.databaseType,
      },
      {
        onSuccess: (response) => {
          if (response.isValid) {
            toast.success("Connection successful!");
          } else {
            toast.error(response.message || "Connection failed");
          }
        },
      },
    );
  };

  const buildConnectionString = (data: ConnectionFormData) => {
    return `Server=${data.server},${data.port};Database=${selectedProject?.databaseName};User Id=${data.username};Password=${data.password};TrustServerCertificate=True;`;
  };

  const handleLinkDatabase = async (data: ConnectionFormData) => {
    if (!selectedProject) return;

    const isConfirmed = await confirm({
      title: "Link Database",
      description:
        "This will link your project to the database and start schema sync. Connection string will be used temporarily and not stored.",
      confirmText: "Link",
      cancelText: "Cancel",
      variant: "default",
    });

    if (isConfirmed) {
      const connectionString = buildConnectionString(data);
      linkMutation.mutate({
        projectId: Number(projectId),
        connectionString,
      });
    }
  };

  const handleResyncDatabase = async (data: ConnectionFormData) => {
    if (!selectedProject) return;

    const isConfirmed = await confirm({
      title: "Re-sync Database",
      description:
        "This will re-sync your database schema. Connection string will be used temporarily and not stored.",
      confirmText: "Re-sync",
      cancelText: "Cancel",
      variant: "default",
    });

    if (isConfirmed) {
      const connectionString = buildConnectionString(data);
      resyncMutation.mutate({
        projectId: Number(projectId),
        connectionString,
      });
    }
  };

  if (!selectedProject) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <Loader2 className="w-8 h-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="container mx-auto py-6 space-y-6 max-w-4xl">
      {/* Breadcrumb */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink asChild>
              <Link to="/projects">Projects</Link>
            </BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink asChild>
              <Link to={`/project/${projectId}`}>
                {selectedProject.projectName}
              </Link>
            </BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Settings</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Project Settings</h1>
        <p className="text-muted-foreground mt-1">
          Manage your project configuration and connection
        </p>
      </div>

      {/* Basic Information */}
      <Card>
        <CardHeader>
          <CardTitle>Basic Information</CardTitle>
          <CardDescription>Update project name and description</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmitProject(handleSave)}>
            <FieldGroup>
              <FieldSet>
                {/* Project Name + Database Name side by side */}
                <div className="grid grid-cols-2 gap-4">
                  <Field>
                    <FieldLabel htmlFor="projectName">Project Name</FieldLabel>
                    <Input
                      id="projectName"
                      {...registerProject("projectName", {
                        required: "Project name is required",
                      })}
                      placeholder="My Project"
                    />
                    {projectErrors.projectName && (
                      <FieldDescription className="text-destructive">
                        {projectErrors.projectName.message}
                      </FieldDescription>
                    )}
                  </Field>

                  <Field>
                    <FieldLabel htmlFor="databaseName">
                      Database Name
                    </FieldLabel>
                    <Input
                      id="databaseName"
                      {...registerProject("databaseName")}
                      placeholder="MyDatabase"
                      disabled
                      className="bg-muted"
                    />
                    <FieldDescription className="text-xs text-muted-foreground">
                      Database name cannot be changed after creation
                    </FieldDescription>
                  </Field>
                </div>

                {/* Description */}
                <Field>
                  <FieldLabel htmlFor="description">Description</FieldLabel>
                  <Textarea
                    id="description"
                    {...registerProject("description")}
                    placeholder="Project description..."
                    rows={3}
                  />
                </Field>
              </FieldSet>

              {/* Save + Unsaved Changes */}
              <div className="flex items-center justify-between mt-6">
                {useAuthorization("Projects:Update") && (
                  <Button
                    type="submit"
                    disabled={!hasChanges || updateMutation.isPending}
                  >
                    {updateMutation.isPending ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Saving...
                      </>
                    ) : (
                      <>
                        <Save className="w-4 h-4 mr-2" />
                        Save Changes
                      </>
                    )}
                  </Button>
                )}

                {hasChanges && (
                  <Badge variant="secondary" className="gap-1">
                    <AlertCircle className="w-3 h-3" />
                    Unsaved changes
                  </Badge>
                )}
              </div>
            </FieldGroup>
          </form>
        </CardContent>
      </Card>

      {/* Link Database */}
      {!selectedProject.isLinked && useAuthorization("Projects:Link") && (
        <Card className="border-primary/50">
          <CardHeader>
            <CardTitle>Link Database</CardTitle>
            <CardDescription>
              Connect your project to a database and sync schema metadata
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmitConnection(handleLinkDatabase)}>
              <FieldGroup>
                <div className="mb-4 p-3 bg-blue-50 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-900 rounded-md">
                  <p className="text-sm text-blue-900 dark:text-blue-100">
                    <strong>Note:</strong> Connection details are used
                    temporarily to sync schema metadata and are not stored
                    permanently.
                  </p>
                </div>

                <FieldSet>
                  {/* Server + Port */}
                  <div className="grid grid-cols-2 gap-4">
                    <Field>
                      <FieldLabel htmlFor="link-server">
                        Server Address
                      </FieldLabel>
                      <Input
                        id="link-server"
                        {...registerConnection("server", {
                          required: "Server address is required",
                        })}
                        placeholder="localhost"
                      />
                    </Field>
                    <Field>
                      <FieldLabel htmlFor="link-port">Port</FieldLabel>
                      <Input
                        id="link-port"
                        type="number"
                        {...registerConnection("port", {
                          required: "Port is required",
                          valueAsNumber: true,
                        })}
                      />
                    </Field>
                  </div>

                  {/* Username + Password */}
                  <div className="grid grid-cols-2 gap-4">
                    <Field>
                      <FieldLabel htmlFor="link-username">Username</FieldLabel>
                      <Input
                        id="link-username"
                        {...registerConnection("username", {
                          required: "Username is required",
                        })}
                        placeholder="sa"
                      />
                    </Field>
                    <Field>
                      <FieldLabel htmlFor="link-password">Password</FieldLabel>
                      <div className="relative">
                        <Input
                          id="link-password"
                          type={showPassword ? "text" : "password"}
                          {...registerConnection("password", {
                            required: "Password is required",
                          })}
                          placeholder="••••••••"
                          className="pr-10"
                        />
                        <button
                          type="button"
                          onClick={() =>
                            setConnectionValue("showPassword", !showPassword)
                          }
                          className="absolute right-3 top-1/2 -translate-y-1/2"
                        >
                          {showPassword ? (
                            <EyeOff className="w-4 h-4" />
                          ) : (
                            <Eye className="w-4 h-4" />
                          )}
                        </button>
                      </div>
                    </Field>
                  </div>
                </FieldSet>

                <div className="flex gap-3 mt-6">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleSubmitConnection(handleTestConnection)}
                    disabled={verifyMutation.isPending}
                  >
                    {verifyMutation.isPending ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Testing...
                      </>
                    ) : (
                      <>
                        <Database className="w-4 h-4 mr-2" />
                        Test Connection
                      </>
                    )}
                  </Button>
                  <Button type="submit" disabled={linkMutation.isPending}>
                    {linkMutation.isPending ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Linking...
                      </>
                    ) : (
                      <>
                        <Database className="w-4 h-4 mr-2" />
                        Link Database
                      </>
                    )}
                  </Button>
                </div>
              </FieldGroup>
            </form>
          </CardContent>
        </Card>
      )}

      {/* Re-sync Database */}
      {selectedProject.isLinked && useAuthorization("Schema:Sync") && (
        <Card>
          <CardHeader>
            <CardTitle>Re-sync Database</CardTitle>
            <CardDescription>
              Update schema metadata from your database
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="mb-4 flex items-start gap-3 p-4 border rounded-lg bg-muted/50">
              <Database className="w-5 h-5 text-muted-foreground mt-0.5" />
              <div className="flex-1 space-y-1">
                <p className="text-sm font-medium">Last Sync</p>
                <p className="text-sm text-muted-foreground">
                  {selectedProject.lastSyncAttempt
                    ? utcToLocal(selectedProject.lastSyncAttempt)
                    : "Never synced"}
                </p>
                {selectedProject.syncStatus && (
                  <Badge
                    variant={
                      selectedProject.syncStatus === "Completed"
                        ? "default"
                        : "secondary"
                    }
                  >
                    {selectedProject.syncStatus}
                  </Badge>
                )}
              </div>
            </div>

            <form onSubmit={handleSubmitConnection(handleResyncDatabase)}>
              <FieldGroup>
                <div className="mb-4 p-3 bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900 rounded-md">
                  <p className="text-sm text-amber-900 dark:text-amber-100">
                    <strong>Note:</strong> Connection details are used
                    temporarily and are not stored.
                  </p>
                </div>

                <FieldSet>
                  {/* Server + Port */}
                  <div className="grid grid-cols-2 gap-4">
                    <Field>
                      <FieldLabel htmlFor="resync-server">
                        Server Address
                      </FieldLabel>
                      <Input
                        id="resync-server"
                        {...registerConnection("server", {
                          required: "Server address is required",
                        })}
                        placeholder="localhost"
                      />
                    </Field>
                    <Field>
                      <FieldLabel htmlFor="resync-port">Port</FieldLabel>
                      <Input
                        id="resync-port"
                        type="number"
                        {...registerConnection("port", {
                          required: "Port is required",
                          valueAsNumber: true,
                        })}
                      />
                    </Field>
                  </div>

                  {/* Username + Password */}
                  <div className="grid grid-cols-2 gap-4">
                    <Field>
                      <FieldLabel htmlFor="resync-username">
                        Username
                      </FieldLabel>
                      <Input
                        id="resync-username"
                        {...registerConnection("username", {
                          required: "Username is required",
                        })}
                        placeholder="sa"
                      />
                    </Field>
                    <Field>
                      <FieldLabel htmlFor="resync-password">
                        Password
                      </FieldLabel>
                      <div className="relative">
                        <Input
                          id="resync-password"
                          type={showPassword ? "text" : "password"}
                          {...registerConnection("password", {
                            required: "Password is required",
                          })}
                          placeholder="••••••••"
                          className="pr-10"
                        />
                        <button
                          type="button"
                          onClick={() =>
                            setConnectionValue("showPassword", !showPassword)
                          }
                          className="absolute right-3 top-1/2 -translate-y-1/2"
                        >
                          {showPassword ? (
                            <EyeOff className="w-4 h-4" />
                          ) : (
                            <Eye className="w-4 h-4" />
                          )}
                        </button>
                      </div>
                    </Field>
                  </div>
                </FieldSet>

                <div className="flex gap-3 mt-6">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleSubmitConnection(handleTestConnection)}
                    disabled={verifyMutation.isPending}
                  >
                    {verifyMutation.isPending ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Testing...
                      </>
                    ) : (
                      <>
                        <Database className="w-4 h-4 mr-2" />
                        Test Connection
                      </>
                    )}
                  </Button>
                  <Button type="submit" disabled={resyncMutation.isPending}>
                    {resyncMutation.isPending ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                        Re-syncing...
                      </>
                    ) : (
                      <>
                        <RefreshCw className="w-4 h-4 mr-2" />
                        Re-sync Database
                      </>
                    )}
                  </Button>
                </div>
              </FieldGroup>
            </form>
          </CardContent>
        </Card>
      )}

      {/* Danger Zone */}
      {useAuthorization("Projects:Delete") && (
        <Card className="border-destructive">
          <CardHeader>
            <CardTitle className="text-destructive">Danger Zone</CardTitle>
            <CardDescription>Irreversible actions</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <p className="font-medium">Delete Project</p>
                <p className="text-sm text-muted-foreground">
                  Permanently delete this project and all associated data
                  including forms, code generations, and history.
                </p>
              </div>
              <Button
                variant="destructive"
                onClick={handleDelete}
                disabled={deleteMutation.isPending}
                className="flex-shrink-0"
              >
                {deleteMutation.isPending ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                    Deleting...
                  </>
                ) : (
                  <>
                    <Trash2 className="w-4 h-4 mr-2" />
                    Delete Project
                  </>
                )}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
