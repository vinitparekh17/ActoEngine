// pages/FormBuilderSimple.tsx
// SIMPLIFIED VERSION - Just the essentials

import { useState, lazy } from 'react';
import { useFormBuilder, type GeneratedCode } from '../hooks/useFormBuilder';
import { useProject } from '../hooks/useProject';
import { Save, Code, Plus, Trash2, Copy } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '../components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../components/ui/tabs';
import { Input } from '../components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';

const MonacoEditor = lazy(() => import("@monaco-editor/react"));

export default function FormBuilderSimple() {
  const { selectedProject } = useProject();
  const {
    config,
    fields,
    generatedCode,
    isSaving,
    isGenerating,
    initializeForm,
    saveConfig,
    generateCode,
  } = useFormBuilder();

  const [activeTab, setActiveTab] = useState<'builder' | 'preview' | 'code'>('builder');
  const [tableName, setTableName] = useState('');

  // Initialize form
  const handleInitialize = () => {
    if (!tableName.trim()) {
      toast.error('Please enter a table name');
      return;
    }
    if (!selectedProject) {
      toast.error('Please select a project');
      return;
    }

    // Check if a form already exists for this table/project
    if (config && config.tableName === tableName.trim() && config.projectId === selectedProject.projectId) {
      const confirmed = window.confirm(
        `A form for table "${tableName}" already exists. Initializing will reset any unsaved changes. Continue?`
      );
      if (!confirmed) {
        return;
      }
    }

    initializeForm(tableName.trim(), selectedProject.projectId);
  };

  if (!selectedProject) {
    return (
      <div className="flex h-screen items-center justify-center">
        <p className="text-gray-500">Please select a project first</p>
      </div>
    );
  }

  return (
    <div className="flex h-screen flex-col">
      {/* Header */}
      <div className="border-b bg-white px-6 py-3">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold">
              {config ? config.formName : 'Form Builder'}
            </h1>
            {config && (
              <p className="text-sm text-gray-600">
                Table: {config.tableName} • {fields.length} fields
              </p>
            )}
          </div>

          {config && (
            <div className="flex items-center gap-3">
              {/* Actions */}
              <Button
                onClick={saveConfig}
                disabled={isSaving}
                variant="default"
              >
                <Save className="h-4 w-4" />
                {isSaving ? 'Saving...' : 'Save'}
              </Button>

              <Button
                onClick={generateCode}
                disabled={isGenerating}
                variant="outline"
              >
                <Code className="h-4 w-4" />
                {isGenerating ? 'Generating...' : 'Generate'}
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-hidden">
        {!config ? (
          // Initialize Form Modal
          <div className="flex h-full items-center justify-center bg-gray-50">
            <Card className="w-96">
              <CardHeader>
                <CardTitle>Create New Form</CardTitle>
              </CardHeader>
              <CardContent>
                <Input
                  type="text"
                  value={tableName}
                  onChange={(e) => setTableName(e.target.value)}
                  placeholder="Enter table name (e.g., Users)"
                  onKeyDown={(e) => e.key === 'Enter' && handleInitialize()}
                />
                <div className="mt-4">
                  <Button
                    onClick={handleInitialize}
                    className="w-full"
                  >
                    Create Form
                  </Button>
                </div>
              </CardContent>
            </Card>
          </div>
        ) : (
          <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as 'builder' | 'preview' | 'code')} className="h-full">
            <div className="border-b px-6 py-2">
              <TabsList>
                <TabsTrigger value="builder">Builder</TabsTrigger>
                <TabsTrigger value="preview">Preview</TabsTrigger>
                <TabsTrigger value="code" disabled={!generatedCode?.success}>Code</TabsTrigger>
              </TabsList>
            </div>
            <TabsContent value="builder" className="h-full m-0">
              <BuilderTab />
            </TabsContent>
            <TabsContent value="preview" className="h-full m-0">
              <PreviewTab />
            </TabsContent>
            <TabsContent value="code" className="h-full m-0">
              {generatedCode && <CodeTab code={generatedCode} />}
            </TabsContent>
          </Tabs>
        )}
      </div>
    </div>
  );
}

// ============================================
// Builder Tab - Simplified field management
// ============================================

function BuilderTab() {
  const {
    fields,
    selectedField,
    addField,
    updateField,
    deleteField,
    selectField,
  } = useFormBuilder();

  return (
    <div className="flex h-full">
      {/* Field List */}
      <div className="w-80 border-r bg-gray-50 p-4">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="font-semibold">Fields</h3>
          <Button
            onClick={() => addField({})}
            size="sm"
          >
            <Plus className="h-4 w-4" />
            Add Field
          </Button>
        </div>

        <div className="space-y-2">
          {fields.map((field) => (
            <div
              key={field.id}
              onClick={() => selectField(field.id)}
              className={`cursor-pointer rounded border p-3 ${selectedField?.id === field.id
                ? 'border-blue-500 bg-blue-50'
                : 'border-gray-200 bg-white hover:border-gray-300'
                }`}
            >
              <div className="flex items-start justify-between">
                <div>
                  <div className="font-medium">{field.label || 'Untitled'}</div>
                  <div className="text-xs text-gray-500">
                    {field.columnName} • {field.inputType}
                  </div>
                </div>
                <Button
                  onClick={(e) => {
                    e.stopPropagation();
                    deleteField(field.id);
                  }}
                  variant="ghost"
                  size="sm"
                  className="text-gray-400 hover:text-red-600"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>

        {fields.length === 0 && (
          <div className="rounded border-2 border-dashed border-gray-300 p-4 text-center text-sm text-gray-500">
            No fields yet. Click "Add Field" to start.
          </div>
        )}
      </div>

      {/* Properties Panel */}
      <div className="flex-1 p-6">
        {selectedField ? (
          <FieldProperties field={selectedField} onUpdate={updateField} />
        ) : (
          <div className="flex h-full items-center justify-center text-gray-500">
            Select a field to edit its properties
          </div>
        )}
      </div>
    </div>
  );
}

// ============================================
// Field Properties - Simple editor
// ============================================

function FieldProperties({
  field,
  onUpdate
}: {
  field: any;
  onUpdate: (id: string, updates: any) => void;
}) {
  return (
    <div className="max-w-2xl space-y-4">
      <h3 className="text-lg font-semibold">Field Properties</h3>

      <div>
        <label className="mb-1 block text-sm font-medium">Label</label>
        <input
          type="text"
          value={field.label}
          onChange={(e) => onUpdate(field.id, { label: e.target.value })}
          className="w-full rounded border px-3 py-2"
        />
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium">Column Name</label>
        <input
          type="text"
          value={field.columnName}
          onChange={(e) => onUpdate(field.id, { columnName: e.target.value })}
          className="w-full rounded border px-3 py-2"
        />
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium">Input Type</label>
        <select
          value={field.inputType}
          onChange={(e) => onUpdate(field.id, { inputType: e.target.value })}
          className="w-full rounded border px-3 py-2"
        >
          <option value="text">Text</option>
          <option value="number">Number</option>
          <option value="email">Email</option>
          <option value="date">Date</option>
          <option value="select">Dropdown</option>
          <option value="textarea">Text Area</option>
          <option value="checkbox">Checkbox</option>
        </select>
      </div>

      <div>
        <label className="mb-1 block text-sm font-medium">Width</label>
        <select
          value={field.colSize}
          onChange={(e) => onUpdate(field.id, { colSize: parseInt(e.target.value) })}
          className="w-full rounded border px-3 py-2"
        >
          <option value={12}>Full Width</option>
          <option value={6}>Half Width</option>
          <option value={4}>One Third</option>
          <option value={3}>One Quarter</option>
        </select>
      </div>

      <div>
        <label className="flex items-center">
          <input
            type="checkbox"
            checked={field.required}
            onChange={(e) => onUpdate(field.id, { required: e.target.checked })}
            className="mr-2"
          />
          Required field
        </label>
      </div>
    </div>
  );
}

// ============================================
// Preview Tab - Simple form preview
// ============================================

function PreviewTab() {
  const { config, fields } = useFormBuilder();
  const colSpanMap: Record<number, string> = {
    1: 'col-span-1',
    2: 'col-span-2',
    3: 'col-span-3',
    4: 'col-span-4',
    5: 'col-span-5',
    6: 'col-span-6',
    7: 'col-span-7',
    8: 'col-span-8',
    9: 'col-span-9',
    10: 'col-span-10',
    11: 'col-span-11',
    12: 'col-span-12',
  };
  if (!config) return null;

  return (
    <div className="bg-gray-50 p-6">
      <div className="mx-auto max-w-4xl rounded-lg bg-white p-8 shadow">
        <h2 className="mb-6 text-2xl font-bold">{config.title}</h2>

        <form onSubmit={(e) => e.preventDefault()}>
          <div className="grid grid-cols-12 gap-4">
            {fields.map((field) => (
              <div
                key={field.id}
                className={`${colSpanMap[field.colSize] || 'col-span-1'} other-static-classes`}
              >
                <label className="mb-1 block text-sm font-medium">
                  {field.label}
                  {field.required && <span className="text-red-500">*</span>}
                </label>

                {field.inputType === 'textarea' ? (
                  <textarea className="w-full rounded border px-3 py-2" rows={3} />
                ) : field.inputType === 'select' ? (
                  <select className="w-full rounded border px-3 py-2">
                    <option>Choose...</option>
                  </select>
                ) : field.inputType === 'checkbox' ? (
                  <input type="checkbox" className="mt-1" />
                ) : (
                  <input
                    type={field.inputType}
                    className="w-full rounded border px-3 py-2"
                    required={field.required}
                  />
                )}
              </div>
            ))}
          </div>

          <div className="mt-6">
            <button
              type="submit"
              className="rounded bg-blue-600 px-6 py-2 text-white hover:bg-blue-700"
            >
              Submit
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ============================================
// Code Tab - Display generated code
// ============================================

function CodeTab({ code }: { code: GeneratedCode }) {
  const [showHtml, setShowHtml] = useState(true);

  return (
    <div className="flex h-full flex-col">
      <div className="border-b p-2">
        <div className="flex gap-2">
          <Button
            onClick={() => setShowHtml(true)}
            className={`rounded px-4 py-1 text-sm ${showHtml ? 'bg-blue-600 text-white' : 'bg-gray-100'
              }`}
          >
            HTML
          </Button>
          <Button
            onClick={() => setShowHtml(false)}
            className={`rounded px-4 py-1 text-sm ${!showHtml ? 'bg-blue-600 text-white' : 'bg-gray-100'
              }`}
          >
            JavaScript
          </Button>
          <Button
            onClick={() => {
              const codeToCopy = showHtml ? code.html : code.javaScript;
              navigator.clipboard.writeText(codeToCopy).then(() => {
                toast.success('Code copied to clipboard');
              });
            }}
            className="rounded px-4 py-1 text-sm bg-gray-100 hover:bg-gray-200 flex items-center gap-1"
          >
            <Copy className="h-4 w-4" />
            Copy
          </Button>
        </div>
      </div>

      <div className="flex-1 overflow-auto bg-gray-900">
        <MonacoEditor
          height="100%"
          defaultLanguage={showHtml ? 'html' : 'javascript'}
          theme='vs-dark'
          value={showHtml ? code.html : code.javaScript}
          options={{
            minimap: { enabled: false },
            fontSize: 13,
            wordWrap: "on",
            scrollBeyondLastLine: false,
            readOnly: true,
          }}
        />
      </div>
    </div>
  );
}