// // ============================================
// // PHASE 2-B: CRUD Mapping UI
// // File: components/formBuilder/CrudMappingPanel.tsx
// // ============================================

// import { useState } from 'react';
// import { useCrudMapping } from '../../hooks/formBuilder/useCrudMapping';
// import { useFormBuilder } from '../../hooks/formBuilder/useFormBuilder';
// import { useSpCompliance, useSchemaCompliance } from '../../hooks/formBuilder/useFormContext';
// import type { SchemaMismatch } from '../../hooks/formBuilder/useFormContext';
// import { 
//   CheckCircle, 
//   XCircle, 
//   AlertTriangle, 
//   RefreshCw, 
//   Code, 
//   ExternalLink,
// } from 'lucide-react';
// import { Button } from '../ui/button';
// import type { CrudAction, SpBinding } from '../../hooks/formBuilder/formBuilder.types';

// export function CrudMappingPanel() {
//   const { config, initializeCrudMapping, crudMapping } = useFormBuilder();
//   const {
//     isVerifying,
//     isGenerating,
//     allSpsExist,
//     hasValidation,
//     isValid,
//     errors,
//     warnings,
//     verifySpExistence,
//     generateSp,
//     getBinding,
//   } = useCrudMapping(config?.id || '');

//   const [selectedAction, setSelectedAction] = useState<CrudAction | null>(null);

//   if (!config) {
//     return (
//       <div className="flex min-h-[400px] items-center justify-center">
//         <div className="text-center">
//           <p className="text-gray-600">No form initialized</p>
//         </div>
//       </div>
//     );
//   }

//   if (!crudMapping) {
//     return (
//       <div className="flex min-h-[400px] items-center justify-center">
//         <div className="text-center space-y-4">
//           <Code className="h-16 w-16 mx-auto text-gray-400" />
//           <h3 className="text-lg font-semibold">No CRUD Mapping</h3>
//           <p className="text-gray-600">Initialize CRUD mapping to connect form fields to stored procedures</p>
//           <Button onClick={initializeCrudMapping}>
//             Initialize CRUD Mapping
//           </Button>
//         </div>
//       </div>
//     );
//   }

//   return (
//     <div className="space-y-6">
//       {/* Header */}
//       <div className="flex items-center justify-between">
//         <div>
//           <h2 className="text-2xl font-bold">CRUD Mapping</h2>
//           <p className="text-sm text-gray-600">
//             Bind form fields to stored procedures for data operations
//           </p>
//         </div>

//         <div className="flex gap-2">
//           <Button
//             variant="outline"
//             onClick={verifySpExistence}
//             disabled={isVerifying}
//           >
//             <RefreshCw className={`h-4 w-4 mr-2 ${isVerifying ? 'animate-spin' : ''}`} />
//             Verify SPs
//           </Button>
//         </div>
//       </div>

//       {/* Status Overview */}
//       <div className="grid grid-cols-4 gap-4">
//         <StatusCard
//           title="Overall Status"
//           status={allSpsExist && isValid ? 'success' : 'error'}
//           description={allSpsExist && isValid ? 'All configured' : 'Issues found'}
//         />
//         <StatusCard
//           title="SP Existence"
//           status={allSpsExist ? 'success' : 'error'}
//           description={allSpsExist ? 'All exist' : 'SPs missing'}
//         />
//         <StatusCard
//           title="Validation"
//           status={isValid ? 'success' : 'warning'}
//           description={isValid ? 'Valid' : `${errors.length} errors`}
//         />
//         <StatusCard
//           title="Warnings"
//           status={warnings.length === 0 ? 'success' : 'warning'}
//           description={`${warnings.length} warnings`}
//         />
//       </div>

//       {/* Validation Messages */}
//       {hasValidation && !isValid && (
//         <div className="rounded-lg border border-red-200 bg-red-50 p-4">
//           <h4 className="font-semibold text-red-900 mb-2">Validation Errors</h4>
//           <ul className="space-y-1">
//             {errors.map((err, i) => (
//               <li key={i} className="text-sm text-red-700">
//                 <span className="font-medium capitalize">{err.action}:</span> {err.message}
//               </li>
//             ))}
//           </ul>
//         </div>
//       )}

//       {warnings.length > 0 && (
//         <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-4">
//           <h4 className="font-semibold text-yellow-900 mb-2">Warnings</h4>
//           <ul className="space-y-1">
//             {warnings.map((warn, i) => (
//               <li key={i} className="text-sm text-yellow-700">
//                 <span className="font-medium capitalize">{warn.action}:</span> {warn.message}
//               </li>
//             ))}
//           </ul>
//         </div>
//       )}

//       {/* CRUD Actions Grid */}
//       <div className="grid grid-cols-2 gap-4">
//         {(['create', 'read', 'update', 'delete'] as CrudAction[]).map(action => {
//           const binding = getBinding(action);
//           if (!binding) return null;

//           return (
//             <ActionCard
//               key={action}
//               action={action}
//               binding={binding}
//               isSelected={selectedAction === action}
//               onSelect={() => setSelectedAction(action)}
//               onGenerate={() => generateSp(action)}
//               isGenerating={isGenerating}
//             />
//           );
//         })}
//       </div>

//       {/* Binding Details Panel */}
//       {selectedAction && getBinding(selectedAction) && (
//         <BindingDetailsPanel
//           action={selectedAction}
//           binding={getBinding(selectedAction)!}
//           onClose={() => setSelectedAction(null)}
//         />
//       )}
//     </div>
//   );
// }

// // ============================================
// // Status Card Component
// // ============================================

// interface StatusCardProps {
//   title: string;
//   status: 'success' | 'warning' | 'error';
//   description: string;
// }

// function StatusCard({ title, status, description }: StatusCardProps) {
//   const icons = {
//     success: <CheckCircle className="h-5 w-5 text-green-600" />,
//     warning: <AlertTriangle className="h-5 w-5 text-yellow-600" />,
//     error: <XCircle className="h-5 w-5 text-red-600" />,
//   };

//   const colors = {
//     success: 'border-green-200 bg-green-50',
//     warning: 'border-yellow-200 bg-yellow-50',
//     error: 'border-red-200 bg-red-50',
//   };

//   return (
//     <div className={`rounded-lg border p-4 ${colors[status]}`}>
//       <div className="flex items-center gap-2 mb-1">
//         {icons[status]}
//         <h3 className="font-semibold text-sm">{title}</h3>
//       </div>
//       <p className="text-sm text-gray-700">{description}</p>
//     </div>
//   );
// }

// // ============================================
// // Action Card Component
// // ============================================

// interface ActionCardProps {
//   action: CrudAction;
//   binding: SpBinding;
//   isSelected: boolean;
//   onSelect: () => void;
//   onGenerate: () => void;
//   isGenerating: boolean;
// }

// function ActionCard({ 
//   action, 
//   binding, 
//   isSelected, 
//   onSelect, 
//   onGenerate,
//   isGenerating 
// }: ActionCardProps) {
//   const actionColors = {
//     create: 'border-green-300 bg-green-50',
//     read: 'border-blue-300 bg-blue-50',
//     update: 'border-yellow-300 bg-yellow-50',
//     delete: 'border-red-300 bg-red-50',
//   };

//   const actionIcons = {
//     create: '➕',
//     read: '👁️',
//     update: '✏️',
//     delete: '🗑️',
//   };

//   return (
//     <div
//       onClick={onSelect}
//       className={`cursor-pointer rounded-lg border-2 p-4 transition-all ${
//         isSelected
//           ? 'border-blue-500 ring-2 ring-blue-200'
//           : actionColors[action]
//       }`}
//     >
//       <div className="flex items-start justify-between mb-3">
//         <div className="flex items-center gap-2">
//           <span className="text-2xl">{actionIcons[action]}</span>
//           <h3 className="text-lg font-bold capitalize">{action}</h3>
//         </div>
//         {binding.exists ? (
//           <CheckCircle className="h-5 w-5 text-green-600" />
//         ) : (
//           <XCircle className="h-5 w-5 text-red-600" />
//         )}
//       </div>

//       <div className="space-y-2 text-sm">
//         <div>
//           <span className="font-medium text-gray-700">SP:</span>{' '}
//           <code className="text-xs bg-gray-100 px-1 py-0.5 rounded">{binding.spName}</code>
//         </div>
//         <div>
//           <span className="font-medium text-gray-700">Method:</span>{' '}
//           <span className={`font-medium ${
//             binding.method === 'POST' ? 'text-green-600' :
//             binding.method === 'GET' ? 'text-blue-600' :
//             binding.method === 'PUT' ? 'text-yellow-600' :
//             'text-red-600'
//           }`}>{binding.method}</span>
//         </div>
//         <div>
//           <span className="font-medium text-gray-700">Fields:</span>{' '}
//           <span>{binding.requiredFields.length} required</span>
//         </div>
//       </div>

//       {!binding.exists && (
//         <Button
//           size="sm"
//           variant="outline"
//           className="w-full mt-3"
//           onClick={(e) => {
//             e.stopPropagation();
//             onGenerate();
//           }}
//           disabled={isGenerating}
//         >
//           <Code className="h-4 w-4 mr-2" />
//           Generate SP
//         </Button>
//       )}
//     </div>
//   );
// }

// // ============================================
// // Binding Details Panel
// // ============================================

// interface BindingDetailsPanelProps {
//   action: CrudAction;
//   binding: SpBinding;
//   onClose: () => void;
// }

// function BindingDetailsPanel({ action, binding, onClose }: BindingDetailsPanelProps) {
//   const { config } = useFormBuilder();
//   const allFields = config?.groups.flatMap(g => g.fields) || [];

//   return (
//     <div className="rounded-xl border-2 border-blue-500 bg-white p-6">
//       <div className="flex items-center justify-between mb-4">
//         <h3 className="text-xl font-bold capitalize">{action} Binding Details</h3>
//         <button
//           onClick={onClose}
//           className="text-gray-400 hover:text-gray-600"
//         >
//           ✕
//         </button>
//       </div>

//       <div className="grid grid-cols-2 gap-6">
//         {/* Left: Configuration */}
//         <div className="space-y-4">
//           <div>
//             <label className="text-sm font-semibold text-gray-700 block mb-1">
//               Stored Procedure
//             </label>
//             <code className="block text-sm bg-gray-100 px-3 py-2 rounded">
//               {binding.spName}
//             </code>
//           </div>

//           <div>
//             <label className="text-sm font-semibold text-gray-700 block mb-1">
//               REST Endpoint
//             </label>
//             <div className="flex items-center gap-2">
//               <code className="flex-1 text-sm bg-gray-100 px-3 py-2 rounded">
//                 {binding.endpoint}
//               </code>
//               <Button size="sm" variant="ghost">
//                 <ExternalLink className="h-4 w-4" />
//               </Button>
//             </div>
//           </div>

//           <div>
//             <label className="text-sm font-semibold text-gray-700 block mb-1">
//               HTTP Method
//             </label>
//             <span className={`inline-block px-3 py-1 rounded font-medium ${
//               binding.method === 'POST' ? 'bg-green-100 text-green-700' :
//               binding.method === 'GET' ? 'bg-blue-100 text-blue-700' :
//               binding.method === 'PUT' ? 'bg-yellow-100 text-yellow-700' :
//               'bg-red-100 text-red-700'
//             }`}>
//               {binding.method}
//             </span>
//           </div>

//           <div>
//             <label className="text-sm font-semibold text-gray-700 block mb-1">
//               Status
//             </label>
//             <div className="flex items-center gap-2">
//               {binding.exists ? (
//                 <>
//                   <CheckCircle className="h-4 w-4 text-green-600" />
//                   <span className="text-sm text-green-700">SP exists in database</span>
//                 </>
//               ) : (
//                 <>
//                   <XCircle className="h-4 w-4 text-red-600" />
//                   <span className="text-sm text-red-700">SP not found</span>
//                 </>
//               )}
//             </div>
//             {binding.lastVerified && (
//               <p className="text-xs text-gray-500 mt-1">
//                 Last verified: {new Date(binding.lastVerified).toLocaleString()}
//               </p>
//             )}
//           </div>
//         </div>

//         {/* Right: Parameter Mapping */}
//         <div>
//           <label className="text-sm font-semibold text-gray-700 block mb-2">
//             Parameter Mapping
//           </label>
//           <div className="max-h-96 overflow-y-auto border rounded-lg">
//             <table className="w-full text-sm">
//               <thead className="bg-gray-50 sticky top-0">
//                 <tr>
//                   <th className="px-3 py-2 text-left font-semibold">Field</th>
//                   <th className="px-3 py-2 text-left font-semibold">Parameter</th>
//                 </tr>
//               </thead>
//               <tbody className="divide-y">
//                 {Object.entries(binding.parameterMap).map(([fieldId, paramName]) => {
//                   const field = allFields.find(f => f.id === fieldId);
//                   const isRequired = binding.requiredFields.includes(fieldId);
                  
//                   return (
//                     <tr key={fieldId} className={isRequired ? 'bg-yellow-50' : ''}>
//                       <td className="px-3 py-2">
//                         <div className="flex items-center gap-2">
//                           <span className="font-medium">
//                             {field?.label || fieldId}
//                           </span>
//                           {isRequired && (
//                             <span className="text-xs text-red-600">*</span>
//                           )}
//                         </div>
//                         <span className="text-xs text-gray-500">
//                           {field?.columnName || 'Unknown'}
//                         </span>
//                       </td>
//                       <td className="px-3 py-2">
//                         <code className="text-xs bg-gray-100 px-2 py-1 rounded">
//                           {paramName as string}
//                         </code>
//                       </td>
//                     </tr>
//                   );
//                 })}
//               </tbody>
//             </table>
//           </div>
          
//           <div className="mt-3 text-xs text-gray-600">
//             <p>✓ {binding.requiredFields.length} required field(s)</p>
//             <p>• {binding.optionalFields.length} optional field(s)</p>
//           </div>
//         </div>
//       </div>

//       {/* Custom Logic Section */}
//       {binding.customLogic && (
//         <div className="mt-4 p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
//           <h4 className="font-semibold text-yellow-900 mb-2">Custom Logic</h4>
//           <pre className="text-xs bg-white p-2 rounded overflow-x-auto">
//             {binding.customLogic}
//           </pre>
//         </div>
//       )}
//     </div>
//   );
// }

// // ============================================
// // Compliance Indicators Component
// // ============================================

// export function ComplianceIndicators() {
//   const { 
//     isCompliant, 
//     errorCount, 
//     validateCompliance,
//     isCheckingCompliance,
//   } = useSchemaCompliance();
  
//   const { 
//     allSpsExist, 
//     missingCount,
//     validateSpExistence,
//     isVerifyingSps,
//   } = useSpCompliance();

//   return (
//     <div className="flex items-center gap-2">
//       {/* Schema Compliance */}
//       <button
//         onClick={validateCompliance}
//         disabled={isCheckingCompliance}
//         className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-medium ${
//           isCompliant
//             ? 'bg-green-100 text-green-700 hover:bg-green-200'
//             : 'bg-red-100 text-red-700 hover:bg-red-200'
//         }`}
//         title="Schema Compliance"
//       >
//         {isCheckingCompliance ? (
//           <RefreshCw className="h-4 w-4 animate-spin" />
//         ) : isCompliant ? (
//           <CheckCircle className="h-4 w-4" />
//         ) : (
//           <XCircle className="h-4 w-4" />
//         )}
//         <span>Schema</span>
//         {!isCompliant && errorCount > 0 && (
//           <span className="ml-1 bg-red-200 px-1.5 rounded-full text-xs">
//             {errorCount}
//           </span>
//         )}
//       </button>

//       {/* SP Existence */}
//       <button
//         onClick={validateSpExistence}
//         disabled={isVerifyingSps}
//         className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-medium ${
//           allSpsExist
//             ? 'bg-green-100 text-green-700 hover:bg-green-200'
//             : 'bg-yellow-100 text-yellow-700 hover:bg-yellow-200'
//         }`}
//         title="Stored Procedures"
//       >
//         {isVerifyingSps ? (
//           <RefreshCw className="h-4 w-4 animate-spin" />
//         ) : allSpsExist ? (
//           <CheckCircle className="h-4 w-4" />
//         ) : (
//           <AlertTriangle className="h-4 w-4" />
//         )}
//         <span>SPs</span>
//         {!allSpsExist && missingCount > 0 && (
//           <span className="ml-1 bg-yellow-200 px-1.5 rounded-full text-xs">
//             {missingCount}
//           </span>
//         )}
//       </button>
//     </div>
//   );
// }

// // ============================================
// // Schema Compliance Panel Component
// // ============================================

// export function SchemaCompliancePanel() {
//   const {
//     complianceReport,
//     isCompliant,
//     errorCount,
//     warningCount,
//     syncWithSchema,
//     isSyncing,
//   } = useSchemaCompliance();

//   if (!complianceReport) {
//     return (
//       <div className="rounded-lg border p-4 text-center">
//         <p className="text-gray-600">No compliance check performed yet</p>
//       </div>
//     );
//   }

//   const errors = complianceReport.mismatches.filter((m: SchemaMismatch) => m.severity === 'error');
//   const warnings = complianceReport.mismatches.filter((m: SchemaMismatch) => m.severity === 'warning');

//   return (
//     <div className="space-y-4">
//       <div className="flex items-center justify-between">
//         <div>
//           <h3 className="text-lg font-semibold">Schema Compliance</h3>
//           <p className="text-sm text-gray-600">
//             Last checked: {complianceReport.lastChecked.toLocaleString()}
//           </p>
//         </div>

//         {!isCompliant && (
//           <Button
//             onClick={syncWithSchema}
//             disabled={isSyncing}
//           >
//             {isSyncing ? 'Syncing...' : 'Auto-Sync'}
//           </Button>
//         )}
//       </div>

//       {/* Overall Status */}
//       {isCompliant ? (
//         <div className="rounded-lg border border-green-200 bg-green-50 p-4">
//           <div className="flex items-center gap-2">
//             <CheckCircle className="h-5 w-5 text-green-600" />
//             <p className="font-semibold text-green-900">
//               Form is compliant with database schema
//             </p>
//           </div>
//         </div>
//       ) : (
//         <div className="rounded-lg border border-red-200 bg-red-50 p-4">
//           <div className="flex items-center gap-2 mb-2">
//             <XCircle className="h-5 w-5 text-red-600" />
//             <p className="font-semibold text-red-900">
//               {errorCount} error(s), {warningCount} warning(s) found
//             </p>
//           </div>
//           <p className="text-sm text-red-700">
//             Form structure doesn't match database schema. Review issues below.
//           </p>
//         </div>
//       )}

//       {/* Errors */}
//       {errors.length > 0 && (
//         <div className="space-y-2">
//           <h4 className="font-semibold text-red-900">Errors</h4>
//           {errors.map((mismatch: SchemaMismatch, i: number) => (
//             <MismatchCard key={i} mismatch={mismatch} />
//           ))}
//         </div>
//       )}

//       {/* Warnings */}
//       {warnings.length > 0 && (
//         <div className="space-y-2">
//           <h4 className="font-semibold text-yellow-900">Warnings</h4>
//           {warnings.map((mismatch: SchemaMismatch, i: number) => (
//             <MismatchCard key={i} mismatch={mismatch} />
//           ))}
//         </div>
//       )}
//     </div>
//   );
// }

// function MismatchCard({ mismatch }: { mismatch: SchemaMismatch }) {
//   const icons = {
//     error: <XCircle className="h-4 w-4 text-red-600" />,
//     warning: <AlertTriangle className="h-4 w-4 text-yellow-600" />,
//   };

//   const colors = {
//     error: 'border-red-200 bg-red-50',
//     warning: 'border-yellow-200 bg-yellow-50',
//   };

//   return (
//     <div className={`rounded-lg border p-3 ${colors[mismatch.severity]}`}>
//       <div className="flex items-start gap-2">
//         {icons[mismatch.severity]}
//         <div className="flex-1">
//           <p className="font-medium text-sm">
//             {mismatch.fieldLabel || mismatch.columnName}
//           </p>
//           <p className="text-xs text-gray-700 mt-1">
//             <span className="font-medium capitalize">{mismatch.issue.replace(/_/g, ' ')}</span>
//             {mismatch.expected && (
//               <span> - Expected: <code className="bg-white px-1 rounded">{mismatch.expected}</code></span>
//             )}
//             {mismatch.actual && (
//               <span> - Actual: <code className="bg-white px-1 rounded">{mismatch.actual}</code></span>
//             )}
//           </p>
//           {mismatch.suggestion && (
//             <p className="text-xs text-gray-600 mt-1 italic">
//               💡 {mismatch.suggestion}
//             </p>
//           )}
//         </div>
//       </div>
//     </div>
//   );
// }