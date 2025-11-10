import { useEffect } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import { useProject } from '../hooks/useProject';
import { useApiPut, useApiDelete, useApiPost } from '../hooks/useApi';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../components/ui/card';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Textarea } from '../components/ui/textarea';
import { Badge } from '../components/ui/badge';
import {
    Database,
    Save,
    Trash2,
    RefreshCw,
    AlertCircle,
    Loader2,
    Eye,
    EyeOff
} from 'lucide-react';
import { toast } from 'sonner';
import { useForm } from 'react-hook-form';
import type {
    VerifyConnectionRequest,
    ConnectionResponse,
    UpdateProjectRequest,
    UpdateProjectResponse,
    DeleteProjectResponse,
    SyncProjectResponse,
    ProjectFormData,
} from '../types/project';
import { useConfirm } from '../hooks/useConfirm';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '../components/ui/breadcrumb';
import { Field, FieldDescription, FieldGroup, FieldLabel, FieldSet } from '../components/ui/field';

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
        formState: { errors: projectErrors, isDirty: hasChanges }
    } = useForm<ProjectFormData>({
        defaultValues: {
            projectName: '',
            description: '',
            databaseName: '',
            isActive: true
        }
    });

    // Connection form
    const {
        register: registerConnection,
        handleSubmit: handleSubmitConnection,
        formState: { errors: connectionErrors },
        watch,
        setValue: setConnectionValue
    } = useForm<ConnectionFormData>({
        defaultValues: {
            server: '',
            username: '',
            password: '',
            port: 1433,
            databaseType: 'SqlServer',
            showPassword: false
        }
    });

    const showPassword = watch('showPassword');

    useEffect(() => {
        if (!selectedProject) return;

        resetProject({
            projectName: selectedProject.projectName,
            description: selectedProject.description || '',
            databaseName: selectedProject.databaseName || '',
            isActive: true,
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [selectedProject?.projectId]);

    // Update mutation - Remove refetchProjects call
    const updateMutation = useApiPut<UpdateProjectResponse, UpdateProjectRequest>(
        `/Project/${projectId}`,
        {
            successMessage: 'Project updated successfully',
            invalidateKeys: [['projects'], ['projects', projectId!]]
        }
    );

    // Delete mutation
    const deleteMutation = useApiDelete<DeleteProjectResponse, { projectId: string }>(
        `/Project/:projectId`,
        {
            successMessage: 'Project deleted successfully',
            invalidateKeys: [['projects']],
            onSuccess: () => {
                navigate('/projects');
            }
        }
    );

    // Verify connection mutation
    const verifyMutation = useApiPost<ConnectionResponse, VerifyConnectionRequest>(
        '/Project/verify',
        {
            showSuccessToast: false,
            showErrorToast: false
        }
    );

    // Sync mutation
    const syncMutation = useApiPost<SyncProjectResponse, Record<string, never>>(
        `/Project/${projectId}/sync`,
        {
            successMessage: 'Sync started successfully',
            invalidateKeys: [['projects', projectId!]]
        }
    );

    const handleSave = (data: ProjectFormData) => {
        updateMutation.mutate({
            projectName: data.projectName,
            description: data.description,
            isActive: data.isActive,
            databaseName: data.databaseName
        });
    };

    const handleDelete = async () => {
        const isConfirmed = await confirm({
            title: 'Confirm Project Deletion',
            description: 'This will permanently delete all forms, code generations, and history. Continue?',
            confirmText: 'Delete',
            cancelText: 'Cancel',
            variant: 'destructive'
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
                databaseType: data.databaseType
            },
            {
                onSuccess: (response) => {
                    if (response.isValid) {
                        toast.success('Connection successful!');
                    } else {
                        toast.error(response.message || 'Connection failed');
                    }
                }
            }
        );
    };

    const handleTriggerSync = async () => {
        const isConfirmed = await confirm({
            title: 'Confirm Database Sync',
            description: 'This will sync the database schema. Continue?',
            confirmText: 'Sync',
            cancelText: 'Cancel',
            variant: 'default'
        });
        if (isConfirmed) {
            syncMutation.mutate({});
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
                            <Link to={`/project/${projectId}`}>{selectedProject.projectName}</Link>
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
                                        <FieldLabel htmlFor="databaseName">Database Name</FieldLabel>
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
                                <Button
                                    type="submit"
                                    disabled={!hasChanges || updateMutation.isPending}>
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

            {/* Database Connection */}
            <Card>
                <CardHeader>
                    <CardTitle>Database Connection</CardTitle>
                    <CardDescription>Test or update your database connection</CardDescription>
                </CardHeader>

                <CardContent>
                    <form
                        onSubmit={handleSubmitConnection(handleTestConnection)}
                    >
                        <FieldGroup>
                            <FieldSet>
                                {/* Server + Port side by side */}
                                <div className="grid grid-cols-2 gap-4">
                                    <Field>
                                        <FieldLabel htmlFor="server">Server Address</FieldLabel>
                                        <Input
                                            id="server"
                                            {...registerConnection("server", {
                                                required: "Server address is required",
                                            })}
                                            placeholder="localhost"
                                        />
                                        {connectionErrors.server && (
                                            <FieldDescription className="text-destructive">
                                                {connectionErrors.server.message}
                                            </FieldDescription>
                                        )}
                                    </Field>

                                    <Field>
                                        <FieldLabel htmlFor="port">Port</FieldLabel>
                                        <Input
                                            id="port"
                                            type="number"
                                            {...registerConnection("port", {
                                                required: "Port is required",
                                                min: { value: 1, message: "Port must be greater than 0" },
                                                max: { value: 65535, message: "Port must be less than 65536" },
                                                valueAsNumber: true,
                                            })}
                                        />
                                        {connectionErrors.port && (
                                            <FieldDescription className="text-destructive">
                                                {connectionErrors.port.message}
                                            </FieldDescription>
                                        )}
                                    </Field>
                                </div>

                                {/* Username + Password side by side */}
                                <div className="grid grid-cols-2 gap-4">
                                    <Field>
                                        <FieldLabel htmlFor="username">Username</FieldLabel>
                                        <Input
                                            id="username"
                                            {...registerConnection("username", {
                                                required: "Username is required",
                                            })}
                                            placeholder="sa"
                                        />
                                        {connectionErrors.username && (
                                            <FieldDescription className="text-destructive">
                                                {connectionErrors.username.message}
                                            </FieldDescription>
                                        )}
                                    </Field>

                                    <Field>
                                        <FieldLabel htmlFor="password">Password</FieldLabel>
                                        <div className="relative">
                                            <Input
                                                id="password"
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
                                                className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                                            >
                                                {showPassword ? (
                                                    <EyeOff className="w-4 h-4" />
                                                ) : (
                                                    <Eye className="w-4 h-4" />
                                                )}
                                            </button>
                                        </div>
                                        {connectionErrors.password && (
                                            <FieldDescription className="text-destructive">
                                                {connectionErrors.password.message}
                                            </FieldDescription>
                                        )}
                                    </Field>
                                </div>
                            </FieldSet>

                            {/* Test Connection Button */}
                            <div className="mt-6">
                                <Button
                                    type="submit"
                                    variant="outline"
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
                            </div>
                        </FieldGroup>
                    </form>
                </CardContent>

            </Card>

            {/* Database Sync */}
            <Card>
                <CardHeader>
                    <CardTitle>Database Synchronization</CardTitle>
                    <CardDescription>Manually trigger schema sync</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="flex items-start gap-3 p-4 border rounded-lg bg-muted/50">
                        <Database className="w-5 h-5 text-muted-foreground mt-0.5" />
                        <div className="flex-1 space-y-1">
                            <p className="text-sm font-medium">Last Sync</p>
                            <p className="text-sm text-muted-foreground">
                                {selectedProject.lastSyncAttempt
                                    ? new Date(selectedProject.lastSyncAttempt).toLocaleString()
                                    : 'Never synced'}
                            </p>
                        </div>
                    </div>

                    <Button
                        variant="outline"
                        onClick={handleTriggerSync}
                        disabled={syncMutation.isPending}
                    >
                        {syncMutation.isPending ? (
                            <>
                                <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                                Syncing...
                            </>
                        ) : (
                            <>
                                <RefreshCw className="w-4 h-4 mr-2" />
                                Trigger Sync Now
                            </>
                        )}
                    </Button>
                </CardContent>
            </Card>

            {/* Danger Zone */}
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
                                Permanently delete this project and all associated data including
                                forms, code generations, and history.
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
        </div>
    );
}