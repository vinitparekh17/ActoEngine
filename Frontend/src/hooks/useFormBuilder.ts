// hooks/useFormBuilder.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { nanoid } from 'nanoid';
import { toast } from 'sonner';
import { useApiPost } from './useApi';

export type InputType =
  | 'text'
  | 'number'
  | 'email'
  | 'date'
  | 'select'
  | 'textarea'
  | 'checkbox'
  | 'password'
  | 'radio'
  | 'file'
  | 'hidden';

export interface SelectOption {
  label: string;
  value: string;
}

export interface ForeignKeyInfo {
  referencedTable: string;
  referencedColumn: string; 
  displayColumn?: string;
  onDeleteAction: string;
  onUpdateAction: string;
  sampleColumns?: string[];
}

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
  referencedTable?: string;
  foreignKeyInfo?: ForeignKeyInfo;
  options?: SelectOption[];
  disabled: boolean;
  readonly: boolean;
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
  tableName: string | null;
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

export interface TableColumn {
  schemaName: string;
  columnName: string;
  dataType: string;
  maxLength: number | null;
  isNullable: boolean;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  isForeignKey: boolean;
  defaultValue: string;
  foreignKeyInfo?: ForeignKeyInfo;
}

export interface TableSchema {
    tableName: string;
    schemaName: string;
    columns: Array<{
      schemaName: string;
      columnName: string;
      dataType: string;
      maxLength: number | null;
      isNullable: boolean;
      isPrimaryKey: boolean;
      isIdentity: boolean;
      isForeignKey: boolean;
      defaultValue: string;
      foreignKeyInfo?: ForeignKeyInfo;
    }>;
    primaryKeys: string[];
}

interface FormBuilderStore {
  config: FormConfig | null;
  selectedFieldId: string | null;
  selectedGroupId: string | null;
  generatedCode: GeneratedCode | null;
  tableSchema: TableSchema | null;

  setConfig: (config: FormConfig) => void;
  setTableSchema: (schema: TableSchema | null) => void;
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
    (set, _get) => ({
      config: null,
      selectedFieldId: null,
      selectedGroupId: null,
      generatedCode: null,
      tableSchema: null,

      setConfig: (config) => set({ config }),
      setTableSchema: (schema) => set({ tableSchema: schema }),
      addGroup: (group) => set((state) => {
        if (!state.config) return state;
        const newGroup: FormGroup = {
          id: group?.id || `group-${nanoid(6)}`,
          title: group?.title || 'New Group',
          description: group?.description,
          fields: group?.fields || [],
          layout: group?.layout || 'row',
          order: group?.order ?? state.config.groups.length,
          collapsible: group?.collapsible ?? false,
          collapsed: group?.collapsed ?? false,
        };
        return { config: { ...state.config, groups: [...state.config.groups, newGroup] } };
      }),
      updateGroup: (groupId, updates) => set((state) => {
        if (!state.config) return state;
        const groups = state.config.groups.map(g => g.id === groupId ? { ...g, ...updates } : g);
        return { config: { ...state.config, groups } };
      }),
      deleteGroup: (groupId) => set((state) => {
        if (!state.config) return state;
        const groups = state.config.groups.filter(g => g.id !== groupId);
        return { config: { ...state.config, groups } };
      }),
      addField: (groupId, field) => set((state) => {
        if (!state.config) return state;
        const groups = state.config.groups.map(g => {
          if (g.id === groupId) {
            const newField: FormField = {
              id: field.id || `field-${nanoid(6)}`,
              columnName: field.columnName || '',
              dataType: field.dataType || 'varchar',
              label: field.label || 'New Field',
              inputType: field.inputType || 'text',
              placeholder: field.placeholder,
              defaultValue: field.defaultValue,
              helpText: field.helpText,
              colSize: field.colSize ?? 6,
              order: field.order ?? g.fields.length,
              required: field.required ?? false,
              minLength: field.minLength,
              maxLength: field.maxLength,
              min: field.min,
              max: field.max,
              pattern: field.pattern,
              errorMessage: field.errorMessage,
              includeInInsert: field.includeInInsert ?? true,
              includeInUpdate: field.includeInUpdate ?? true,
              isPrimaryKey: field.isPrimaryKey ?? false,
              isIdentity: field.isIdentity ?? false,
              isForeignKey: field.isForeignKey ?? false,
              options: field.options,
              disabled: field.disabled ?? false,
              readonly: field.readonly ?? false,
            };
            return { ...g, fields: [...g.fields, newField] };
          }
          return g;
        });
        return { config: { ...state.config, groups } };
      }),
      updateField: (fieldId, updates) => set((state) => {
        if (!state.config) return state;
        const groups = state.config.groups.map(g => ({
          ...g,
          fields: g.fields.map(f => f.id === fieldId ? { ...f, ...updates } : f)
        }));
        return { config: { ...state.config, groups } };
      }),
      deleteField: (fieldId) => set((state) => {
        if (!state.config) return state;
        const groups = state.config.groups.map(g => ({
          ...g,
          fields: g.fields.filter(f => f.id !== fieldId)
        }));
        return { config: { ...state.config, groups } };
      }),
      selectField: (fieldId) => set({ selectedFieldId: fieldId }),
      selectGroup: (groupId) => set({ selectedGroupId: groupId }),
      setGeneratedCode: (code) => set({ generatedCode: code }),
      reset: () => set({ config: null, selectedFieldId: null, selectedGroupId: null, generatedCode: null, tableSchema: null }),
    }),
    {
      name: 'actox-form-builder',
      version: 1,
      partialize: (state) => ({
        selectedFieldId: state.selectedFieldId,
        selectedGroupId: state.selectedGroupId,
      }),
    }
  )
);

export function useFormBuilder() {
  const store = useFormBuilderStore();

  // Save form config
  const saveConfigMutation = useApiPost<
    { success: boolean; formId: number; config: FormConfig },
    { projectId: number; config: FormConfig; description?: string }
  >('/formbuilder/save', {
    onSuccess: () => {
      toast.success('Form configuration saved!');
    },
  });

  // Generate code
  const generateCodeMutation = useApiPost<
    GeneratedCode,
    {
      config: FormConfig;
      generateStoredProcedures?: boolean;
      preview?: boolean;
    }
  >('/formbuilder/generate', {
    onSuccess: (data) => {
      const { success, message, warnings } = data || {};
      if (success) {
        store.setGeneratedCode(data);

        if (warnings?.length) {
          toast.warning(
            `Code generated with warnings: ${warnings.join(', ')}`
          );
        } else {
          toast.success(message || 'Code generated successfully!');
        }
      } else {
        toast.error(
          message || 'Failed to generate code. Please review inputs.'
        );
      }
    },
    onError: (error) => {
      console.error(error);
      toast.error('Server error while generating code.');
    },
  });

  // Initialize from table schema
  const initializeFromTable = (
    tableId: string | number,
    projectId: number,
    schema: TableSchema
  ) => {
    const tableName = schema.tableName;
    const config: FormConfig = {
      id: `form-${nanoid(6)}`,
      projectId,
      tableName,
      formName: `${tableName}Form`,
      title: `${tableName} Form`,
      groups: [],
    };

    store.setConfig(config);
    store.setTableSchema(schema);

    // Create default group
    const groupId = `group-${nanoid(6)}`;
    const newGroup: FormGroup = {
      id: groupId,
      title: 'Main Fields',
      fields: [],
      layout: 'row',
      order: 0,
      collapsible: false,
      collapsed: false,
    };

    // Auto-map columns to fields
    const fields: FormField[] = schema.columns
      .filter((col) => !col.isIdentity)
      .map((col, index) => ({
        id: `field-${nanoid(6)}`,
        columnName: col.columnName,
        dataType: col.dataType,
        label: formatLabel(col.columnName),
        inputType: mapColumnToInputType(col),
        placeholder: `Enter ${formatLabel(col.columnName).toLowerCase()}`,
        colSize: suggestColSize(col),
        order: index,
        required: !col.isNullable,
        includeInInsert: !col.isPrimaryKey && !col.isIdentity,
        includeInUpdate: !col.isPrimaryKey && !col.isIdentity,
        isPrimaryKey: col.isPrimaryKey,
        isIdentity: col.isIdentity,
        isForeignKey: col.isForeignKey,
        referencedTable: col.foreignKeyInfo?.referencedTable,
        foreignKeyInfo: col.foreignKeyInfo,
        disabled: col.isPrimaryKey || col.isIdentity,
        readonly: col.isPrimaryKey || col.isIdentity,
        maxLength: col.maxLength || undefined,
      }));

    newGroup.fields = fields;

    store.setConfig({
      ...config,
      groups: [newGroup],
    });

    return config;
  };

  // Initialize without table
  const initializeCustomForm = (formName: string, projectId: number) => {
  const config: FormConfig = {
    id: `form-${nanoid(6)}`,
    projectId,
    tableName: null,
    formName,
    title: formName,
    groups: [],
  };

  const groupId = `group-${nanoid(6)}`;
  const newGroup: FormGroup = {
    id: groupId,
    title: 'Main Fields',
    fields: [
      {
        id: `field-${nanoid(6)}`,
        columnName: 'field_1',
        dataType: 'varchar',
        label: 'New Field',
        inputType: 'text',
        colSize: 6,
        order: 0,
        required: false,
        includeInInsert: true,
        includeInUpdate: true,
        isPrimaryKey: false,
        isIdentity: false,
        isForeignKey: false,
        disabled: false,
        readonly: false,
      },
    ],
    layout: 'row',
    order: 0,
    collapsible: false,
    collapsed: false,
  };

  store.setConfig({
    ...config,
    groups: [newGroup],
  });
  store.setTableSchema(null);
  store.selectGroup(groupId);

  return config;
};

  // Get selected field
  const selectedField =
    store.config?.groups
      .flatMap((g) => g.fields)
      .find((f) => f.id === store.selectedFieldId) || null;

  // Get selected group
  const selectedGroup =
    store.config?.groups.find((g) => g.id === store.selectedGroupId) || null;

  // Get all fields (flattened from all groups)
  const allFields = store.config?.groups.flatMap((g) => g.fields) || [];

  return {
    // State
    config: store.config,
    groups: store.config?.groups || [],
    fields: allFields,
    selectedField,
    selectedGroup,
    generatedCode: store.generatedCode,
    tableSchema: store.tableSchema,

    // Loading states
    isSaving: saveConfigMutation.isPending,
    isGenerating: generateCodeMutation.isPending,

    // Actions
    initializeFromTable,
    initializeCustomForm,
    saveConfig: () => {
      if (!store.config) return;
      saveConfigMutation.mutate({
        projectId: store.config.projectId,
        config: store.config,
      });
    },
    generateCode: () => {
      if (!store.config) return;
      generateCodeMutation.mutate({
        config: store.config,
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

    // Schema
    setTableSchema: store.setTableSchema,

    // Reset
    reset: store.reset,
  };
}

// Enhanced mapping function
function mapColumnToInputType(col: TableColumn): InputType {
  const type = col.dataType.toUpperCase();

  // Foreign keys become dropdowns
  if (col.isForeignKey) return 'select';

  // Type-based mapping
  if (
    type.includes('INT') ||
    type.includes('DECIMAL') ||
    type.includes('NUMERIC')
  )
    return 'number';
  if (type.includes('DATE') || type.includes('DATETIME')) return 'date';
  if (type.includes('BIT')) return 'checkbox';
  if (
    type.includes('TEXT') ||
    type.includes('NVARCHAR(MAX)') ||
    (col.maxLength && col.maxLength > 200)
  )
    return 'textarea';

  return 'text';
}

// Suggest column size based on data type
function suggestColSize(col: TableColumn): number {
  if (col.dataType === 'bit') return 12; // Checkbox full width
  if (col.isForeignKey) return 6; // Dropdowns half width
  if (col.dataType.includes('date')) return 6;
  if (col.maxLength && col.maxLength > 100) return 12;
  return 6; // Default half width
}

function formatLabel(columnName: string): string {
  return columnName
    .replace(/([A-Z])/g, ' $1')
    .replace(/[_-]/g, ' ')
    .trim()
    .split(' ')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
    .join(' ');
}