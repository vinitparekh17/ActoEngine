import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { useApi, useApiPost } from '../useApi';
import { useProject, useTableSchema } from '../useProject';
import { useConfirm } from '../useConfirm';
import {
  type FormConfig,
  type FormField,
  type FormGroup,
  type GeneratedFormCode,
  type SaveFormConfigRequest,
  type GenerateFormRequest,
  DEFAULT_FORM_OPTIONS,
  DEFAULT_FIELD,
  type FormConfigListResponse,
  type InputType,
} from './formBuilder.types';
import { nanoid } from 'nanoid';
import { toast } from 'sonner';
import { useMemo } from 'react';
import { produce } from 'immer';

// ============================================
// Zustand Store (Form Builder State)
// ============================================
interface FormBuilderStore {
  config: FormConfig | null;
  selectedFieldId: string | null;
  selectedGroupId: string | null;
  isDirty: boolean;
  generatedCode: GeneratedFormCode | null;

  // Actions
  setConfig: (config: FormConfig) => void;
  updateConfig: (updates: Partial<FormConfig>) => void;
  updateGroups: (updater: (draft: FormGroup[]) => void) => void;
  selectField: (fieldId: string | null) => void;
  selectGroup: (groupId: string | null) => void;
  markDirty: () => void;
  markClean: () => void;
  setGeneratedCode: (code: GeneratedFormCode | null) => void;
  reset: () => void;
  replaceClientIds: (idMap: Record<string, string>) => void;
}

const useFormBuilderStore = create<FormBuilderStore>()(
  persist(
    (set) => ({
      config: null,
      selectedFieldId: null,
      selectedGroupId: null,
      isDirty: false,
      generatedCode: null,

      setConfig: (config) => set({ config, isDirty: false }),

      // Deep immutable update with immer
      updateConfig: (updates) => set((state) => {
        if (!state.config) {
          console.warn('Attempted to update null config');
          return state;
        }

        return {
          config: produce(state.config, draft => {
            Object.assign(draft, updates);
          }),
          isDirty: true,
        };
      }),

      // NEW: Dedicated groups updater for better ergonomics
      updateGroups: (updater) => set((state) => {
        if (!state.config) {
          console.warn('Attempted to update groups on null config');
          return state;
        }

        return {
          config: produce(state.config, draft => {
            updater(draft.groups);
          }),
          isDirty: true,
        };
      }),

      selectField: (fieldId) => set({ selectedFieldId: fieldId }),
      selectGroup: (groupId) => set({ selectedGroupId: groupId }),
      markDirty: () => set({ isDirty: true }),
      markClean: () => set({ isDirty: false }),
      setGeneratedCode: (code) => set({ generatedCode: code }),
      reset: () => set({
        config: null,
        selectedFieldId: null,
        selectedGroupId: null,
        isDirty: false,
        generatedCode: null,
      }),

      // Replace temp IDs with server IDs after save
      replaceClientIds: (idMap) => set((state) => {
        if (!state.config) return state;

        return {
          config: produce(state.config, draft => {
            // Replace config ID
            if (draft.id && idMap[draft.id]) {
              draft.id = idMap[draft.id];
            }

            // Replace group IDs and field IDs
            draft.groups.forEach(group => {
              if (group.id && idMap[group.id]) {
                group.id = idMap[group.id];
              }

              group.fields.forEach(field => {
                if (field.id && idMap[field.id]) {
                  field.id = idMap[field.id];
                }
              });
            });
          }),
        };
      }),
    }),
    {
      name: 'actox-form-builder',
      version: 2,
      partialize: (state) => ({
        config: state.config ? {
          id: state.config.id,
          projectId: state.config.projectId,
          tableName: state.config.tableName,
          formName: state.config.formName,
          title: state.config.title,
          groups: state.config.groups ?? [],
          options: state.config.options ?? { ...DEFAULT_FORM_OPTIONS },
        } : null,
        isDirty: state.isDirty,
      }),
      migrate: (persistedState: any, version: number) => {
        if (version < 2) {
          return {
            config: null,
            isDirty: false,
          };
        }
        return persistedState as Partial<FormBuilderStore>;
      },
    }
  )
);

// ============================================
// Main Hook - useFormBuilder
// ============================================
export function useFormBuilder() {
  const store = useFormBuilderStore();
  const { selectedProjectId } = useProject();
  const { confirm } = useConfirm();

  // Fetch saved form configs for current project
  const {
    data: savedForms,
    isLoading: isLoadingSavedForms,
    refetch: refetchSavedForms,
  } = useApi<FormConfigListResponse[]>(
    `/FormBuilder/configs/${selectedProjectId}`,
    {
      enabled: !!selectedProjectId,
      staleTime: 2 * 60 * 1000,
    }
  );

  // Save returns ID map for client→server sync
  interface SaveResponse {
    config: FormConfig;
    idMap: Record<string, string>; // temp-xyz → "123"
  }

  const saveConfigMutation = useApiPost<SaveResponse, SaveFormConfigRequest>(
    '/FormBuilder/config',
    {
      successMessage: 'Form configuration saved!',
      onSuccess: (data) => {
        store.replaceClientIds(data.idMap);
        store.markClean();
        refetchSavedForms();
      },
      onMutate: async (_variables) => {
        const previousDirty = store.isDirty;
        store.markClean();
        return { previousDirty };
      },
      onError: (_error, _variables, onMutateResult) => {
        const mutateResult = onMutateResult as { previousDirty: boolean } | undefined;
        toast.error('Save failed. Your changes are preserved locally.');
        if (mutateResult?.previousDirty) store.markDirty();
      },
    }
  );

  // Generate form code
  const generateFormMutation = useApiPost<GeneratedFormCode, GenerateFormRequest>(
    '/FormBuilder/generate',
    {
      showSuccessToast: false,
      showErrorToast: true,
      onSuccess: (data) => {
        store.setGeneratedCode(data);

        if (data.warnings && data.warnings.length > 0) {
          toast.warning(`Generated with ${data.warnings.length} warning(s)`, {
            description: data.warnings.slice(0, 3).join(', '),
            duration: 5000,
          });
        } else {
          toast.success('Form generated successfully!');
        }
      },
    }
  );

  // FIXED: Load form using dedicated mutation
  const loadFormMutation = useApiPost<FormConfig, { formId: string }>(
    '/FormBuilder/config/load',
    {
      onSuccess: (config) => {
        store.setConfig(config);
        toast.success(`Loaded: ${config.formName}`);
      },
      onError: () => {
        toast.error('Failed to load form');
      },
    }
  );

  // Initialize new form
  const initializeForm = (tableName: string, projectId: number, options?: {
    groups?: number;
    groupTitles?: string[];
  }) => {
    const groupCount = options?.groups || 1;
    const groups: FormGroup[] = Array.from({ length: groupCount }, (_, i) => ({
      id: `temp-group-${nanoid()}`,
      title: options?.groupTitles?.[i] || `Section ${i + 1}`,
      fields: [],
      layout: 'row',
      order: i,
    }));

    const newConfig: FormConfig = {
      id: `temp-config-${nanoid()}`,
      projectId,
      tableName,
      formName: `${tableName}Form`,
      title: `${tableName} Form`,
      groups,
      options: { ...DEFAULT_FORM_OPTIONS },
    };

    store.setConfig(newConfig);
    return newConfig;
  };

  // FIXED: Load existing form
  const loadForm = async (formId: string) => {
    if (!formId) {
      toast.error('Invalid form ID');
      return;
    }

    await loadFormMutation.mutateAsync({ formId });
  };

  // Guard against null config
  const ensureConfig = (): FormConfig | null => {
    if (!store.config) {
      toast.error('No form initialized. Please create a form first.');
      return null;
    }
    return store.config;
  };

  // FIXED: Add field using updateGroups
  const addField = (field: Partial<FormField>) => {
    const config = ensureConfig();
    if (!config) return;

    if (!config.groups || config.groups.length === 0) {
      toast.error('No groups available. Add a group first.');
      return;
    }

    const groupId = store.selectedGroupId || config.groups[0].id;

    const newField: FormField = {
      id: `temp-field-${nanoid()}`,
      columnName: field.columnName || '',
      dataType: field.dataType || 'NVARCHAR',
      label: field.label || '',
      inputType: field.inputType || 'text',
      order: 0,
      ...DEFAULT_FIELD,
      ...field,
    } as FormField;

    store.updateGroups(groups => {
      const group = groups.find(g => g.id === groupId);
      if (group) {
        newField.order = group.fields.length;
        group.fields.push(newField);
      }
    });

    store.selectField(newField.id);
    toast.success(`Field "${newField.label}" added`);
  };

  // FIXED: Batch add fields
  const addFields = (fields: Partial<FormField>[]) => {
    const config = ensureConfig();
    if (!config) return;

    if (!config.groups || config.groups.length === 0) {
      toast.error('No groups available');
      return;
    }

    const groupId = store.selectedGroupId || config.groups[0].id;

    store.updateGroups(groups => {
      const group = groups.find(g => g.id === groupId);
      if (group) {
        const startOrder = group.fields.length;
        const newFields = fields.map((field, idx) => ({
          id: `temp-field-${nanoid()}`,
          columnName: field.columnName || '',
          dataType: field.dataType || 'NVARCHAR',
          label: field.label || '',
          inputType: field.inputType || 'text',
          order: startOrder + idx,
          ...DEFAULT_FIELD,
          ...field,
        } as FormField));

        group.fields.push(...newFields);
      }
    });

    toast.success(`Added ${fields.length} fields`);
  };

  // Update field
  const updateField = (fieldId: string, updates: Partial<FormField>) => {
    const config = ensureConfig();
    if (!config) return;

    store.updateGroups(groups => {
      for (const group of groups) {
        const field = group.fields.find(f => f.id === fieldId);
        if (field) {
          Object.assign(field, updates);
          break;
        }
      }
    });
  };

  const deleteField = (fieldId: string) => {
    const config = ensureConfig();
    if (!config) return;

    store.updateGroups(groups => {
      for (const group of groups) {
        const index = group.fields.findIndex(f => f.id === fieldId);
        if (index !== -1) {
          group.fields.splice(index, 1);
          // Reorder remaining fields
          group.fields.forEach((f, i) => {
            f.order = i;
          });
          break;
        }
      }
    });

    store.selectField(null);
    toast.success('Field deleted');
  };

  const reorderField = (fieldId: string, newOrder: number) => {
    const config = ensureConfig();
    if (!config) return;

    store.updateGroups(groups => {
      for (const group of groups) {
        const fieldIndex = group.fields.findIndex(f => f.id === fieldId);
        if (fieldIndex !== -1) {
          const [field] = group.fields.splice(fieldIndex, 1);
          group.fields.splice(newOrder, 0, field);

          // Update order properties
          group.fields.forEach((f, i) => {
            f.order = i;
          });
          break;
        }
      }
    });
  };

  const addGroup = (title?: string) => {
    const config = ensureConfig();
    if (!config) return;

    const newGroup: FormGroup = {
      id: `temp-group-${nanoid()}`,
      title: title || `Section ${config.groups.length + 1}`,
      fields: [],
      layout: 'row',
      order: config.groups.length,
    };

    store.updateGroups(groups => {
      groups.push(newGroup);
    });

    store.selectGroup(newGroup.id);
    toast.success('Section added');
  };

  const updateGroup = (groupId: string, updates: Partial<FormGroup>) => {
    const config = ensureConfig();
    if (!config) return;

    store.updateGroups(groups => {
      const group = groups.find(g => g.id === groupId);
      if (group) {
        Object.assign(group, updates);
      }
    });
  };

  const deleteGroup = (groupId: string) => {
    const config = ensureConfig();
    if (!config) return;

    if (config.groups.length <= 1) {
      toast.error('Cannot delete the last group');
      return;
    }

    const groupToDelete = config.groups.find(g => g.id === groupId);
    if (!groupToDelete) return;

    if (groupToDelete.fields.length > 0) {
      toast.error(
        `This group contains ${groupToDelete.fields.length} field(s). ` +
        `Delete or move them first.`,
        { duration: 5000 }
      );
      return;
    }

    store.updateGroups(groups => {
      const index = groups.findIndex(g => g.id === groupId);
      if (index !== -1) {
        groups.splice(index, 1);
        // Reorder remaining groups
        groups.forEach((g, i) => {
          g.order = i;
        });
      }
    });

    store.selectGroup(null);
    toast.success('Section deleted');
  };

  const saveConfig = async () => {
    const config = ensureConfig();
    if (!config || !selectedProjectId) {
      toast.error('Cannot save: no form or project selected');
      return;
    }

    await saveConfigMutation.mutateAsync({
      projectId: selectedProjectId,
      config,
    });
  };

  const generateForm = async (preview = false) => {
    const config = ensureConfig();
    if (!config) return;

    await generateFormMutation.mutateAsync({
      config,
      preview,
    });
  };

  // Memoization depends on groups array, not just config.id
  const selectedField = useMemo(() => {
    if (!store.config || !store.selectedFieldId) return null;

    for (const group of store.config.groups) {
      const field = group.fields.find(f => f.id === store.selectedFieldId);
      if (field) return field;
    }
    return null;
  }, [store.config?.groups, store.selectedFieldId]);

  const selectedGroup = useMemo(() => {
    if (!store.config || !store.selectedGroupId) return null;
    return store.config.groups.find(g => g.id === store.selectedGroupId) || null;
  }, [store.config?.groups, store.selectedGroupId]);

  const resetForm = async () => {
    if (store.isDirty) {
      const confirmed = await confirm({
        title: 'Reset Form',
        description: 'You have unsaved changes. Reset anyway?',
        confirmText: 'Reset',
        cancelText: 'Cancel',
        variant: 'destructive',
      });
      if (!confirmed) return;
    }
    store.reset();
    toast.info('Form builder reset');
  };

  return {
    // State
    config: store.config,
    savedForms,
    selectedField,
    selectedGroup,
    isDirty: store.isDirty,
    generatedCode: store.generatedCode,

    // Loading states
    isLoadingSavedForms,
    isSaving: saveConfigMutation.isPending,
    isGenerating: generateFormMutation.isPending,
    isLoadingForm: loadFormMutation.isPending,

    // Actions - Form
    initializeForm,
    loadForm,
    saveConfig,
    generateForm,
    resetForm,

    // Actions - Fields
    addField,
    addFields,
    updateField,
    deleteField,
    reorderField,
    selectField: store.selectField,

    // Actions - Groups
    addGroup,
    updateGroup,
    deleteGroup,
    selectGroup: store.selectGroup,

    // Actions - Options
    updateOptions: (updates: Partial<FormConfig['options']>) => {
      const config = ensureConfig();
      if (!config) return;

      store.updateConfig({
        options: { ...config.options, ...updates }
      });
    },

    // Utilities
    refetchSavedForms,
  } as const;
}

// ============================================
// Hook - useFormFieldFromSchema
// ============================================
export function useFormFieldFromSchema(tableName: string) {
  let schema, isLoading;
  try {
    const result = useTableSchema(tableName);
    schema = result.schema;
    isLoading = result.isLoading;
  } catch (error) {
    console.warn('useTableSchema not available:', error);
    schema = undefined;
    isLoading = false;
  }

  const { addField, addFields, config } = useFormBuilder();

  const importFieldFromSchema = (columnName: string) => {
    if (!config) {
      toast.error('Initialize a form first');
      return;
    }

    const column = schema?.columns.find(c => c.columnName === columnName);
    if (!column) {
      toast.error('Column not found in schema');
      return;
    }

    const inputType = mapSqlTypeToInputType(column.dataType, column.isForeignKey, columnName);

    addField({
      columnName: column.columnName,
      dataType: column.dataType,
      label: formatLabel(column.columnName),
      inputType,
      required: !column.isNullable,
      isPrimaryKey: column.isPrimaryKey,
      isIdentity: column.isIdentity,
      isForeignKey: column.isForeignKey,
      includeInInsert: !column.isIdentity,
      includeInUpdate: !column.isIdentity && !column.isPrimaryKey,
    });
  };

  const importAllFields = () => {
    if (!config) {
      toast.error('Initialize a form first');
      return;
    }

    if (!schema?.columns || schema.columns.length === 0) {
      toast.error('No columns to import');
      return;
    }

    // Filter out system columns
    const userColumns = schema.columns.filter(col =>
      !col.isIdentity &&
      !['CreatedAt', 'UpdatedAt', 'CreatedBy', 'UpdatedBy', 'RowVersion', 'Timestamp'].includes(col.columnName)
    );

    const fields = userColumns.map(column => ({
      columnName: column.columnName,
      dataType: column.dataType,
      label: formatLabel(column.columnName),
      inputType: mapSqlTypeToInputType(column.dataType, column.isForeignKey, column.columnName),
      required: !column.isNullable,
      isPrimaryKey: column.isPrimaryKey,
      isIdentity: column.isIdentity,
      isForeignKey: column.isForeignKey,
      includeInInsert: !column.isIdentity,
      includeInUpdate: !column.isIdentity && !column.isPrimaryKey,
    }));

    addFields(fields);
    toast.success(`Imported ${fields.length} fields from schema`);
  };

  return {
    schema,
    isLoading,
    importFieldFromSchema,
    importAllFields,
  };
}

// ============================================
// Utility Functions
// ============================================
function mapSqlTypeToInputType(
  sqlType: string,
  isForeignKey = false,
  columnName = ''
): InputType {
  const type = sqlType.toUpperCase();
  const colName = columnName.toLowerCase();

  // FK mapping
  if (isForeignKey) {
    return 'select';
  }

  // Email detection
  if (colName.includes('email')) {
    return 'email';
  }

  // GUID/Unique identifier
  if (type.includes('UNIQUEIDENTIFIER') || type.includes('GUID')) {
    return 'text';
  }

  // XML/JSON
  if (type.includes('XML') || type.includes('JSON')) {
    return 'textarea';
  }

  // Number types
  if (type.includes('INT') || type.includes('NUMERIC') || type.includes('DECIMAL') ||
    type.includes('FLOAT') || type.includes('REAL') || type.includes('MONEY')) {
    return 'number';
  }

  // Date/Time types
  if (type.includes('DATETIME')) {
    return 'datetime-local';
  }
  if (type.includes('DATE')) {
    return 'date';
  }
  if (type.includes('TIME')) {
    return 'time';
  }

  // Boolean types
  if (type.includes('BIT') || type.includes('BOOL')) {
    return 'checkbox';
  }

  // Text types
  if (type.includes('TEXT') || type === 'NVARCHAR(MAX)' || type === 'VARCHAR(MAX)') {
    return 'textarea';
  }

  return 'text';
}

function formatLabel(columnName: string): string {
  return columnName
    .replace(/([A-Z])/g, ' $1') // PascalCase
    .replace(/[_-]/g, ' ') // snake_case & kebab-case
    .trim()
    .split(' ')
    .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ')
    .replace(/&/g, 'and')
    .replace(/[<>"']/g, '');
}