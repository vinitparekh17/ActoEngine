import { useState } from 'react';
import { useApi, useApiPost, useApiPut, useApiDelete, queryKeys } from '../hooks/useApi';
import { useAuthorization } from '../hooks/useAuth';
import { useConfirm } from '../hooks/useConfirm';
import { Button } from '../components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { TableSkeleton, PageHeaderSkeleton, LoadingContainer } from '../components/ui/skeletons';
import { Pencil, Trash2, Plus, Users } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import type { Client, CreateClientRequest } from '../types/client';

export default function ClientManagementPage() {
    const [editingClient, setEditingClient] = useState<Client | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [isEditing, setIsEditing] = useState(false);
    const [formData, setFormData] = useState<CreateClientRequest>({ clientName: '', projectId: 1 });
    const { confirm } = useConfirm();

    // Fetch clients
    const { data: clients, isLoading, error } = useApi<Client[]>('/Client', {
        queryKey: Array.from(queryKeys.clients.all()),
    });

    // Mutations
    const createClientMutation = useApiPost<Client, CreateClientRequest>(
        '/Client',
        {
            successMessage: 'Client created successfully',
            invalidateKeys: [Array.from(queryKeys.clients.all())],
        }
    );

    const updateClientMutation = useApiPut<Client, Client>(
        `/Client/:clientId`,
        {
            successMessage: 'Client updated successfully',
            invalidateKeys: [Array.from(queryKeys.clients.all())],
        }
    );

    const deleteClientMutation = useApiDelete<void, { clientId: number }>(
        '/Client/:clientId',
        {
            successMessage: 'Client deleted successfully',
            invalidateKeys: [Array.from(queryKeys.clients.all())],
        }
    );

    const handleCreate = () => {
        if (!formData.clientName.trim()) return;
        createClientMutation.mutate(formData, {
            onSuccess: () => {
                setIsModalOpen(false);
                setFormData({ clientName: '', projectId: 1 });
            },
        });
    };

    const handleUpdate = () => {
        if (!editingClient || !formData.clientName.trim()) return;
        const updatedClient = { ...editingClient, clientName: formData.clientName, projectId: formData.projectId };
        updateClientMutation.mutate(updatedClient, {
            onSuccess: () => {
                setIsModalOpen(false);
                setEditingClient(null);
                setFormData({ clientName: '', projectId: 1 });
            },
        });
    };

    const handleDelete = async (client: Client) => {
        const confirmed = await confirm({
            title: 'Delete Client',
            description: `Are you sure you want to delete "${client.clientName}"? This action cannot be undone.`,
            confirmText: 'Delete',
            cancelText: 'Cancel',
            variant: 'destructive',
        });

        if (confirmed) {
            deleteClientMutation.mutate(
                { clientId: client.clientId }
            );
        }
    };

    const openEditModal = (client: Client) => {
        setIsEditing(true);
        setEditingClient(client);
        setFormData({ clientName: client.clientName, projectId: client.projectId });
        setIsModalOpen(true);
    };

    const openCreateModal = () => {
        setIsEditing(false);
        setEditingClient(null);
        setFormData({ clientName: '', projectId: 1 });
        setIsModalOpen(true);
    };

    const handleModalClose = (open: boolean) => {
        if (!open) {
            setIsModalOpen(false);
            setEditingClient(null);
            setIsEditing(false);
            setFormData({ clientName: '', projectId: 1 });
        }
    };

    return (
        <LoadingContainer
            isLoading={isLoading}
            skeleton={
                <div className="space-y-6">
                    <PageHeaderSkeleton />
                    <TableSkeleton rows={5} columns={6} />
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
                            <h1 className="text-3xl font-bold tracking-tight">Client Management</h1>
                            <p className="text-muted-foreground mt-1">
                                Manage clients and their access to this project
                            </p>
                        </div>
                        {useAuthorization('Clients:Create') && (
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
                                        <TableRow key={client.clientId}>
                                            <TableCell className="font-medium">
                                                {client.clientName}
                                            </TableCell>
                                            <TableCell>
                                                <code className="relative rounded bg-muted px-[0.3rem] py-[0.2rem] font-mono text-sm">
                                                    {client.clientName}
                                                </code>
                                            </TableCell>
                                            <TableCell>
                                                <Badge variant={client.isActive ? 'default' : 'secondary'}>
                                                    {client.isActive ? 'Active' : 'Inactive'}
                                                </Badge>
                                            </TableCell>
                                            <TableCell>
                                                {new Date(client.createdAt).toLocaleDateString()}
                                            </TableCell>
                                            <TableCell className="text-right">
                                                <div className="flex justify-end gap-2">
                                                    {useAuthorization('Clients:Update') && (
                                                        <Button
                                                            variant="ghost"
                                                            size="icon"
                                                            onClick={() => openEditModal(client)}
                                                        >
                                                            <Pencil className="w-4 h-4" />
                                                        </Button>
                                                    )}
                                                    {useAuthorization('Clients:Delete') && (
                                                        <Button
                                                            variant="ghost"
                                                            size="icon"
                                                            className="text-destructive hover:text-destructive"
                                                            onClick={() => handleDelete(client)}
                                                        >
                                                            <Trash2 className="w-4 h-4" />
                                                        </Button>
                                                    )}
                                                </div>
                                            </TableCell>
                                        </TableRow>
                                    ))
                                )}
                            </TableBody>
                        </Table>
                    </div>

                    {/* Unified Modal */}
                    <Dialog open={isModalOpen} onOpenChange={handleModalClose}>
                        <DialogContent className="sm:max-w-[425px]">
                            <DialogHeader>
                                <DialogTitle>{isEditing ? 'Edit Client' : 'Create New Client'}</DialogTitle>
                                <DialogDescription>
                                    {isEditing
                                        ? 'Update the client details below and click “Update Client” to save changes.'
                                        : 'Enter the details below and click “Create Client” to add a new client.'
                                    }
                                </DialogDescription>
                            </DialogHeader>

                            <div className="grid gap-4 py-4">
                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="clientName" className="text-right">
                                        Name
                                    </Label>
                                    <Input
                                        id="clientName"
                                        value={formData.clientName}
                                        onChange={(e) => setFormData({ ...formData, clientName: e.target.value })}
                                        placeholder="Enter client name"
                                        className="col-span-3"
                                    />
                                </div>

                                <div className="grid grid-cols-4 items-center gap-4">
                                    <Label htmlFor="projectId" className="text-right">
                                        Project ID
                                    </Label>
                                    <Input
                                        id="projectId"
                                        type="number"
                                        value={formData.projectId}
                                        onChange={(e) => setFormData({ ...formData, projectId: parseInt(e.target.value, 10) })}
                                        className="col-span-3"
                                    />
                                </div>
                            </div>

                            <DialogFooter>
                                <Button onClick={isEditing ? handleUpdate : handleCreate} disabled={isEditing ? updateClientMutation.isPending : createClientMutation.isPending}>
                                    {isEditing
                                        ? (updateClientMutation.isPending ? 'Updating...' : 'Update Client')
                                        : (createClientMutation.isPending ? 'Creating...' : 'Create Client')
                                    }
                                </Button>
                            </DialogFooter>
                        </DialogContent>
                    </Dialog>
                </div>
            )}
        </LoadingContainer>
    );
}