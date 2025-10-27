// ============================================
// Preview Tab - Simple form preview
// ============================================

import { useFormBuilder } from "../../hooks/useFormBuilder";

export default function PreviewTab() {
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
    <div className="bg-gray-50 p-6 dark:bg-neutral-900 min-h-[calc(100vh-200px)]">
      <div className="mx-auto max-w-4xl rounded-lg bg-white p-8 shadow">
        <h2 className="mb-6 text-2xl font-bold text-gray-900">{config.title}</h2>

        <form onSubmit={(e) => e.preventDefault()}>
          <div className="grid grid-cols-12 gap-4">
            {fields.map((field) => (
              <div
                key={field.id}
                className={`${colSpanMap[field.colSize] || 'col-span-1'} other-static-classes`}
              >
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {field.label}
                  {field.required && <span className="text-red-500">*</span>}
                </label>

                {field.inputType === 'textarea' ? (
                  <textarea className="w-full rounded border px-3 py-2" rows={3} />
                ) : field.inputType === 'select' ? (
                  <select className="w-full rounded border px-3 py-2 text-gray-900 border-gray-300">
                    <option>Choose...</option>
                  </select>
                ) : field.inputType === 'checkbox' ? (
                  <input type="checkbox" className="mt-1" />
                ) : (
                  <input
                    type={field.inputType}
                    className="w-full rounded border px-3 py-2 text-gray-900 border-gray-300"
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