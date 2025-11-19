import { useState } from 'react';
import { useApi, useApiMutation, queryKeys } from '../hooks/useApi';
import { useConfirm } from '../hooks/useConfirm';
import { Button } from '../components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '../components/ui/dialog';
import { TableSkeleton, PageHeaderSkeleton, LoadingContainer } from '../components/ui/skeletons';
import { Pencil, Trash2, Plus } from 'lucide-react';
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
    const createClientMutation = useApiMutation<Client, CreateClientRequest>(
        '/Client',
        'POST',
        {
            successMessage: 'Client created successfully',
            invalidateKeys: [Array.from(queryKeys.clients.all())],
        }
    );

    const updateClientMutation = useApiMutation<Client, Client>(
        `/Client/:clientId`,
        'PUT',
        {
            successMessage: 'Client updated successfully',
            invalidateKeys: [Array.from(queryKeys.clients.all())],
        }
    );

    const deleteClientMutation = useApiMutation<void, { clientId: number }>(
        '/Client/:clientId',
        'DELETE',
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
                    <div className="flex justify-between items-center">
                        <div>
                            <h1 className="text-3xl font-bold text-foreground">Client Master</h1>
                        </div>

                        {/* Create Client Button */}
                        <Button onClick={openCreateModal}>
                            <Plus className="h-4 w-4 mr-2" />
                            Add Client
                        </Button>
                    </div>

                    {/* Clients Table */}
                    <div className="rounded-2xl border bg-background dark:bg-dark-500 shadow-sm overflow-hidden">
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHead>Client ID</TableHead>
                                    <TableHead>Client Name</TableHead>
                                    <TableHead>Project ID</TableHead>
                                    <TableHead>Active</TableHead>
                                    <TableHead>Created At</TableHead>
                                    <TableHead>Actions</TableHead>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {clients?.map((client) => (
                                    <TableRow key={client.clientId}>
                                        <TableCell>{client.clientId}</TableCell>
                                        <TableCell>{client.clientName}</TableCell>
                                        <TableCell>{client.projectId}</TableCell>
                                        <TableCell>
                                            <span className={`px-2 py-1 rounded-full text-xs ${client.isActive
                                                ? 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200'
                                                : 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200'
                                                }`}>
                                                {client.isActive ? 'Active' : 'Inactive'}
                                            </span>
                                        </TableCell>
                                        <TableCell>{new Date(client.createdAt).toLocaleDateString()}</TableCell>
                                        <TableCell>
                                            <div className="flex gap-2">
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => openEditModal(client)}
                                                >
                                                    <Pencil className="h-4 w-4" />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    onClick={() => handleDelete(client)}
                                                    className="text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
                                                >
                                                    <Trash2 className="h-4 w-4" />
                                                </Button>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))}
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