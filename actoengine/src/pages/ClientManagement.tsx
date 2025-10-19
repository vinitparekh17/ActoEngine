import { useState } from 'react';
import { useApi, useApiMutation, queryKeys } from '../hooks/useApi';
import { useConfirm } from '../hooks/useConfirm';
import { Button } from '../components/ui/button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '../components/ui/dialog';
import { TableSkeleton, PageHeaderSkeleton, LoadingContainer } from '../components/ui/skeletons';
import { Pencil, Trash2, Plus } from 'lucide-react';

// ============================================
// Types
// ============================================
export interface Client {
    clientId: number;
    clientName: string;
    projectId: number;
    isActive: boolean;
    createdAt: string;
    createdBy: number;
    updatedAt: string | null;
    updatedBy: number | null;
}

export interface CreateClientRequest {
    clientName: string;
    projectId: number;
}

export default function ClientManagementPage() {
    const [editingClient, setEditingClient] = useState<Client | null>(null);
    const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
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

    const deleteClientMutation = useApiMutation<void, { clientId: number; projectId: number }>(
        '/Client/:clientId/project/:projectId',
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
                setIsCreateModalOpen(false);
                setFormData({ clientName: '', projectId: 1 });
            },
        });
    };

    const handleUpdate = () => {
        if (!editingClient || !formData.clientName.trim()) return;
        const updatedClient = { ...editingClient, clientName: formData.clientName, projectId: formData.projectId };
        updateClientMutation.mutate(updatedClient, {
            onSuccess: () => {
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
                { clientId: client.clientId, projectId: client.projectId }
            );
        }
    };

    const openEditModal = (client: Client) => {
        setEditingClient(client);
        setFormData({ clientName: client.clientName, projectId: client.projectId });
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
                        <Dialog open={isCreateModalOpen} onOpenChange={(open) => {
                            setIsCreateModalOpen(open);
                            if (open) {
                                // Reset form when opening create modal
                                setFormData({ clientName: '', projectId: 1 });
                            }
                        }}>
                            <DialogTrigger asChild>
                                <Button>
                                    <Plus className="h-4 w-4 mr-2" />
                                    Add Client
                                </Button>
                            </DialogTrigger>
                            <DialogContent>
                                <DialogHeader>
                                    <DialogTitle>Create New Client</DialogTitle>
                                </DialogHeader>
                                <div className="space-y-4">
                                    <div>
                                        <Label htmlFor="clientName">Client Name</Label>
                                        <Input
                                            id="clientName"
                                            value={formData.clientName}
                                            onChange={(e) => setFormData({ ...formData, clientName: e.target.value })}
                                            placeholder="Enter client name"
                                        />
                                    </div>
                                    <div>
                                        <Label htmlFor="projectId">Project ID</Label>
                                        <Input
                                            id="projectId"
                                            type="number"
                                            value={formData.projectId}
                                            onChange={(e) => setFormData({ ...formData, projectId: parseInt(e.target.value, 10) })}
                                        />
                                    </div>
                                    <Button onClick={handleCreate} disabled={createClientMutation.isPending}>
                                        {createClientMutation.isPending ? 'Creating...' : 'Create Client'}
                                    </Button>
                                </div>
                            </DialogContent>
                        </Dialog>
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

                    {/* Edit Modal */}
                    <Dialog open={!!editingClient} onOpenChange={(open) => !open && setEditingClient(null)}>
                        <DialogContent>
                            <DialogHeader>
                                <DialogTitle>Edit Client</DialogTitle>
                            </DialogHeader>
                            <div className="space-y-4">
                                <div>
                                    <Label htmlFor="editClientName">Client Name</Label>
                                    <Input
                                        id="editClientName"
                                        value={formData.clientName}
                                        onChange={(e) => setFormData({ ...formData, clientName: e.target.value })}
                                        placeholder="Enter client name"
                                    />
                                </div>
                                <div>
                                    <Label htmlFor="editProjectId">Project ID</Label>
                                    <Input
                                        id="editProjectId"
                                        type="number"
                                        value={formData.projectId}
                                        onChange={(e) => setFormData({ ...formData, projectId: parseInt(e.target.value, 10) })}
                                    />
                                </div>
                                <Button onClick={handleUpdate} disabled={updateClientMutation.isPending}>
                                    {updateClientMutation.isPending ? 'Updating...' : 'Update Client'}
                                </Button>
                            </div>
                        </DialogContent>
                    </Dialog>
                </div>
            )}
        </LoadingContainer>
    );
}