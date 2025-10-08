// ============================================
// Form Builder Type Definitions
// ============================================

import { AlignLeft, Calendar, CalendarClock, CheckSquare, CircleDot, Clock, EyeOff, Hash, List, Mail, Paperclip, Type, Lock } from "lucide-react";

export type InputType = 
  | 'text'
  | 'number'
  | 'email'
  | 'password'
  | 'date'
  | 'datetime-local'
  | 'time'
  | 'checkbox'
  | 'radio'
  | 'select'
  | 'textarea'
  | 'file'
  | 'hidden';

export type ValidationOperator = '==' | '!=' | '>' | '<' | '>=' | '<=';

export type ColSize = 1 | 2 | 3 | 4 | 6 | 12;

export type FormStyle = 'horizontal' | 'vertical' | 'inline';

export type LabelPosition = 'top' | 'left' | 'floating';

export type GridType = 'jqgrid' | 'datatables' | 'none';

export type JsFramework = 'vanilla' | 'jquery';

export type BootstrapVersion = '4' | '5';

export type ModalSize = 'sm' | 'md' | 'lg' | 'xl';

// ============================================
// Form Field Configuration
// ============================================
export interface FormField {
  id: string;
  
  // Database mapping
  columnName: string;
  dataType: string;
  
  // UI Configuration
  label: string;
  inputType: InputType;
  placeholder?: string;
  defaultValue?: string;
  helpText?: string;
  
  // Layout
  colSize: ColSize;
  order: number;
  groupId?: string; // Which group this field belongs to
  
  // Validation
  required: boolean;
  minLength?: number;
  maxLength?: number;
  min?: number;
  max?: number;
  pattern?: string;
  customValidation?: string;
  errorMessage?: string;
  
  // Conditional rendering
  showIf?: {
    field: string;
    operator: ValidationOperator;
    value: any;
  };
  
  // Database operations
  includeInInsert: boolean;
  includeInUpdate: boolean;
  isPrimaryKey: boolean;
  isIdentity: boolean;
  isForeignKey: boolean;
  
  // Select/Radio options
  options?: Array<{ label: string; value: string }>;
  optionsSource?: 'manual' | 'api' | 'table';
  apiEndpoint?: string;
  
  // Additional attributes
  disabled?: boolean;
  readonly?: boolean;
}

// ============================================
// Form Group (Section)
// ============================================
export interface FormGroup {
  id: string;
  title?: string;
  description?: string;
  fields: FormField[];
  layout: 'row' | 'column';
  order: number;
  collapsible?: boolean;
  collapsed?: boolean;
}

// ============================================
// Form Generation Options
// ============================================
export interface FormGenerationOptions {
  // Bootstrap options
  bootstrapVersion: BootstrapVersion;
  formStyle: FormStyle;
  labelPosition: LabelPosition;
  
  // JavaScript options
  jsFramework: JsFramework;
  validationStyle: 'inline' | 'toast' | 'alert';
  
  // CRUD options
  generateGrid: boolean;
  gridType: GridType;
  spPrefix: string;
  
  // Modal options
  useModal: boolean;
  modalSize: ModalSize;
  
  // Permission checks
  includePermissionChecks: boolean; // isA(), isU(), isD()
  
  // Error handling
  includeErrorHandling: boolean;
}

// ============================================
// Complete Form Configuration
// ============================================
export interface FormConfig {
  id: string;
  projectId: number;
  tableName: string;
  formName: string;
  title: string;
  description?: string;
  
  // Form structure
  groups: FormGroup[];
  
  // Generation options
  options: FormGenerationOptions;
  
  // Metadata
  createdAt?: Date;
  updatedAt?: Date;
  createdBy?: number;
}

// ============================================
// Generated Code Output
// ============================================
export interface GeneratedFormCode {
  html: string;
  javascript: string;
  storedProcedures: GeneratedSpItem[];
  fileName: string;
  warnings?: string[];
}

export interface GeneratedSpItem {
  spName: string;
  spType: 'CUD' | 'SELECT';
  code: string;
  fileName: string;
  description?: string;
}

// ============================================
// Form Builder State
// ============================================
export interface FormBuilderState {
  config: FormConfig | null;
  selectedFieldId: string | null;
  selectedGroupId: string | null;
  isDirty: boolean;
  isGenerating: boolean;
  generatedCode: GeneratedFormCode | null;
}

// ============================================
// API Request/Response Types
// ============================================
export interface SaveFormConfigRequest {
  projectId: number;
  config: FormConfig;
}

export interface GenerateFormRequest {
  config: FormConfig;
  preview?: boolean; // If true, don't save to DB
}

export interface FormConfigListResponse {
  id: string;
  projectId: number;
  formName: string;
  tableName: string;
  title: string;
  createdAt: Date;
  updatedAt: Date;
}

// ============================================
// Default Values
// ============================================
export const DEFAULT_FORM_OPTIONS: FormGenerationOptions = {
  bootstrapVersion: '5',
  formStyle: 'vertical',
  labelPosition: 'top',
  jsFramework: 'jquery',
  validationStyle: 'inline',
  generateGrid: true,
  gridType: 'jqgrid',
  spPrefix: 'usp',
  useModal: true,
  modalSize: 'lg',
  includePermissionChecks: true,
  includeErrorHandling: true,
};

export const DEFAULT_FIELD: Partial<FormField> = {
  colSize: 12,
  required: false,
  includeInInsert: true,
  includeInUpdate: true,
  isPrimaryKey: false,
  isIdentity: false,
  isForeignKey: false,
  inputType: 'text',
};

// ============================================
// Input Type Metadata
// ============================================
export const INPUT_TYPE_METADATA: Record<InputType, {
  label: string;
  icon: React.ElementType;
  defaultColSize: ColSize;
  supportsMinMax: boolean;
  supportsOptions: boolean;
}> = {
  text: { label: 'Text Input', icon: Type, defaultColSize: 12, supportsMinMax: false, supportsOptions: false },
  number: { label: 'Number', icon: Hash, defaultColSize: 6, supportsMinMax: true, supportsOptions: false },
  email: { label: 'Email', icon: Mail, defaultColSize: 12, supportsMinMax: false, supportsOptions: false },
  password: { label: 'Password', icon: Lock, defaultColSize: 12, supportsMinMax: false, supportsOptions: false },
  date: { label: 'Date', icon: Calendar, defaultColSize: 6, supportsMinMax: false, supportsOptions: false },
  'datetime-local': { label: 'Date Time', icon: CalendarClock, defaultColSize: 6, supportsMinMax: false, supportsOptions: false },
  time: { label: 'Time', icon: Clock, defaultColSize: 4, supportsMinMax: false, supportsOptions: false },
  checkbox: { label: 'Checkbox', icon: CheckSquare, defaultColSize: 6, supportsMinMax: false, supportsOptions: false },
  radio: { label: 'Radio Group', icon: CircleDot, defaultColSize: 12, supportsMinMax: false, supportsOptions: true },
  select: { label: 'Dropdown', icon: List, defaultColSize: 12, supportsMinMax: false, supportsOptions: true },
  textarea: { label: 'Text Area', icon: AlignLeft, defaultColSize: 12, supportsMinMax: false, supportsOptions: false },
  file: { label: 'File Upload', icon: Paperclip, defaultColSize: 12, supportsMinMax: false, supportsOptions: false },
  hidden: { label: 'Hidden Field', icon: EyeOff, defaultColSize: 12, supportsMinMax: false, supportsOptions: false },
};