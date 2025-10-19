import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { useApiPost } from './useApi';
import { toast } from 'sonner';
import { nanoid } from 'nanoid';

// ============================================
// Types of Inputs and Form Structures
// ============================================

export type InputType =
  | 'text' | 'number' | 'email' | 'date' | 'select' | 'textarea' | 'checkbox' | 'password' | 'radio' | 'file' | 'hidden';

export interface FormField {
  id: string;
  columnName: string;
  dataType: string;
  label: string;
  inputType: InputType;
  placeholder?: string;
  defaultValue?: string;
  helpText?: string;
  colSize: number;
  order: number;
  required: boolean;
  minLength?: number;
  maxLength?: number;
  min?: number;
  max?: number;
  pattern?: string;
  errorMessage?: string;
  includeInInsert: boolean;
  includeInUpdate: boolean;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  isForeignKey: boolean;
  options?: SelectOption[];
  disabled: boolean;
  readonly: boolean;
}

export interface SelectOption {
  label: string;
  value: string;
}

export interface FormGroup {
  id: string;
  title?: string;
  description?: string;
  fields: FormField[];
  layout: string;
  order: number;
  collapsible: boolean;
  collapsed: boolean;
}

export interface FormGenerationOptions {
  bootstrapVersion: string;
  formStyle: string;
  labelPosition: string;
  jsFramework: string;
  validationStyle: string;
  generateGrid: boolean;
  gridType: string;
  spPrefix: string;
  useModal: boolean;
  modalSize: string;
  includePermissionChecks: boolean;
  includeErrorHandling: boolean;
}

export interface FormConfig {
  id?: string;
  projectId: number;
  tableName: string;
  formName: string;
  title: string;
  description?: string;
  groups: FormGroup[];
  options?: FormGenerationOptions;
  createdAt?: string;
  updatedAt?: string;
}

export interface GeneratedCode {
  success: boolean;
  warnings?: string[];
  html: string;
  javaScript: string;
  storedProcedures?: string[];
  fileName?: string;
  message?: string;
}

// ============================================
// Zustand Store for Form Builder State Management
// ============================================

interface FormBuilderStore {
  config: FormConfig | null;
  selectedFieldId: string | null;
  selectedGroupId: string | null;
  generatedCode: GeneratedCode | null;

  setConfig: (config: FormConfig) => void;
  addGroup: (group?: Partial<FormGroup>) => void;
  updateGroup: (groupId: string, updates: Partial<FormGroup>) => void;
  deleteGroup: (groupId: string) => void;
  addField: (groupId: string, field: Partial<FormField>) => void;
  updateField: (fieldId: string, updates: Partial<FormField>) => void;
  deleteField: (fieldId: string) => void;
  selectField: (fieldId: string | null) => void;
  selectGroup: (groupId: string | null) => void;
  setGeneratedCode: (code: GeneratedCode | null) => void;
  reset: () => void;
}

const useFormBuilderStore = create<FormBuilderStore>()(
  persist(
    (set) => ({
      config: null,
      selectedFieldId: null,
      selectedGroupId: null,
      generatedCode: null,

      setConfig: (config) => set({ config }),

      addGroup: (group) => set((state) => {
        if (!state.config) return state;

        const newGroup: FormGroup = {
          id: `group-${nanoid(6)}`,
          title: group?.title || 'New Group',
          description: group?.description,
          fields: [],
          layout: group?.layout || 'row',
          order: state.config.groups.length,
          collapsible: group?.collapsible || false,
          collapsed: group?.collapsed || false,
        };

        return {
          config: {
            ...state.config,
            groups: [...state.config.groups, newGroup],
          },
          selectedGroupId: newGroup.id,
        };
      }),

      updateGroup: (groupId, updates) => set((state) => {
        if (!state.config) return state;

        return {
          config: {
            ...state.config,
            groups: state.config.groups.map(g =>
              g.id === groupId ? { ...g, ...updates } : g
            ),
          },
        };
      }),

      deleteGroup: (groupId) => set((state) => {
        if (!state.config) return state;

        return {
          config: {
            ...state.config,
            groups: state.config.groups.filter(g => g.id !== groupId),
          },
          selectedGroupId: state.selectedGroupId === groupId ? null : state.selectedGroupId,
        };
      }),

      addField: (groupId, field) => set((state) => {
        if (!state.config) return state;

        const newField: FormField = {
          id: `field-${nanoid(6)}`,
          columnName: field.columnName || '',
          dataType: field.dataType || 'NVARCHAR',
          label: field.label || '',
          inputType: field.inputType || 'text',
          placeholder: field.placeholder,
          defaultValue: field.defaultValue,
          helpText: field.helpText,
          colSize: field.colSize || 12,
          order: 0, // Will be set below
          required: field.required || false,
          minLength: field.minLength,
          maxLength: field.maxLength,
          min: field.min,
          max: field.max,
          pattern: field.pattern,
          errorMessage: field.errorMessage,
          includeInInsert: field.includeInInsert ?? true,
          includeInUpdate: field.includeInUpdate ?? true,
          isPrimaryKey: field.isPrimaryKey || false,
          isIdentity: field.isIdentity || false,
          isForeignKey: field.isForeignKey || false,
          options: field.options,
          disabled: field.disabled || false,
          readonly: field.readonly || false,
        };

        return {
          config: {
            ...state.config,
            groups: state.config.groups.map(g => {
              if (g.id === groupId) {
                newField.order = g.fields.length;
                return {
                  ...g,
                  fields: [...g.fields, newField],
                };
              }
              return g;
            }),
          },
          selectedFieldId: newField.id,
        };
      }),

      updateField: (fieldId, updates) => set((state) => {
        if (!state.config) return state;

        return {
          config: {
            ...state.config,
            groups: state.config.groups.map(g => ({
              ...g,
              fields: g.fields.map(f =>
                f.id === fieldId ? { ...f, ...updates } : f
              ),
            })),
          },
        };
      }),

      deleteField: (fieldId) => set((state) => {
        if (!state.config) return state;

        return {
          config: {
            ...state.config,
            groups: state.config.groups.map(g => ({
              ...g,
              fields: g.fields.filter(f => f.id !== fieldId),
            })),
          },
          selectedFieldId: state.selectedFieldId === fieldId ? null : state.selectedFieldId,
        };
      }),

      selectField: (fieldId) => set({ selectedFieldId: fieldId }),
      selectGroup: (groupId) => set({ selectedGroupId: groupId }),
      setGeneratedCode: (code) => set({ generatedCode: code }),
      reset: () => set({ config: null, selectedFieldId: null, selectedGroupId: null, generatedCode: null }),
    }),
    {
      name: 'actox-form-builder',
      version: 1,
      partialize: (state) => ({
        selectedFieldId: state.selectedFieldId,
        selectedGroupId: state.selectedGroupId,
        config: state.config,
        generatedCode: state.generatedCode,
      }),
      migrate: (persistedState: any, version: number) => {
        if (version === 0) {
          return {
            selectedFieldId: persistedState.selectedFieldId || null,
            selectedGroupId: persistedState.selectedGroupId || null,
            config: persistedState.config || {},
            generatedCode: persistedState.generatedCode || '',
          };
        }
        return persistedState as FormBuilderStore;
      },
    }
  )
);

// ============================================
// Query Keys (Structured)
// ============================================

export const formBuilderQueryKeys = {
  all: () => ['formbuilder'] as const,
  configs: (projectId: number) => ['formbuilder', 'configs', projectId] as const,
  generated: (configId: string) => ['formbuilder', 'generated', configId] as const,
};

// ============================================
// Main Hook
// ============================================

export function useFormBuilder() {
  const store = useFormBuilderStore();

  // Save form config
  const saveConfigMutation = useApiPost<{ success: boolean; formId: number; config: FormConfig }, { projectId: number; config: FormConfig; description?: string }>(
    '/formbuilder/save',
    {
      onSuccess: (data) => {
        if (data.formId && store.config) {
          store.setConfig({
            ...store.config,
            id: data.formId.toString()
          });
        }
      }
    });

  // Generate code
  const generateCodeMutation = useApiPost<GeneratedCode, { config: FormConfig; generateStoredProcedures?: boolean; preview?: boolean }>(
    '/formbuilder/generate',
    {
      onSuccess: (data) => {
        const { success, message, warnings } = data || {};
        if (success) {
          store.setGeneratedCode(data);

          // show success or partial-success notification
          if (warnings?.length) {
            toast.warning(`Code generated with warnings: ${warnings.join(', ')}`);
          } else {
            toast.success(message || 'Code generated successfully!');
          }
        } else {
          // handle soft failure (validation, logical error)
          toast.error(message || 'Failed to generate code. Please review inputs.');
        }
      },
      onError: (error) => {
        console.error(error);
        toast.error('Server error while generating code.');
      },
    }
  );

  // Initialize a new form
  const initializeForm = (tableName: string, projectId: number) => {
    const config: FormConfig = {
      id: `form-${nanoid(6)}`,
      projectId,
      tableName,
      formName: `${tableName}Form`,
      title: `${tableName} Form`,
      groups: [],
    };

    store.setConfig(config);
    // Add a default group
    store.addGroup({ title: 'Main Fields' });
    return config;
  };

  // Import fields from schema (simplified)
  const importFromSchema = (columns: any[]) => {
    if (!store.config || !Array.isArray(store.config.groups) || store.config.groups.length === 0) {
      toast.error('No groups defined — please create a group in the form first');
      return;
    }

    const firstGroupId = store.config.groups[0].id;
    const fields = columns
      .filter(col => !col.isIdentity) // Skip identity columns
      .map(col => ({
        columnName: col.columnName,
        dataType: col.dataType,
        label: formatLabel(col.columnName),
        inputType: mapSqlTypeToInput(col.dataType),
        required: !col.isNullable,
        colSize: 12,
        includeInInsert: true,
        includeInUpdate: true,
        isPrimaryKey: col.isPrimaryKey || false,
        isIdentity: col.isIdentity || false,
        isForeignKey: col.isForeignKey || false,
      }));

    if (fields.length === 0) {
      toast.info('No non-identity columns to import');
      return;
    }

    fields.forEach(field => store.addField(firstGroupId, field));
    toast.success(`Imported ${fields.length} fields`);
  };
  // Get selected field
  const selectedField = store.config?.groups
    .flatMap(g => g.fields)
    .find(f => f.id === store.selectedFieldId) || null;

  // Get all fields (flattened from all groups)
  const allFields = store.config?.groups.flatMap(g => g.fields) || [];

  return {
    // State
    config: store.config,
    groups: store.config?.groups || [],
    fields: allFields,
    selectedField,
    selectedGroup: store.config?.groups.find(g => g.id === store.selectedGroupId) || null,
    generatedCode: store.generatedCode,

    // Loading states
    isSaving: saveConfigMutation.isPending,
    isGenerating: generateCodeMutation.isPending,

    // Actions
    initializeForm,
    importFromSchema,
    saveConfig: () => {
      if (!store.config) return;
      saveConfigMutation.mutate({
        projectId: store.config.projectId,
        config: store.config
      });
    },
    generateCode: () => {
      if (!store.config) return;
      generateCodeMutation.mutate({
        config: store.config
      });
    },

    // Group operations
    addGroup: store.addGroup,
    updateGroup: store.updateGroup,
    deleteGroup: store.deleteGroup,
    selectGroup: store.selectGroup,

    // Field operations
    addField: (field: Partial<FormField>) => {
      const groupId = store.selectedGroupId || store.config?.groups[0]?.id;
      if (groupId) {
        store.addField(groupId, field);
      }
    },
    updateField: store.updateField,
    deleteField: store.deleteField,
    selectField: store.selectField,

    // Reset
    reset: store.reset,
  };
}

// ============================================
// Utility Functions
// ============================================

function formatLabel(columnName: string): string {
  return columnName
    .replace(/([A-Z])/g, ' $1')
    .replace(/[_-]/g, ' ')
    .trim()
    .split(' ')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ');
}

function mapSqlTypeToInput(sqlType: string): InputType {
  const type = sqlType.toUpperCase();

  if (type.includes('INT') || type.includes('DECIMAL')) return 'number';
  if (type.includes('DATE')) return 'date';
  if (type.includes('BIT')) return 'checkbox';
  if (type.includes('TEXT') || type.includes('NVARCHAR(MAX)')) return 'textarea';

  return 'text';
}