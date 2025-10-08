import { useState } from 'react';
import { useFormBuilder } from '../hooks/form=builder/useFormBuilder';
import { useProject } from '../hooks/useProject';
import {
    Plus,
    Save,
    Code,
    Eye,
    Upload,
    Trash2,
    GripVertical,
    ChevronDown,
    ChevronRight,
} from 'lucide-react';
import { toast } from 'sonner';
import { useConfirm } from '../hooks/useConfirm';

export default function FormBuilderPage() {
    const { selectedProject, hasProject } = useProject();
    const {
        config,
        isDirty,
        isSaving,
        isGenerating,
        initializeForm,
        saveConfig,
        generateForm,
        generatedCode,
        deleteGroup,
    } = useFormBuilder();

    const { confirm } = useConfirm();

    const [showInitModal, setShowInitModal] = useState(!config);
    const [activeTab, setActiveTab] = useState<'builder' | 'preview' | 'code'>('builder');

    // Require project selection
    if (!hasProject) {
        return (
            <div className="flex min-h-[600px] items-center justify-center">
                <div className="text-center">
                    <h2 className="text-xl font-semibold text-gray-900">No Project Selected</h2>
                    <p className="mt-2 text-gray-600">
                        Please select a project to use the Form Builder
                    </p>
                </div>
            </div>
        );
    }

    // Pass handleDeleteGroup as a prop to GroupCanvas
    const handleDeleteGroup = (groupId: string) => {
        confirm({
            title: 'Delete this section?',
            description: 'Are you sure you want to delete this section? This action cannot be undone.',
            confirmText: 'Delete',
            cancelText: 'Cancel',
            variant: 'destructive',
        }).then((confirmed) => {
            if (confirmed) {
                deleteGroup(groupId);
            }
        });
    };

    return (
        <div className="flex h-[calc(100vh-120px)] flex-col">
            {/* Header */}
            <div className="border-b bg-white px-6 py-4">
                <div className="flex items-center justify-between">
                    {/* Left: Title */}
                    <div>
                        <h1 className="text-2xl font-bold text-gray-900">
                            {config ? config.formName : 'Form Builder'}
                        </h1>
                        {config && (
                            <p className="text-sm text-gray-600">
                                Table: {config.tableName} • {config.groups.reduce((acc, g) => acc + g.fields.length, 0)} fields
                                {isDirty && <span className="ml-2 text-orange-600">• Unsaved changes</span>}
                            </p>
                        )}
                    </div>

                    {/* Right: Actions */}
                    <div className="flex items-center gap-2">
                        {/* Tabs */}
                        <div className="flex items-center gap-1 rounded-lg border p-1">
                            <button
                                onClick={() => setActiveTab('builder')}
                                className={`rounded px-3 py-1.5 text-sm font-medium transition-colors ${activeTab === 'builder'
                                        ? 'bg-blue-600 text-white'
                                        : 'text-gray-600 hover:bg-gray-100'
                                    }`}
                            >
                                Builder
                            </button>
                            <button
                                onClick={() => setActiveTab('preview')}
                                className={`rounded px-3 py-1.5 text-sm font-medium transition-colors ${activeTab === 'preview'
                                        ? 'bg-blue-600 text-white'
                                        : 'text-gray-600 hover:bg-gray-100'
                                    }`}
                            >
                                <Eye className="inline h-4 w-4 mr-1" />
                                Preview
                            </button>
                            <button
                                onClick={() => setActiveTab('code')}
                                className={`rounded px-3 py-1.5 text-sm font-medium transition-colors ${activeTab === 'code'
                                        ? 'bg-blue-600 text-white'
                                        : 'text-gray-600 hover:bg-gray-100'
                                    }`}
                                disabled={!generatedCode}
                            >
                                <Code className="inline h-4 w-4 mr-1" />
                                Code
                            </button>
                        </div>

                        {/* Action Buttons */}
                        {config && (
                            <>
                                <button
                                    onClick={() => saveConfig()}
                                    disabled={!isDirty || isSaving}
                                    className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                                >
                                    <Save className="h-4 w-4" />
                                    {isSaving ? 'Saving...' : 'Save'}
                                </button>

                                <button
                                    onClick={() => generateForm(false)}
                                    disabled={isGenerating}
                                    className="flex items-center gap-2 rounded-lg border border-blue-600 px-4 py-2 text-sm font-medium text-blue-600 hover:bg-blue-50 disabled:opacity-50"
                                >
                                    <Code className="h-4 w-4" />
                                    {isGenerating ? 'Generating...' : 'Generate Code'}
                                </button>
                            </>
                        )}
                    </div>
                </div>
            </div>

            {/* Main Content */}
            <div className="flex flex-1 overflow-hidden">
                {activeTab === 'builder' && config && (
                    <BuilderView config={config} handleDeleteGroup={handleDeleteGroup} />
                )}

                {activeTab === 'preview' && config && (
                    <PreviewView config={config} />
                )}

                {activeTab === 'code' && generatedCode && (
                    <CodeView generatedCode={generatedCode} />
                )}
            </div>

            {/* Initialize Form Modal */}
            {showInitModal && (
                <InitializeFormModal
                    onClose={() => setShowInitModal(false)}
                    onInitialize={(tableName) => {
                        initializeForm(tableName, selectedProject!.projectId);
                        setShowInitModal(false);
                    }}
                />
            )}
        </div>
    );
}

// ============================================
// Builder View Component
// ============================================
function BuilderView({ config }: { config: any, handleDeleteGroup: (groupId: string) => void }) {
    const {
        selectedGroup,
        selectedField,
    } = useFormBuilder();

    return (
        <div className="flex flex-1 overflow-hidden">
            {/* Left: Toolbox */}
            <div className="w-64 border-r bg-gray-50 p-4 overflow-y-auto">
                <FieldToolbox />
            </div>

            {/* Center: Canvas */}
            <div className="flex-1 overflow-y-auto p-6">
                <FormCanvas />
            </div>

            {/* Right: Properties Panel */}
            <div className="w-80 border-l bg-white p-4 overflow-y-auto">
                {selectedField ? (
                    <FieldPropertiesPanel field={selectedField} />
                ) : selectedGroup ? (
                    <GroupPropertiesPanel group={selectedGroup} />
                ) : (
                    <FormSettingsPanel config={config} />
                )}
            </div>
        </div>
    );
}

// ============================================
// Field Toolbox Component
// ============================================
import { INPUT_TYPE_METADATA } from '../hooks/form=builder/formBuilder.types';

function FieldToolbox() {
    const { addField } = useFormBuilder();

    const fieldTypes = Object.entries(INPUT_TYPE_METADATA);

    return (
        <div className="space-y-4">
            <div>
                <h3 className="text-sm font-semibold text-gray-900 mb-2">Field Types</h3>
                <p className="text-xs text-gray-600 mb-3">
                    Click to add a field to the form
                </p>
            </div>

            <div className="space-y-2">
                {fieldTypes.map(([type, meta]) => (
                    <button
                        key={type}
                        onClick={() => {
                            addField({
                                inputType: type as any,
                                label: meta.label,
                                colSize: meta.defaultColSize,
                            });
                        }}
                        className="flex w-full items-center gap-3 rounded-lg border bg-white p-3 text-left hover:border-blue-500 hover:bg-blue-50 transition-colors"
                    >
                        <span className="text-2xl"><meta.icon /></span>
                        <span className="text-sm font-medium text-gray-900">
                            {meta.label}
                        </span>
                    </button>
                ))}
            </div>

            <div className="pt-4 border-t">
                <button
                    onClick={() => {
                        // TODO: Import from schema modal
                        toast.info('Schema import coming soon');
                    }}
                    className="flex w-full items-center justify-center gap-2 rounded-lg border-2 border-dashed border-gray-300 p-3 text-sm font-medium text-gray-600 hover:border-blue-500 hover:text-blue-600"
                >
                    <Upload className="h-4 w-4" />
                    Import from Schema
                </button>
            </div>
        </div>
    );
}

// ============================================
// Form Canvas Component
// ============================================
function FormCanvas() {
    const { config, addGroup, deleteGroup } = useFormBuilder();

    if (!config) return null;

    return (
        <div className="space-y-6">
            {config.groups.map((group, idx) => (
                <GroupCanvas key={group.id} group={group} handleDeleteGroup={deleteGroup} index={idx} />
            ))}

            {/* Add Group Button */}
            <button
                onClick={() => addGroup()}
                className="flex w-full items-center justify-center gap-2 rounded-xl border-2 border-dashed border-gray-300 p-6 text-sm font-medium text-gray-600 hover:border-blue-500 hover:bg-blue-50 hover:text-blue-600 transition-colors"
            >
                <Plus className="h-5 w-5" />
                Add Section
            </button>
        </div>
    );
}

// ============================================
// Group Canvas Component
// ============================================
function GroupCanvas({ group, index, handleDeleteGroup }: { group: any; index: number; handleDeleteGroup: (groupId: string) => void }) {
    const { selectGroup, selectedGroup } = useFormBuilder();
    const [collapsed, setCollapsed] = useState(false);
    const isSelected = selectedGroup?.id === group.id;

    return (
        <div
            className={`rounded-xl border-2 bg-white transition-colors ${isSelected ? 'border-blue-500 ring-2 ring-blue-200' : 'border-gray-200'
                }`}
        >
            {/* Group Header */}
            <div
                className="flex items-center justify-between border-b p-4 cursor-pointer hover:bg-gray-50"
                onClick={() => selectGroup(group.id)}
            >
                <div className="flex items-center gap-3">
                    <button
                        onClick={(e) => {
                            e.stopPropagation();
                            setCollapsed(!collapsed);
                        }}
                        className="text-gray-400 hover:text-gray-600"
                    >
                        {collapsed ? (
                            <ChevronRight className="h-5 w-5" />
                        ) : (
                            <ChevronDown className="h-5 w-5" />
                        )}
                    </button>
                    <GripVertical className="h-5 w-5 text-gray-400" />
                    <div>
                        <h3 className="font-semibold text-gray-900">
                            {group.title || `Section ${index + 1}`}
                        </h3>
                        <p className="text-xs text-gray-600">
                            {group.fields.length} field{group.fields.length !== 1 ? 's' : ''}
                        </p>
                    </div>
                </div>

                <button
                    onClick={(e) => {
                        e.stopPropagation();
                        handleDeleteGroup(group.id);
                    }}
                    className="text-gray-400 hover:text-red-600"
                >
                    <Trash2 className="h-4 w-4" />
                </button>
            </div>

            {/* Group Content */}
            {!collapsed && (
                <div className="p-4">
                    {group.fields.length === 0 ? (
                        <div className="rounded-lg border-2 border-dashed border-gray-200 p-8 text-center text-sm text-gray-500">
                            No fields yet. Click a field type from the toolbox to add one.
                        </div>
                    ) : (
                        <div
                            className="grid gap-4"
                            style={{
                                gridTemplateColumns: `repeat(${12 / (group.fields[0]?.colSize || 12)}, 1fr)`
                            }}
                        >
                            {group.fields.map((field: Field) => (
                                <FieldCard key={field.id} field={field} />
                            ))}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

// Define a type for the field object
interface Field {
    id: string;
    colSize: number;
    label?: string;
    columnName?: string;
    inputType: string;
    required?: boolean;
}

// ============================================
// Field Card Component
// ============================================
function FieldCard({ field }: { field: any }) {
    const { selectField, selectedField, deleteField } = useFormBuilder();
    const { confirm } = useConfirm();
    const isSelected = selectedField?.id === field.id;

    return (
        <div
            onClick={() => selectField(field.id)}
            className={`rounded-lg border-2 p-3 cursor-pointer transition-colors ${isSelected
                    ? 'border-blue-500 bg-blue-50'
                    : 'border-gray-200 hover:border-gray-300 hover:bg-gray-50'
                }`}
            style={{ gridColumn: `span ${field.colSize}` }}
        >
            <div className="flex items-start justify-between gap-2">
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                        <GripVertical className="h-4 w-4 text-gray-400 flex-shrink-0" />
                        <span className="text-xs font-medium text-gray-600 truncate">
                            {field.label || field.columnName}
                        </span>
                    </div>
                    <div className="text-xs text-gray-500">
                        {field.inputType}
                        {field.required && <span className="text-red-500 ml-1">*</span>}
                    </div>
                </div>
                <button
                    onClick={async (e) => {
                        e.stopPropagation();
                        const confirmed = await confirm({
                            title: 'Delete Field',
                            description: 'Are you sure you want to delete this field?',
                            confirmText: 'Delete',
                            cancelText: 'Cancel',
                            variant: 'destructive',
                        });
                        if (confirmed) {
                            deleteField(field.id);
                        }
                    }}
                    className="text-gray-400 hover:text-red-600 flex-shrink-0"
                >
                    <Trash2 className="h-3.5 w-3.5" />
                </button>
            </div>
        </div>
    );
}

// ============================================
// Properties Panels (Placeholders)
// ============================================
function FieldPropertiesPanel({ field }: { field: any }) {
    return (
        <div>
            <h3 className="text-lg font-semibold mb-4">Field Properties</h3>
            <p className="text-sm text-gray-600">Selected: {field.label}</p>
            {/* TODO: Full properties form */}
        </div>
    );
}

function GroupPropertiesPanel({ group }: { group: any }) {
    return (
        <div>
            <h3 className="text-lg font-semibold mb-4">Section Properties</h3>
            <p className="text-sm text-gray-600">Selected: {group.title}</p>
            {/* TODO: Group properties form */}
        </div>
    );
}

function FormSettingsPanel({ config }: { config: any }) {
    return (
        <div>
            <h3 className="text-lg font-semibold mb-4">Form Settings</h3>
            <p className="text-sm text-gray-600">{config.formName}</p>
            {/* TODO: Form options */}
        </div>
    );
}

// ============================================
// Preview & Code Views (Placeholders)
// ============================================
function PreviewView({ config }: { config: any }) {
    return (
        <div className="flex-1 p-6 overflow-y-auto">
            <div className="max-w-4xl mx-auto rounded-xl border bg-white p-8">
                <h2 className="text-2xl font-bold mb-6">{config.title}</h2>
                <p className="text-gray-600">Form preview coming soon...</p>
            </div>
        </div>
    );
}

function CodeView({ generatedCode }: { generatedCode: any }) {
    return (
        <div className="flex-1 p-6 overflow-y-auto">
            <div className="space-y-4">
                <h2 className="text-xl font-bold">Generated Code</h2>
                <pre className="rounded-lg bg-gray-900 p-4 text-sm text-gray-100 overflow-x-auto">
                    {generatedCode.html}
                </pre>
            </div>
        </div>
    );
}

// ============================================
// Initialize Form Modal
// ============================================
function InitializeFormModal({
    onClose,
    onInitialize
}: {
    onClose: () => void;
    onInitialize: (tableName: string) => void;
}) {
    //   const { tables } = useProject();
    const tables = ['users', 'products', 'orders']; // Placeholder tables
    const [selectedTable, setSelectedTable] = useState('');

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
            <div className="w-full max-w-md rounded-xl bg-white p-6">
                <h2 className="text-xl font-bold mb-4">Create New Form</h2>

                <div className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium mb-2">Select Table</label>
                        <select
                            value={selectedTable}
                            onChange={(e) => setSelectedTable(e.target.value)}
                            className="w-full rounded-lg border px-3 py-2"
                        >
                            <option value="">Choose a table...</option>
                            {tables?.map((table: any) => (
                                <option key={table} value={table}>
                                    {table}
                                </option>
                            ))}
                        </select>
                    </div>

                    <div className="flex gap-2">
                        <button
                            onClick={onClose}
                            className="flex-1 rounded-lg border px-4 py-2 font-medium hover:bg-gray-50"
                        >
                            Cancel
                        </button>
                        <button
                            onClick={() => selectedTable && onInitialize(selectedTable)}
                            disabled={!selectedTable}
                            className="flex-1 rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                        >
                            Create
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}