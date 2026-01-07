import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  useApi,
  useApiPost,
  useApiPut,
  useApiDelete,
  queryKeys,
} from "../hooks/useApi";
import { useAuthorization } from "../hooks/useAuth";
import { useConfirm } from "../hooks/useConfirm";
import { Button } from "../components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../components/ui/table";
import { Input } from "../components/ui/input";
import { Label } from "../components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "../components/ui/dialog";
import {
  TableSkeleton,
  PageHeaderSkeleton,
  LoadingContainer,
} from "../components/ui/skeletons";
import { Pencil, Trash2, Plus, Users } from "lucide-react";
import { Badge } from "../components/ui/badge";
import type { Client, CreateClientRequest } from "../types/client";

// Zod schema for client form
const clientSchema = z.object({
  clientName: z.string().min(1, "Name is required"),
  projectId: z.number().int().positive("Project ID must be a positive integer"),
});

type ClientFormValues = z.infer<typeof clientSchema>;

// ClientTableRow Component
interface ClientTableRowProps {
  readonly client: Client;
  readonly canUpdate: boolean;
  readonly canDelete: boolean;
  readonly onEdit: (client: Client) => void;
  readonly onDelete: (client: Client) => void;
}

function ClientTableRow({
  client,
  canUpdate,
  canDelete,
  onEdit,
  onDelete,
}: ClientTableRowProps) {
  return (
    <TableRow>
      <TableCell className="font-medium">{client.clientName}</TableCell>
      <TableCell>
        <code className="relative rounded bg-muted px-[0.3rem] py-[0.2rem] font-mono text-sm">
          {client.clientId}
        </code>
      </TableCell>
      <TableCell>
        <Badge variant={client.isActive ? "default" : "secondary"}>
          {client.isActive ? "Active" : "Inactive"}
        </Badge>
      </TableCell>
      <TableCell>
        {new Date(client.createdAt).toLocaleDateString()}
      </TableCell>
      <TableCell className="text-right">
        <div className="flex justify-end gap-2">
          {canUpdate && (
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onEdit(client)}
            >
              <Pencil className="w-4 h-4" />
            </Button>
          )}
          {canDelete && (
            <Button
              variant="ghost"
              size="icon"
              className="text-destructive hover:text-destructive"
              onClick={() => onDelete(client)}
            >
              <Trash2 className="w-4 h-4" />
            </Button>
          )}
        </div>
      </TableCell>
    </TableRow>
  );
}

// ClientModal Component
interface ClientModalProps {
  readonly isOpen: boolean;
  readonly isEditing: boolean;
  readonly defaultValues?: ClientFormValues;
  readonly isPending: boolean;
  readonly onSubmit: (data: ClientFormValues) => void;
  readonly onClose: (open: boolean) => void;
}

function ClientModal({
  isOpen,
  isEditing,
  defaultValues,
  isPending,
  onSubmit,
  onClose,
}: ClientModalProps) {
  const title = isEditing ? "Edit Client" : "Create New Client";
  const description = isEditing
    ? "Update the client details below and click 'Update Client' to save changes."
    : "Enter the details below and click 'Create Client' to add a new client.";

  let buttonText: string;
  if (isEditing) {
    buttonText = isPending ? "Updating..." : "Update Client";
  } else {
    buttonText = isPending ? "Creating..." : "Create Client";
  }

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ClientFormValues>({
    resolver: zodResolver(clientSchema),
    defaultValues: { clientName: "", projectId: 1 },
  });

  // Reset form when modal opens or defaultValues change
  useEffect(() => {
    if (isOpen) {
      reset(defaultValues || { clientName: "", projectId: 1 });
    }
  }, [isOpen, defaultValues, reset]);

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit((data) => onSubmit(data))}
          className="grid gap-4 py-4"
        >
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="clientName" className="text-right">
              Name
            </Label>
            <div className="col-span-3">
              <Input
                id="clientName"
                placeholder="Enter client name"
                {...register("clientName")}
                className={errors.clientName ? "border-destructive" : ""}
              />
              {errors.clientName && (
                <p className="text-destructive text-sm mt-1">
                  {errors.clientName.message}
                </p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="projectId" className="text-right">
              Project ID
            </Label>
            <div className="col-span-3">
              <Input
                id="projectId"
                type="number"
                {...register("projectId", { valueAsNumber: true })}
                className={errors.projectId ? "border-destructive" : ""}
              />
              {errors.projectId && (
                <p className="text-destructive text-sm mt-1">
                  {errors.projectId.message}
                </p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button type="submit" disabled={isPending}>
              {buttonText}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export default function ClientManagementPage() {
  const [editingClient, setEditingClient] = useState<Client | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);

  const { confirm } = useConfirm();

  // Permission checks
  const canCreate = useAuthorization("Clients:Create");
  const canUpdate = useAuthorization("Clients:Update");
  const canDelete = useAuthorization("Clients:Delete");

  // Fetch clients
  const {
    data: clients,
    isLoading,
    error,
  } = useApi<Client[]>("/Client", {
    queryKey: Array.from(queryKeys.clients.all()),
  });

  // Mutations
  const createClientMutation = useApiPost<Client, CreateClientRequest>(
    "/Client",
    {
      successMessage: "Client created successfully",
      invalidateKeys: [Array.from(queryKeys.clients.all())],
    },
  );

  const updateClientMutation = useApiPut<Client, Client>(`/Client/:clientId`, {
    successMessage: "Client updated successfully",
    invalidateKeys: [Array.from(queryKeys.clients.all())],
  });

  const deleteClientMutation = useApiDelete<void, { clientId: number }>(
    "/Client/:clientId",
    {
      successMessage: "Client deleted successfully",
      invalidateKeys: [Array.from(queryKeys.clients.all())],
    },
  );

  const handleCreate = (data: ClientFormValues) => {
    createClientMutation.mutate(
      {
        clientName: data.clientName,
        projectId: data.projectId,
      },
      {
        onSuccess: () => {
          setIsModalOpen(false);
        },
      }
    );
  };

  const handleUpdate = (data: ClientFormValues) => {
    if (!editingClient) return;

    const updatedClient = {
      ...editingClient,
      clientName: data.clientName,
      projectId: data.projectId,
    };
    updateClientMutation.mutate(updatedClient, {
      onSuccess: () => {
        setIsModalOpen(false);
        setEditingClient(null);
      },
    });
  };

  const handleDelete = async (client: Client) => {
    const confirmed = await confirm({
      title: "Delete Client",
      description: `Are you sure you want to delete "${client.clientName}"? This action cannot be undone.`,
      confirmText: "Delete",
      cancelText: "Cancel",
      variant: "destructive",
    });

    if (confirmed) {
      deleteClientMutation.mutate({ clientId: client.clientId });
    }
  };

  const openEditModal = (client: Client) => {
    setIsEditing(true);
    setEditingClient(client);
    setIsModalOpen(true);
  };

  const openCreateModal = () => {
    setIsEditing(false);
    setEditingClient(null);
    setIsModalOpen(true);
  };

  const handleModalClose = (open: boolean) => {
    if (!open) {
      setIsModalOpen(false);
      setEditingClient(null);
      setIsEditing(false);
    }
  };

  return (
    <LoadingContainer
      isLoading={isLoading}
      skeleton={
        <div className="space-y-6">
          <PageHeaderSkeleton />
          <TableSkeleton rows={5} columns={5} />
        </div>
      }
    >
      {error ? (
        <div className="text-center text-red-600">
          Error loading clients: {error.message}
        </div>
      ) : (
        <div className="space-y-6">
          {/* Header */}
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold tracking-tight">
                Client Management
              </h1>
              <p className="text-muted-foreground mt-1">
                Manage clients and their access to this project
              </p>
            </div>
            {canCreate && (
              <Button onClick={openCreateModal}>
                <Plus className="w-4 h-4 mr-2" />
                Add Client
              </Button>
            )}
          </div>

          {/* Clients Table */}
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {!clients || clients.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="h-24 text-center">
                      <div className="flex flex-col items-center justify-center py-4 text-center">
                        <Users className="w-8 h-8 text-muted-foreground mb-2" />
                        <p className="text-lg font-medium">No clients found</p>
                        <p className="text-sm text-muted-foreground">
                          Get started by adding your first client
                        </p>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  clients.map((client) => (
                    <ClientTableRow
                      key={client.clientId}
                      client={client}
                      canUpdate={canUpdate}
                      canDelete={canDelete}
                      onEdit={openEditModal}
                      onDelete={handleDelete}
                    />
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {/* Unified Modal */}
          <ClientModal
            isOpen={isModalOpen}
            isEditing={isEditing}
            defaultValues={
              editingClient
                ? {
                  clientName: editingClient.clientName,
                  projectId: editingClient.projectId,
                }
                : undefined
            }
            isPending={
              isEditing
                ? updateClientMutation.isPending
                : createClientMutation.isPending
            }
            onSubmit={isEditing ? handleUpdate : handleCreate}
            onClose={handleModalClose}
          />
        </div>
      )}
    </LoadingContainer>
  );
}
