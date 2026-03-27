import { useState, useMemo, useRef, useEffect } from 'react';
import { Copy, AlertCircle, RefreshCw, CheckSquare, Square, GripVertical, ChevronDown, ChevronRight, Eye, Code, LayoutTemplate } from 'lucide-react';
import Editor from '@monaco-editor/react';
import {
    buildPathGuard,
    type DisplayMode,
    type Entity,
    type PropMeta,
    useTemplateGenerator,
} from '../hooks/useTemplateGenerator';

type BootstrapVersion = '4' | '5';
type RepairStatus = 'idle' | 'applying' | 'applied' | 'failed';

type PreviewMessage = {
    source: 'ajr-preview';
    type: 'render-error' | 'render-success';
    message?: string;
};

type RepairResult = {
    changed: boolean;
    updatedTemplate: string;
    reason: string;
    patchedLoops: number;
};

const isSimplePath = (path: string): boolean => {
    return /^[A-Za-z_$][\w$]*(\.[A-Za-z_$][\w$]*)*$/.test(path);
};

const repairTemplateFromError = (template: string, errorMessage: string): RepairResult => {
    if (!/Cannot read propert(?:y|ies) of undefined/i.test(errorMessage)) {
        return {
            changed: false,
            updatedTemplate: template,
            reason: 'No automatic fix available for this error. Only undefined path errors are auto-repairable.',
            patchedLoops: 0,
        };
    }

    const forBlockRegex = /\{\{\s*for\s+([^\}\s]+)\s*\}\}([\s\S]*?)\{\{\s*\/for\s*\}\}/g;
    let patchedLoops = 0;

    const updatedTemplate = template.replace(forBlockRegex, (match, rawPath, _innerBlock, offset, fullTemplate) => {
        const path = String(rawPath).trim();
        if (!isSimplePath(path)) return match;

        const guard = buildPathGuard(path);
        const before = String(fullTemplate).slice(0, Number(offset)).replace(/\s+$/, '');
        if (before.endsWith(`{{if ${guard}}}`)) return match;

        patchedLoops += 1;
        return `{{if ${guard}}}${match}{{/if}}`;
    });

    if (patchedLoops === 0) {
        return {
            changed: false,
            updatedTemplate: template,
            reason: 'No automatic fix available for this error. No unguarded simple {{for path}} blocks were found to repair.',
            patchedLoops: 0,
        };
    }

    return {
        changed: true,
        updatedTemplate,
        reason: `Patched ${patchedLoops} loop(s) with full-path guards.`,
        patchedLoops,
    };
};

const escapeHtmlForTextarea = (value: string): string => {
    return value
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
};

export default function AdvancedJsRenderGenerator() {
    const [xmlInput, setXmlInput] = useState<string>('');
    const [entities, setEntities] = useState<Entity[]>([]);
    const [error, setError] = useState<string | null>(null);
    const [activeTab, setActiveTab] = useState<'blueprint' | 'actual' | 'code'>('blueprint');
    const [copied, setCopied] = useState(false);
    const [isDarkMode, setIsDarkMode] = useState(false);

    const [lowercaseKeys, setLowercaseKeys] = useState(true);
    const [tableClasses, setTableClasses] = useState('table table-bordered table-striped');
    const [bootstrapVersion, setBootstrapVersion] = useState<BootstrapVersion>('5');
    const [templateOverride, setTemplateOverride] = useState<string | null>(null);
    const [actualPreviewError, setActualPreviewError] = useState<string | null>(null);
    const [repairStatus, setRepairStatus] = useState<RepairStatus>('idle');
    const [repairMessage, setRepairMessage] = useState<string | null>(null);

    useEffect(() => {
        const detectDark = () => document.documentElement.classList.contains('dark');
        setIsDarkMode(detectDark());

        const observer = new MutationObserver(() => {
            setIsDarkMode(detectDark());
        });

        observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
        return () => observer.disconnect();
    }, []);

    useEffect(() => {
        const handlePreviewMessage = (event: MessageEvent<PreviewMessage>) => {
            const data = event.data;
            if (!data || typeof data !== 'object' || data.source !== 'ajr-preview') return;

            if (data.type === 'render-error') {
                setActualPreviewError(data.message || 'Unknown render error.');
                setRepairStatus(prev => (prev === 'applied' ? 'idle' : prev));
                return;
            }

            if (data.type === 'render-success') {
                setActualPreviewError(null);
                setRepairMessage(null);
                setRepairStatus(prev => (prev === 'failed' ? 'idle' : prev));
            }
        };

        window.addEventListener('message', handlePreviewMessage);
        return () => window.removeEventListener('message', handlePreviewMessage);
    }, []);

    // Drag and Drop state refs
    const dragEntityItem = useRef<number | null>(null);
    const dragEntityOverItem = useRef<number | null>(null);
    const dragPropItem = useRef<{ eIdx: number, pIdx: number } | null>(null);
    const dragPropOverItem = useRef<{ eIdx: number, pIdx: number } | null>(null);

    // --- Parser Logic ---
    const handleParseXml = () => {
        setError(null);
        setTemplateOverride(null);
        setActualPreviewError(null);
        setRepairStatus('idle');
        setRepairMessage(null);
        if (!xmlInput.trim()) return;

        try {
            const parser = new DOMParser();
            const doc = parser.parseFromString(xmlInput, 'text/xml');

            if (doc.getElementsByTagName('parsererror').length > 0) {
                throw new Error('Invalid XML structure detected.');
            }

            const root = doc.documentElement;
            const parsedEntities: Entity[] = [];

            const traverse = (node: Element, pathSegments: string[]) => {
                const children = Array.from(node.children);
                if (!children.length) return;

                const primitiveChildren = children.filter(c => c.children.length === 0);
                const complexChildren = children.filter(c => c.children.length > 0);

                if (primitiveChildren.length > 0) {
                    const currentPath = pathSegments.join('.');
                    const entityId = currentPath || 'ROOT';
                    const parentPath = pathSegments.slice(0, -1).join('.');

                    let existing = parsedEntities.find(e => e.id === entityId);

                    if (!existing) {
                        const props = Array.from(new Set(primitiveChildren.map(c => c.tagName))).map(tag => ({
                            id: tag,
                            label: tag.charAt(0).toUpperCase() + tag.slice(1).toLowerCase(),
                            active: true
                        }));

                        existing = {
                            id: entityId,
                            name: node.tagName,
                            path: currentPath,
                            parentPath: parentPath,
                            count: 1,
                            props: props,
                            displayMode: currentPath ? 'table' : 'summary',
                            isCollapsed: true
                        };
                        parsedEntities.push(existing);
                    } else {
                        existing.count++;
                        primitiveChildren.forEach(c => {
                            if (!existing!.props.find(p => p.id === c.tagName)) {
                                existing!.props.push({
                                    id: c.tagName,
                                    label: c.tagName.charAt(0).toUpperCase() + c.tagName.slice(1).toLowerCase(),
                                    active: true
                                });
                            }
                        });
                    }
                }

                complexChildren.forEach(c => {
                    traverse(c, [...pathSegments, c.tagName]);
                });
            };

            traverse(root, []);
            setEntities(parsedEntities);
        } catch (err: unknown) {
            const message = err instanceof Error ? err.message : String(err || 'Failed to parse XML');
            setError(message || 'Failed to parse XML');
        }
    };

    // Convert XML to JSON for Actual Preview Rendering
    const xmlToJson = useMemo(() => {
        if (!xmlInput) return {};
        try {
            const parser = new DOMParser();
            const xmlDoc = parser.parseFromString(xmlInput, 'text/xml');

            const parseNode = (node: Element): any => {
                if (node.children.length === 0) return node.textContent;
                const obj: any = {};
                for (let i = 0; i < node.children.length; i++) {
                    const child = node.children[i];
                    const key = lowercaseKeys ? child.tagName.toLowerCase() : child.tagName;
                    const childVal = parseNode(child);

                    if (obj[key] !== undefined) {
                        if (!Array.isArray(obj[key])) obj[key] = [obj[key]];
                        obj[key].push(childVal);
                    } else {
                        obj[key] = childVal;
                    }
                }
                return obj;
            };

            return parseNode(xmlDoc.documentElement);
        } catch {
            return {};
        }
    }, [xmlInput, lowercaseKeys]);

    // --- Mutators ---
    const updateEntity = (id: string, updates: Partial<Entity>) => {
        setEntities(prev => prev.map(e => e.id === id ? { ...e, ...updates } : e));
    };

    const updateProp = (entityId: string, propId: string, updates: Partial<PropMeta>) => {
        setEntities(prev => prev.map(e => {
            if (e.id !== entityId) return e;
            return {
                ...e,
                props: e.props.map(p => p.id === propId ? { ...p, ...updates } : p)
            };
        }));
    };

    // --- Drag & Drop Handlers ---
    const handleEntitySort = () => {
        if (dragEntityItem.current === null || dragEntityOverItem.current === null) return;
        const _entities = [...entities];
        const draggedItemContent = _entities.splice(dragEntityItem.current, 1)[0];
        _entities.splice(dragEntityOverItem.current, 0, draggedItemContent);
        dragEntityItem.current = null;
        dragEntityOverItem.current = null;
        setEntities(_entities);
    };

    const handlePropSort = (eIdx: number) => {
        if (dragPropItem.current === null || dragPropOverItem.current === null) return;
        if (dragPropItem.current.eIdx !== eIdx || dragPropOverItem.current.eIdx !== eIdx) return;

        setEntities(prev => {
            const newEntities = [...prev];
            const props = [...newEntities[eIdx].props];
            const draggedProp = props.splice(dragPropItem.current!.pIdx, 1)[0];
            props.splice(dragPropOverItem.current!.pIdx, 0, draggedProp);
            newEntities[eIdx] = { ...newEntities[eIdx], props };
            return newEntities;
        });

        dragPropItem.current = null;
        dragPropOverItem.current = null;
    };

    // --- Code Generation ---
    const generatedTemplate = useTemplateGenerator(entities, lowercaseKeys, tableClasses);

    const activeTemplate = templateOverride ?? generatedTemplate;

    useEffect(() => {
        setTemplateOverride(null);
        setActualPreviewError(null);
        setRepairStatus('idle');
        setRepairMessage(null);
    }, [generatedTemplate, bootstrapVersion]);

    const handleRepairTemplate = () => {
        if (!actualPreviewError || !activeTemplate) return;

        setRepairStatus('applying');
        const result = repairTemplateFromError(activeTemplate, actualPreviewError);

        if (!result.changed) {
            setRepairStatus('failed');
            setRepairMessage(result.reason);
            return;
        }

        setTemplateOverride(result.updatedTemplate);
        setActualPreviewError(null);
        setRepairMessage(result.reason);
        setRepairStatus('applied');
        setTimeout(() => setRepairStatus('idle'), 1500);
    };

    // Actual HTML Preview utilizing an iframe sandbox and a CSP-safe template renderer
    const actualPreviewIframeSrc = useMemo(() => {
        if (!activeTemplate || Object.keys(xmlToJson).length === 0) return '';
        const serializedTemplate = escapeHtmlForTextarea(activeTemplate);
        const serializedData = escapeHtmlForTextarea(JSON.stringify(xmlToJson));
        const bootstrapCssHref = bootstrapVersion === '4'
            ? 'https://cdn.jsdelivr.net/npm/bootstrap@4.6.2/dist/css/bootstrap.min.css'
            : 'https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css';

        return `
      <html>
        <head>
          <link href="${bootstrapCssHref}" rel="stylesheet">
          <style>body { padding: 1rem; font-family: system-ui, sans-serif; }</style>
        </head>
        <body>
          <div id="output">Rendering...</div>
          <textarea id="ajr-template" hidden>${serializedTemplate}</textarea>
          <textarea id="ajr-data" hidden>${serializedData}</textarea>
          <script src="/ajr-preview-runner.js"></script>
        </body>
      </html>
    `;
    }, [activeTemplate, xmlToJson, bootstrapVersion]);

    const canRepair = Boolean(actualPreviewError && activeTemplate);
    const repairButtonLabel = repairStatus === 'applying'
        ? 'Repairing...'
        : repairStatus === 'applied'
            ? 'Repaired'
            : 'Repair Template';

    return (
        <div className="min-h-screen bg-background p-6 font-sans text-foreground transition-colors">
            <div className="max-w-[1600px] mx-auto grid grid-cols-1 xl:grid-cols-12 gap-6 items-start">

                {/* Left Column: Data & Mapping */}
                <div className="xl:col-span-5 space-y-6">
                    <div className="bg-card rounded-lg shadow-sm border border-border">
                        <div className="bg-muted/40 border-b border-border p-3 flex justify-between items-center">
                            <h2 className="text-sm font-bold text-foreground uppercase">1. Data Source & Settings</h2>
                            <button onClick={handleParseXml} className="bg-indigo-600 hover:bg-indigo-500 text-white px-3 py-1.5 rounded-md text-xs font-bold uppercase flex items-center gap-2 transition-colors">
                                <RefreshCw className="w-3 h-3" /> Parse
                            </button>
                        </div>
                        <textarea
                            className="w-full h-40 p-4 resize-none outline-none font-mono text-xs text-foreground bg-card placeholder:text-muted-foreground"
                            placeholder="Paste XML Mock Data here..."
                            value={xmlInput}
                            onChange={(e) => setXmlInput(e.target.value)}
                        />
                        <div className="p-3 bg-muted/40 border-t border-border grid grid-cols-1 md:grid-cols-3 gap-4">
                            <div>
                                <label className="block text-xs font-bold text-muted-foreground mb-1">Table Classes</label>
                                <input
                                    type="text"
                                    value={tableClasses}
                                    onChange={e => setTableClasses(e.target.value)}
                                    className="w-full h-8 text-xs border border-input bg-background text-foreground px-2 rounded-md focus:border-indigo-500 focus:outline-none"
                                />
                            </div>
                            <div>
                                <label className="block text-xs font-bold text-muted-foreground mb-1">Bootstrap Version</label>
                                <select
                                    value={bootstrapVersion}
                                    onChange={e => setBootstrapVersion(e.target.value as BootstrapVersion)}
                                    className="w-full h-8 text-xs border border-input bg-background text-foreground px-2 rounded-md focus:border-indigo-500 focus:outline-none"
                                >
                                    <option value="4">Bootstrap 4</option>
                                    <option value="5">Bootstrap 5</option>
                                </select>
                            </div>
                            <div className="flex items-center md:pt-5">
                                <label className="flex items-center gap-2 cursor-pointer">
                                    <input type="checkbox" checked={lowercaseKeys} onChange={e => setLowercaseKeys(e.target.checked)} className="w-4 h-4 accent-indigo-600 rounded-sm" />
                                    <span className="text-xs font-bold text-foreground">Lowercase JSON Bindings</span>
                                </label>
                            </div>
                        </div>
                    </div>

                    {error && <div className="bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 text-red-700 dark:text-red-300 px-4 py-3 rounded-lg text-sm font-medium flex items-center gap-2"><AlertCircle className="w-4 h-4" /> {error}</div>}

                    {entities.length > 0 && (
                        <div className="bg-card rounded-lg shadow-sm border border-border max-h-[600px] flex flex-col">
                            <div className="bg-muted/40 border-b border-border p-3 z-10 shrink-0">
                                <h2 className="text-sm font-bold text-foreground uppercase">2. Arrangement & Mapping</h2>
                                <p className="text-[10px] text-muted-foreground mt-0.5">Drag handles to reorder blocks and columns.</p>
                            </div>
                            <div className="p-4 space-y-3 overflow-y-auto">
                                {entities.map((entity, eIdx) => (
                                    <div
                                        key={entity.id}
                                        className="border border-border rounded-lg bg-card overflow-hidden transition-all"
                                        draggable
                                        onDragStart={() => dragEntityItem.current = eIdx}
                                        onDragEnter={() => dragEntityOverItem.current = eIdx}
                                        onDragEnd={handleEntitySort}
                                        onDragOver={(e) => e.preventDefault()}
                                    >
                                        {/* Collapsible Header */}
                                        <div className="flex items-center gap-3 p-2 bg-muted/60 border-b border-border group">
                                            <div className="cursor-grab active:cursor-grabbing p-1 text-muted-foreground hover:text-foreground"><GripVertical className="w-4 h-4" /></div>
                                            <button onClick={() => updateEntity(entity.id, { isCollapsed: !entity.isCollapsed })} className="p-1 hover:bg-muted rounded-md text-muted-foreground">
                                                {entity.isCollapsed ? <ChevronRight className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                                            </button>
                                            <div className="flex-1">
                                                <h3 className="text-xs font-bold text-indigo-700 dark:text-indigo-400 font-mono">{entity.id === 'ROOT' ? '<ROOT SUMMARY>' : entity.id}</h3>
                                            </div>
                                            <select
                                                className="border border-input text-xs font-semibold p-1 focus:outline-none focus:border-indigo-500 rounded-md bg-background text-foreground"
                                                value={entity.displayMode}
                                                onChange={(e) => updateEntity(entity.id, { displayMode: e.target.value as DisplayMode })}
                                            >
                                                <option value="table">Table</option>
                                                <option value="input-table">Table (Inputs)</option>
                                                <option value="cards">Cards</option>
                                                <option value="summary">Summary</option>
                                                <option value="ignore">Ignore</option>
                                            </select>
                                        </div>

                                        {/* Properties List */}
                                        {!entity.isCollapsed && entity.displayMode !== 'ignore' && (
                                            <div className="p-2 bg-card space-y-1">
                                                <div className="flex text-[10px] font-bold text-muted-foreground uppercase px-6 pb-1">
                                                    <span className="flex-1">Source Node</span>
                                                    <span className="w-[45%]">Display Alias</span>
                                                </div>
                                                {entity.props.map((prop, pIdx) => (
                                                    <div
                                                        key={prop.id}
                                                        className={`flex items-center gap-2 p-1 rounded-md border transition-colors ${prop.active ? 'bg-indigo-50/30 dark:bg-indigo-950/20 border-border' : 'bg-muted/40 border-transparent opacity-60'}`}
                                                        draggable
                                                        onDragStart={(e) => { e.stopPropagation(); dragPropItem.current = { eIdx, pIdx }; }}
                                                        onDragEnter={(e) => { e.stopPropagation(); dragPropOverItem.current = { eIdx, pIdx }; }}
                                                        onDragEnd={(e) => { e.stopPropagation(); handlePropSort(eIdx); }}
                                                        onDragOver={(e) => e.preventDefault()}
                                                    >
                                                        <div className="cursor-grab active:cursor-grabbing text-muted-foreground/70 hover:text-foreground"><GripVertical className="w-3 h-3" /></div>
                                                        <button onClick={() => updateProp(entity.id, prop.id, { active: !prop.active })} className="text-muted-foreground hover:text-indigo-600 dark:hover:text-indigo-400 shrink-0">
                                                            {prop.active ? <CheckSquare className="w-4 h-4 text-indigo-600 dark:text-indigo-400" /> : <Square className="w-4 h-4" />}
                                                        </button>
                                                        <span className="text-xs font-mono font-medium text-foreground flex-1 truncate" title={prop.id}>{prop.id}</span>
                                                        <input
                                                            type="text"
                                                            value={prop.label}
                                                            onChange={(e) => updateProp(entity.id, prop.id, { label: e.target.value })}
                                                            className="w-[45%] text-xs border border-input p-1 rounded-md focus:border-indigo-500 focus:outline-none bg-background text-foreground"
                                                            disabled={!prop.active}
                                                        />
                                                    </div>
                                                ))}
                                            </div>
                                        )}
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                </div>

                {/* Right Column: Previews & Code */}
                <div className="xl:col-span-7 flex flex-col h-full min-h-[700px]">
                    <div className="flex gap-2 mb-4 border-b border-border pb-2">
                        {[
                            { id: 'blueprint', icon: LayoutTemplate, label: 'Blueprint Preview' },
                            { id: 'actual', icon: Eye, label: 'Actual Preview' },
                            { id: 'code', icon: Code, label: 'Monaco Editor' }
                        ].map(tab => (
                            <button
                                key={tab.id}
                                onClick={() => setActiveTab(tab.id as any)}
                                className={`flex items-center gap-2 px-4 py-2 text-sm font-bold uppercase rounded-t-md transition-colors border-b-2 ${activeTab === tab.id ? 'border-indigo-600 text-indigo-700 dark:text-indigo-400 bg-indigo-50/50 dark:bg-indigo-950/20' : 'border-transparent text-muted-foreground hover:bg-muted/70 hover:text-foreground'
                                    }`}
                            >
                                <tab.icon className="w-4 h-4" /> {tab.label}
                            </button>
                        ))}
                        <div className="ml-auto flex items-center gap-2">
                            {actualPreviewError && (
                                <button
                                    onClick={handleRepairTemplate}
                                    disabled={!canRepair || repairStatus === 'applying'}
                                    className="flex items-center gap-2 px-4 py-1.5 border border-amber-300 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 hover:bg-amber-100 dark:hover:bg-amber-950/40 text-amber-800 dark:text-amber-200 disabled:opacity-50 text-xs font-bold uppercase rounded-md transition-colors"
                                >
                                    <RefreshCw className={`w-4 h-4 ${repairStatus === 'applying' ? 'animate-spin' : ''}`} />
                                    {repairButtonLabel}
                                </button>
                            )}
                            <button onClick={() => { navigator.clipboard.writeText(activeTemplate); setCopied(true); setTimeout(() => setCopied(false), 2000); }} disabled={!activeTemplate} className="flex items-center gap-2 px-4 py-1.5 border border-border bg-card hover:bg-muted text-foreground disabled:opacity-50 text-xs font-bold uppercase rounded-md transition-colors">
                                {copied ? <CheckSquare className="w-4 h-4 text-green-600 dark:text-green-400" /> : <Copy className="w-4 h-4" />}
                                {copied ? 'Copied' : 'Copy Code'}
                            </button>
                        </div>
                    </div>

                    <div className="flex-1 bg-card border border-border shadow-sm rounded-lg overflow-hidden relative">
                        {activeTab === 'blueprint' && (
                            <div className="p-6 h-full overflow-auto bg-muted/30">
                                {/* Simplified Visual Blueprint (No logic execution, just structural) */}
                                {entities.map(e => e.displayMode !== 'ignore' && e.props.some(p => p.active) && (
                                    <div key={e.id} className="mb-6 p-4 border-2 border-dashed border-border bg-card rounded-lg">
                                        <div className="text-xs font-bold text-muted-foreground mb-2 uppercase">{e.name} ({e.displayMode})</div>
                                        <div className="flex gap-2 flex-wrap">
                                            {e.props.filter(p => p.active).map(p => (
                                                <span key={p.id} className="bg-indigo-50 dark:bg-indigo-950/20 border border-indigo-200 dark:border-indigo-900 text-indigo-700 dark:text-indigo-300 px-2 py-1 text-xs font-mono rounded-md">{p.label} <span className="opacity-50 text-[10px]">{'{{:'}{lowercaseKeys ? p.id.toLowerCase() : p.id}{'}}'}</span></span>
                                            ))}
                                        </div>
                                    </div>
                                ))}
                                {!entities.length && <div className="text-muted-foreground flex items-center justify-center h-full text-sm font-medium">Extract XML to view structure blueprint.</div>}
                            </div>
                        )}

                        {activeTab === 'actual' && (
                            <div className="h-full w-full bg-card flex flex-col">
                                {(actualPreviewError || repairMessage) && (
                                    <div className={`mx-4 mt-4 mb-2 rounded-md border px-3 py-2 text-xs ${actualPreviewError ? 'bg-red-50 dark:bg-red-950/30 border-red-200 dark:border-red-900 text-red-700 dark:text-red-300' : 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-900 text-amber-700 dark:text-amber-300'}`}>
                                        {actualPreviewError ? (
                                            <span>Error rendering template: {actualPreviewError}{' '}{canRepair ? 'Click "Repair Template" to apply an automatic path-safe fix.' : ''}</span>
                                        ) : (
                                            <span>{repairMessage}</span>
                                        )}
                                    </div>
                                )}
                                {actualPreviewIframeSrc ? (
                                    <iframe srcDoc={actualPreviewIframeSrc} sandbox="allow-scripts" className="w-full h-full border-0 flex-1" title="Actual Render Preview" />
                                ) : (
                                    <div className="text-muted-foreground flex items-center justify-center h-full text-sm font-medium">Extract valid XML payload to render Actual Preview.</div>
                                )}
                            </div>
                        )}

                        {activeTab === 'code' && (
                            <div className="h-full w-full">
                                <Editor
                                    height="100%"
                                    defaultLanguage="html"
                                    theme={isDarkMode ? 'vs-dark' : 'light'}
                                    value={activeTemplate}
                                    options={{ minimap: { enabled: false }, fontSize: 13, wordWrap: 'on' }}
                                />
                            </div>
                        )}
                    </div>
                </div>

            </div>
        </div>
    );
}
