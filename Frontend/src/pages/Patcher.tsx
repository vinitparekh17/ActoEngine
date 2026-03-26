import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { useApi, useApiMutation, useApiPost } from "@/hooks/useApi";
import { useAuthorization } from "@/hooks/useAuth";
import { useProject } from "@/hooks/useProject";
import { toast } from "sonner";
import {
  Settings, ChevronDown, Download, Copy,
  GitPullRequest, FileCode, CheckCircle2, AlertCircle,
  Clock, PackageCheck, LayoutDashboard, Share2, ShieldAlert, X,
  Search,
  ChevronRight,
  Package,
  Zap,
  Check,
  RefreshCw,
  PackagePlus
} from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import type {
  BulkMappingActionRequest,
  MappingStatus,
  PageMapping,
  PagePatchStatus,
  PatchGenerationResponse,
  PatchHistoryRecord,
  PatchStatusRequestPayload,
  ProjectPatchConfig,
  UpdateMappingRequest,
} from "@/types/patcher";
import { Alert, AlertDescription } from "@/components/ui/alert";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:5093/api";

type GroupedMappings = Record<string, PageMapping[]>;

function groupMappingsByPage(mappings: PageMapping[]): GroupedMappings {
  return mappings.reduce((acc, mapping) => {
    const key = `${mapping.domainName}/${mapping.pageName}`;
    if (!acc[key]) acc[key] = [];
    acc[key].push(mapping);
    return acc;
  }, {} as GroupedMappings);
}

function filterGroupedMappings(grouped: GroupedMappings, query: string): GroupedMappings {
  if (!query.trim()) return grouped;
  const lowerQuery = query.toLowerCase();

  const filtered: GroupedMappings = {};
  for (const [key, rows] of Object.entries(grouped)) {
    // If the domain or page name matches, keep the whole group
    if (key.toLowerCase().includes(lowerQuery)) {
      filtered[key] = rows;
    } else {
      // Otherwise, only keep the rows that match the SP name
      const matchedRows = rows.filter(r => r.storedProcedure.toLowerCase().includes(lowerQuery));
      if (matchedRows.length > 0) {
        filtered[key] = matchedRows;
      }
    }
  }
  return filtered;
}

function buildPatchPayload(projectId: number, pageKeys: string[], patchName: string, newPages: Record<string, boolean> = {}): PatchStatusRequestPayload {
  return {
    projectId,
    patchName: patchName.trim() || undefined,
    pageMappings: pageKeys.map((key) => {
      const [domainName, pageName] = key.split("/");
      return { domainName, pageName, serviceNames: [], isNewPage: !!newPages[key] };
    }),
  };
}

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffHours = diffMs / (1000 * 60 * 60);
  const diffDays = diffHours / 24;

  if (diffHours < 1) return "Just now";
  if (diffHours < 24) return `${Math.floor(diffHours)}h ago`;
  if (diffDays < 2) return "Yesterday";
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
}

function getDownloadFileName(response: Response, fallbackFileName: string): string {
  const disposition = response.headers.get("content-disposition");
  if (!disposition) return fallbackFileName;

  const encodedMatch = disposition.match(/filename\*\s*=\s*UTF-8''([^;]+)/i);
  if (encodedMatch?.[1]) {
    return decodeURIComponent(encodedMatch[1]);
  }

  const quotedMatch = disposition.match(/filename\s*=\s*"([^"]+)"/i);
  if (quotedMatch?.[1]) {
    return quotedMatch[1];
  }

  const plainMatch = disposition.match(/filename\s*=\s*([^;]+)/i);
  return plainMatch?.[1]?.trim() || fallbackFileName;
}

// ─── Sub-components ──────────────────────────────────────────────────────────

function ConfidenceBar({ value }: { value: number | null }) {
  if (value === null) return <span className="text-xs text-muted-foreground font-mono w-16 text-right">—</span>;
  const pct = Math.round(value * 100);
  const color = value >= 0.8 ? "bg-emerald-500" : value >= 0.6 ? "bg-amber-500" : "bg-muted-foreground/40";
  return (
    <div className="flex items-center gap-2 w-28" title={`Confidence: ${pct}%`}>
      <div className="flex-1 h-1.5 rounded-full bg-secondary overflow-hidden">
        <div className={cn("h-full rounded-full transition-all", color)} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-[10px] text-muted-foreground font-mono w-8 text-right">{pct}%</span>
    </div>
  );
}

// ─── Mapping Row ──────────────────────────────────────────────────────────────

function MappingRow({
  row,
  tab,
  onUpdate,
  onBulkShared,
  isUpdating,
}: {
  row: PageMapping;
  tab: MappingStatus;
  onUpdate: (mappingId: number, patch: Partial<UpdateMappingRequest>) => void;
  onBulkShared: (mappingId: number) => void;
  isUpdating: boolean;
}) {
  const isShared = row.mappingType === "shared";

  return (
    <div className="flex items-center gap-4 px-4 py-2.5 border-b border-border/40 last:border-0 hover:bg-accent/50 transition-colors group">
      <div className="flex-1 min-w-0 flex items-center gap-2">
        <FileCode className="w-4 h-4 text-muted-foreground/60" />
        <span className="text-sm font-mono text-foreground truncate">
          {row.storedProcedure}
        </span>
      </div>

      {/* <ConfidenceBar value={row.confidence ?? null} /> */}

      <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity focus-within:opacity-100 min-w-[140px] justify-end">
        {tab === "candidate" && (
          <Button
            size="sm"
            variant="outline"
            className="h-6 text-[11px] px-2.5 bg-emerald-50/50 text-emerald-700 border-emerald-200 hover:bg-emerald-100 hover:text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-400 dark:border-emerald-500/20 dark:hover:bg-emerald-500/20"
            disabled={isUpdating}
            onClick={() => onUpdate(row.mappingId, { status: "approved" })}
          >
            Approve
          </Button>
        )}
        {(tab === "approved" || tab === "candidate") && (
          <Button
            size="sm"
            variant="ghost"
            className="h-6 text-[11px] px-2.5 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
            disabled={isUpdating}
            onClick={() => onUpdate(row.mappingId, { status: "ignored" })}
          >
            Ignore
          </Button>
        )}
        {tab === "ignored" && (
          <Button
            size="sm"
            variant="outline"
            className="h-6 text-[11px] px-2.5 bg-amber-50/50 text-amber-700 border-amber-200 hover:bg-amber-100 dark:bg-amber-500/10 dark:text-amber-400 dark:border-amber-500/20"
            disabled={isUpdating}
            onClick={() => onUpdate(row.mappingId, { status: "candidate" })}
          >
            Restore
          </Button>
        )}
        {tab === "approved" && (
          <Button
            size="sm"
            variant={isShared ? "default" : "outline"}
            className={cn(
              "h-6 text-[11px] px-2.5 gap-1.5 transition-all",
              isShared
                ? "bg-blue-600 hover:bg-blue-700 text-white shadow-sm"
                : "border-border text-muted-foreground hover:text-foreground"
            )}
            disabled={isUpdating}
            onClick={() => onBulkShared(row.mappingId)}
          >
            <Share2 className="w-3 h-3" />
            {isShared ? "Shared" : "Share"}
          </Button>
        )}
      </div>
    </div>
  );
}

// ─── Page Group ───────────────────────────────────────────────────────────────
function PageGroup({
  pageKey, rows, tab, defaultOpen = true, onUpdate, onBulkApprove, onToggleShared, isUpdating, isBulkPending
}: {
  pageKey: string;
  rows: PageMapping[];
  tab: MappingStatus;
  defaultOpen?: boolean;
  onUpdate: (mappingId: number, patch: Partial<UpdateMappingRequest>) => void;
  onBulkApprove: (ids: number[]) => void;
  onToggleShared: (mappingId: number) => void;
  isUpdating: boolean;
  isBulkPending: boolean;
}) {
  const [domain, page] = pageKey.split("/");
  const [isOpen, setIsOpen] = useState(defaultOpen);

  return (
    <div className={cn("mb-4 border rounded-lg overflow-hidden bg-card shadow-sm transition-all", !isOpen && "mb-2")}>
      <div
        className={cn(
          "flex items-center justify-between px-4 py-2.5 bg-muted/40 transition-colors hover:bg-muted/60 cursor-pointer select-none",
          isOpen && "border-b backdrop-blur supports-[backdrop-filter]:bg-muted/20 sticky top-0 z-10"
        )}
        onClick={() => setIsOpen(!isOpen)}
      >
        <div className="flex items-center gap-3 overflow-hidden">
          <ChevronRight className={cn("w-4 h-4 text-muted-foreground shrink-0 transition-transform duration-200", isOpen && "rotate-90")} />
          <Badge variant="secondary" className="text-[10px] uppercase font-mono tracking-wider px-1.5 h-5 rounded-sm bg-background border shadow-sm shrink-0">
            {domain}
          </Badge>
          <span className="text-sm font-semibold tracking-tight truncate">{page}</span>
          <span className="text-[11px] text-muted-foreground shrink-0">
            {rows.length} file{rows.length !== 1 ? "s" : ""}
          </span>
        </div>

        {/* Actions - stop propagation so clicking buttons doesn't collapse the accordion */}
        <div className="flex items-center gap-2 pl-4" onClick={(e) => e.stopPropagation()}>
          {tab === "candidate" && isOpen && (
            <Button
              size="sm" variant="secondary"
              className="h-6 text-[11px] px-3 font-medium bg-background shadow-sm border hover:bg-accent"
              disabled={isBulkPending}
              onClick={() => {
                const ids = rows.filter((r) => (r.confidence ?? 0) >= 0.8).map((r) => r.mappingId);
                if (ids.length === 0) return toast.error("No mappings on this page meet the 80% threshold.");
                onBulkApprove(ids);
              }}
            >
              Approve ≥ 80%
            </Button>
          )}
        </div>
      </div>

      {isOpen && (
        <div className="flex flex-col divide-y divide-border/40 bg-background animate-in slide-in-from-top-2 fade-in duration-200">
          {rows.map((row) => (
            <MappingRow
              key={row.mappingId} row={row} tab={tab}
              onUpdate={onUpdate} onBulkShared={onToggleShared} isUpdating={isUpdating}
            />
          ))}
        </div>
      )}
    </div>
  );
}
// ─── History Row ──────────────────────────────────────────────────────────────

function HistoryRow({ item, onDownload, onDownloadScript, onCopyPath }: { item: PatchHistoryRecord; onDownload: (patchId: number) => void; onDownloadScript: (patchId: number) => void; onCopyPath: (path: string) => void; }) {
  const [expanded, setExpanded] = useState(false);
  const pages: any[] = item.pages ?? [];

  const formatPage = (p: any): string => {
    if (typeof p === 'string') return p;
    if (p && typeof p === 'object') return `${p.domainName}/${p.pageName}`;
    return "";
  };

  const firstPage = item.patchName || (pages.length > 0 ? formatPage(pages[0]) : `${item.domainName}/${item.pageName}`);
  const extraCount = item.patchName ? pages.length : (pages.length > 1 ? pages.length - 1 : 0);

  return (
    <div className="py-3 px-1 border-b border-border/40 last:border-0 group">
      <div className="flex items-start gap-3">
        <div className="mt-0.5 p-1.5 rounded-md bg-primary/10 text-primary border border-primary/20 shrink-0">
          <PackageCheck className="w-4 h-4" />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-foreground truncate">{firstPage}</span>
            {extraCount > 0 && (
              <Badge
                variant="secondary"
                className="text-[10px] h-4 px-1.5 cursor-pointer hover:bg-accent"
                onClick={() => setExpanded(!expanded)}
              >
                +{extraCount}
              </Badge>
            )}
          </div>
          <div className="flex items-center gap-2 text-[11px] text-muted-foreground mt-1.5 font-mono">
            <span>#{item.patchId}</span>
            <span className="text-border">•</span>
            <span className="flex items-center gap-1 font-sans">
              <Clock className="w-3 h-3" /> {formatRelativeTime(item.generatedAt)}
            </span>
            {(item as any).filesIncluded?.length != null && (
              <>
                <span className="text-border">•</span>
                <span className="font-sans">{(item as any).filesIncluded.length} files</span>
              </>
            )}
          </div>

          {expanded && pages.length > 1 && (
            <div className="mt-3 rounded-md bg-muted/40 border border-border/50 p-2 space-y-1.5">
              {pages.map((p, i) => {
                const pStr = formatPage(p);
                return (
                  <div key={pStr || i} className="text-[11px] text-muted-foreground font-mono flex items-center gap-2">
                    <div className="w-1 h-1 rounded-full bg-border" /> {pStr}
                  </div>
                );
              })}
            </div>
          )}
        </div>

        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
          <Button size="icon" variant="ghost" className="h-7 w-7 text-muted-foreground hover:text-foreground" onClick={() => onCopyPath(item.patchFilePath || "")}>
            <Copy className="h-3.5 w-3.5" />
          </Button>
          <Button size="icon" variant="ghost" className="h-7 w-7 text-amber-600 hover:text-amber-700 hover:bg-amber-500/10" onClick={() => onDownloadScript(item.patchId)} title="Download apply script">
            <FileCode className="h-3.5 w-3.5" />
          </Button>
          <Button size="icon" variant="ghost" className="h-7 w-7 text-primary hover:text-primary hover:bg-primary/10" onClick={() => onDownload(item.patchId)}>
            <Download className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PatcherPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const numericProjectId = Number(projectId);
  const navigate = useNavigate();
  const canPatch = useAuthorization("StoredProcedures:Create");
  const { selectedProject, selectedProjectId, selectProject } = useProject();

  const [activeTab, setActiveTab] = useState<MappingStatus>("candidate");
  const [searchQuery, setSearchQuery] = useState(""); // <-- NEW: Search state
  const [checkResults, setCheckResults] = useState<PagePatchStatus[]>([]);
  const [selectedPageKeys, setSelectedPageKeys] = useState<string[]>([]);
  const [newPages, setNewPages] = useState<Record<string, boolean>>({});
  const [patchName, setPatchName] = useState("");

  const [configOpen, setConfigOpen] = useState(false);
  const [shareWarningOpen, setShareWarningOpen] = useState(false);
  const [pendingShareId, setPendingShareId] = useState<number | null>(null);
  const [sharedExpanded, setSharedExpanded] = useState(false); // Collapsed by default strategically

  const [buildExpanded, setBuildExpanded] = useState(true);
  const [initialBuildStateSet, setInitialBuildStateSet] = useState(false);

  const [configForm, setConfigForm] = useState<ProjectPatchConfig>({
    projectRootPath: "", viewDirPath: "", scriptDirPath: "", patchDownloadPath: "",
  });

  useEffect(() => {
    if (!projectId || Number.isNaN(numericProjectId)) {
      navigate("/projects", { replace: true });
      return;
    }
    if (selectedProjectId !== numericProjectId) selectProject(numericProjectId);
  }, [navigate, numericProjectId, projectId, selectProject, selectedProjectId]);

  const mappings = useApi<PageMapping[]>(
    `/projects/${numericProjectId}/mappings?status=${activeTab}`,
    { enabled: !Number.isNaN(numericProjectId), queryKey: ["patcher-mappings", numericProjectId, activeTab] },
  );

  const approvedMappings = useApi<PageMapping[]>(
    `/projects/${numericProjectId}/mappings?status=approved`,
    { enabled: !Number.isNaN(numericProjectId), queryKey: ["patcher-approved", numericProjectId] },
  );

  const history = useApi<PatchHistoryRecord[]>(`/patcher/history/${numericProjectId}`, {
    enabled: !Number.isNaN(numericProjectId), queryKey: ["patcher-history", numericProjectId],
  });

  useEffect(() => {
    if (history.isSuccess && !initialBuildStateSet) {
      setBuildExpanded(history.data.length === 0);
      setInitialBuildStateSet(true);
    }
  }, [history.isSuccess, history.data, initialBuildStateSet]);

  const config = useApi<ProjectPatchConfig>(`/patcher/config/${numericProjectId}`, {
    enabled: !Number.isNaN(numericProjectId), queryKey: ["patcher-config", numericProjectId],
  });

  useEffect(() => { if (config.data) setConfigForm(config.data); }, [config.data]);

  // Derived & Filtered Data
  const rawGroupedMappings = useMemo(() => groupMappingsByPage(mappings.data || []), [mappings.data]);
  const groupedMappings = useMemo(() => filterGroupedMappings(rawGroupedMappings, searchQuery), [rawGroupedMappings, searchQuery]);

  const sharedApproved = useMemo(() => {
    const arr = (approvedMappings.data || []).filter((m) => m.mappingType === "shared");
    if (!searchQuery.trim()) return arr;
    const lowerQuery = searchQuery.toLowerCase();
    return arr.filter(m => m.storedProcedure.toLowerCase().includes(lowerQuery));
  }, [approvedMappings.data, searchQuery]);

  const pageSpecificApproved = useMemo(() => (approvedMappings.data || []).filter((m) => m.mappingType !== "shared"), [approvedMappings.data]);
  const rawGroupedApproved = useMemo(() => groupMappingsByPage(pageSpecificApproved), [pageSpecificApproved]);
  const groupedApproved = useMemo(() => filterGroupedMappings(rawGroupedApproved, searchQuery), [rawGroupedApproved, searchQuery]);

  const availablePageKeys = useMemo(() => Object.keys(rawGroupedApproved), [rawGroupedApproved]);

  // (Omitted standard hooks for brevity, identical to previous: useEffect for selectedPageKeys, updateMapping, etc.)
  useEffect(() => {
    setSelectedPageKeys((current) => {
      if (availablePageKeys.length === 0) return [];
      if (current.length === 0) return availablePageKeys;
      const retained = current.filter((k) => availablePageKeys.includes(k));
      return retained.length > 0 ? retained : availablePageKeys;
    });
  }, [availablePageKeys]);

  const refetchMappings = async () => { await Promise.all([mappings.refetch(), approvedMappings.refetch()]); };
  useEffect(() => { setCheckResults([]); }, [selectedPageKeys, activeTab, approvedMappings.data]);

  const updateMapping = useApiMutation<PageMapping, UpdateMappingRequest & { mappingId: number }>(
    `/projects/${numericProjectId}/mappings/:mappingId`, "PATCH",
    { showSuccessToast: false, invalidateKeys: [["patcher-approved", numericProjectId], ["patcher-mappings", numericProjectId, activeTab]], onSuccess: async (_data, variables) => { toast.success(`Mapping ${variables.status ? `marked as ${variables.status}` : "updated"}.`); await refetchMappings(); } }
  );
  const bulkAction = useApiPost<object, BulkMappingActionRequest>(`/projects/${numericProjectId}/mappings/bulk`, { showSuccessToast: false, invalidateKeys: [["patcher-approved", numericProjectId]], onSuccess: async (_data, variables) => { toast.success(`Bulk ${variables.action} applied to ${variables.ids.length} file(s).`); await refetchMappings(); } });
  const checkStatus = useApiPost<PagePatchStatus[], PatchStatusRequestPayload>("/patcher/check-status", { showSuccessToast: false, onSuccess: (data) => { const stale = data.filter((s) => s.needsRegeneration).length; toast.success(`${stale} of ${data.length} page(s) need regeneration.`); } });
  const generatePatch = useApiPost<PatchGenerationResponse, PatchStatusRequestPayload>("/patcher/generate", { showSuccessToast: false, onSuccess: (data) => { toast.success(`Patch #${data.patchId} generated with ${data.filesIncluded.length} file(s).`); history.refetch(); } });
  const saveConfig = useApiPost<string, ProjectPatchConfig>(`/patcher/config/${numericProjectId}`, { successMessage: "Patch configuration saved.", onSuccess: () => { config.refetch(); setConfigOpen(false); } });

  const runStatusCheck = async () => {
    if (selectedPageKeys.length === 0) return;
    setBuildExpanded(true);
    const payload = buildPatchPayload(numericProjectId, selectedPageKeys, patchName, newPages);
    const results = await checkStatus.mutateAsync(payload);
    setCheckResults(results);
  };

  const runGeneratePatch = async () => {
    if (selectedPageKeys.length === 0) return;
    setBuildExpanded(true);
    const payload = buildPatchPayload(numericProjectId, selectedPageKeys, patchName, newPages);
    await generatePatch.mutateAsync(payload);
  };

  const downloadFile = async (endpoint: string, fallbackFileName: string, successMessage: string) => {
    const response = await fetch(endpoint, { credentials: "include" });
    if (!response.ok) throw new Error("Download failed");

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const fileName = getDownloadFileName(response, fallbackFileName);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
    toast.success(successMessage);
  };

  const downloadPatch = async (patchId: number) => {
    try {
      await downloadFile(
        `${API_BASE_URL}/patcher/download/${patchId}`,
        `patch-${patchId}.zip`,
        "Patch downloaded successfully."
      );
    } catch (err: any) {
      toast.error(err.message || "Failed to download patch.");
    }
  };

  const downloadPatchScript = async (patchId: number) => {
    try {
      await downloadFile(
        `${API_BASE_URL}/patcher/download-script/${patchId}`,
        `patch-${patchId}.ps1`,
        "Patch script downloaded successfully."
      );
    } catch (err: any) {
      toast.error(err.message || "Failed to download patch script.");
    }
  };

  const handleToggleShared = (mappingId: number) => {
    const row = (approvedMappings.data || []).find(m => m.mappingId === mappingId);
    if (!row) return;
    if (row.mappingType === "shared") {
      updateMapping.mutate({ mappingId, mappingType: "page_specific" });
    } else {
      setPendingShareId(mappingId);
      setShareWarningOpen(true);
    }
  };

  const confirmShare = () => {
    if (pendingShareId !== null) {
      updateMapping.mutate({ mappingId: pendingShareId, mappingType: "shared" });
    }
    setShareWarningOpen(false);
    setPendingShareId(null);
  };

  if (!canPatch) {
    return (
      <div className="flex flex-col items-center justify-center h-screen bg-background">
        <ShieldAlert className="w-12 h-12 text-destructive mb-4" />
        <h2 className="text-xl font-semibold tracking-tight">Access Denied</h2>
        <p className="text-muted-foreground mt-2">You do not have permission to use the patch generator.</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-[calc(100vh-7rem)] w-full bg-background overflow-hidden font-sans">

      {/* Modals remain mostly identical, styled slightly for modern aesthetic */}
      <Dialog open={shareWarningOpen} onOpenChange={setShareWarningOpen}>
        <DialogContent className="sm:max-w-[425px]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-amber-600">
              <AlertCircle className="h-5 w-5" /> Shared Procedure Warning
            </DialogTitle>
            <DialogDescription className="pt-3 text-sm">
              Making this procedure shared means it will be included in patches for <strong>ALL</strong> pages.
              <br /><br />
              It appears this stored procedure is already mapped to other pages. Sharing it will make those local mappings redundant.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="mt-4">
            <Button variant="outline" onClick={() => { setShareWarningOpen(false); setPendingShareId(null); }}>Cancel</Button>
            <Button variant="default" className="bg-amber-600 hover:bg-amber-700 text-white" onClick={confirmShare}>
              Make Shared
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={configOpen} onOpenChange={setConfigOpen}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Patch Configuration</DialogTitle>
            <DialogDescription>Setup global paths for patch generation. Saved per project.</DialogDescription>
          </DialogHeader>
          <div className="space-y-5 py-4">
            <Alert className="bg-blue-50/50 text-blue-800 border-blue-200 dark:bg-blue-950/30 dark:border-blue-900/50 dark:text-blue-300">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription className="text-[13px] leading-relaxed">These paths dictate where the patch engine looks for templates and saves output on the server.</AlertDescription>
            </Alert>
            <div className="space-y-4">
              {[
                { key: "projectRootPath" as const, label: "Project Root Path", placeholder: "e.g. C:\\Projects\\MyApp" },
                { key: "viewDirPath" as const, label: "View Directory", placeholder: "Relative to root" },
                { key: "scriptDirPath" as const, label: "Script Directory", placeholder: "Relative to root" },
                { key: "patchDownloadPath" as const, label: "Output Path", placeholder: "Where ZIPs are saved" },
              ].map(({ key, label, placeholder }) => (
                <div key={key} className="space-y-1.5">
                  <label className="text-xs font-semibold text-foreground tracking-tight">{label}</label>
                  <Input
                    value={configForm[key] || ""}
                    onChange={(e) => setConfigForm((prev) => ({ ...prev, [key]: e.target.value }))}
                    placeholder={placeholder}
                    className="font-mono text-xs h-9"
                  />
                </div>
              ))}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfigOpen(false)}>Cancel</Button>
            <Button onClick={() => saveConfig.mutate(configForm)} disabled={saveConfig.isPending}>
              {saveConfig.isPending ? "Saving..." : "Save Configuration"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Sticky Top Navigation */}
      <header className="flex-none h-14 border-b flex items-center justify-between px-4 lg:px-6 bg-background/95 backdrop-blur z-30 supports-[backdrop-filter]:bg-background/80">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2.5 text-sm font-semibold text-foreground">
            <div className="bg-primary/10 p-1.5 rounded-md">
              <GitPullRequest className="w-4 h-4 text-primary" />
            </div>
            Patcher Engine
          </div>
          <Separator orientation="vertical" className="h-5" />
          <Badge variant="secondary" className="font-medium text-xs rounded-sm px-2">
            {selectedProject?.projectName || "Select Project"}
          </Badge>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" asChild className="text-muted-foreground hover:text-foreground hidden sm:flex h-8">
            <Link to="/">
              <LayoutDashboard className="w-3.5 h-3.5 mr-2" />
              Dashboard
            </Link>
          </Button>
          <Separator orientation="vertical" className="h-4 mx-1 hidden sm:block" />
          <Button variant="outline" size="sm" onClick={() => setConfigOpen(true)} className="h-8 shadow-sm">
            <Settings className="w-3.5 h-3.5 mr-2" />
            Config
          </Button>
        </div>
      </header>

      {/* Main Workspace - strict flex layouts to bound scrolling */}
      <main className="flex-1 min-h-0 w-full max-w-[1920px] mx-auto p-4 lg:p-6 flex flex-col lg:flex-row gap-6">

        {/* Left Pane (Mappings) */}
        <div className="flex-1 flex flex-col min-w-0 bg-card border rounded-xl shadow-sm overflow-hidden">
          <Tabs value={activeTab} onValueChange={(v) => { setActiveTab(v as MappingStatus); setSearchQuery(""); }}
            className="flex-1 flex flex-col min-h-0"> {/* min-h-0 is critical here */}

            {/* HEADER: Tightened padding and gap */}
            <div className="flex-none px-4 py-3 border-b bg-muted/5 flex flex-col gap-3">
              <div className="flex items-start justify-between">
                <div className="space-y-0.5">
                  <h2 className="text-sm font-semibold tracking-tight text-foreground">Review Mappings</h2>
                  <p className="text-[12px] text-muted-foreground">Verify and approve discovered relations before patching.</p>
                </div>
              </div>

              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <TabsList className="h-8 w-full sm:w-auto bg-muted/50 p-1">
                  <TabsTrigger value="candidate" className="text-[11px] px-3 h-6">
                    Candidates
                    {activeTab === "candidate" && mappings.data && (
                      <Badge variant="secondary" className="ml-2 h-3.5 px-1 min-w-4 text-[9px] bg-background">
                        {mappings.data.length}
                      </Badge>
                    )}
                  </TabsTrigger>
                  <TabsTrigger value="approved" className="text-[11px] px-3 h-6">Approved</TabsTrigger>
                  <TabsTrigger value="ignored" className="text-[11px] px-3 h-6">Ignored</TabsTrigger>
                </TabsList>

                <div className="relative w-full sm:w-64">
                  <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground/60" />
                  <Input
                    placeholder="Filter domain, page or SP..."
                    className="h-8 pl-8 text-[12px] bg-background shadow-sm"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                  />
                </div>
              </div>
            </div>

            {/* SCROLL AREA: flex-1 and min-h-0 allow it to fill and scroll */}
            <ScrollArea className="flex-1 min-h-0">
              <div className="p-4">
                <TabsContent value={activeTab} className="mt-0 border-none p-0 outline-none">
                  {/* Content logic remains same, but ensured height doesn't break */}
                  {(activeTab === "candidate" || activeTab === "ignored") && (
                    <div className="space-y-1">
                      {mappings.isLoading ? (
                        <div className="space-y-3">
                          <Skeleton className="h-12 w-full rounded-lg" />
                          <Skeleton className="h-12 w-full rounded-lg" />
                        </div>
                      ) : (
                        <>
                          {Object.entries(groupedMappings).map(([pageKey, rows]) => (
                            <PageGroup
                              key={pageKey} pageKey={pageKey} rows={rows} tab={activeTab}
                              defaultOpen={activeTab === "candidate" || !!searchQuery}
                              onUpdate={(id, patch) => updateMapping.mutate({ mappingId: id, ...patch })}
                              onBulkApprove={(ids) => bulkAction.mutate({ ids, action: "approve" })}
                              onToggleShared={handleToggleShared}
                              isUpdating={updateMapping.isPending} isBulkPending={bulkAction.isPending}
                            />
                          ))}
                          {/* ... Empty State ... */}
                          {Object.keys(groupedMappings).length === 0 && (
                            <div className="py-20 text-center">
                              <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mx-auto mb-4">
                                <GitPullRequest className="w-6 h-6 text-muted-foreground/40" />
                              </div>
                              <p className="text-sm font-medium text-foreground">No mappings found</p>
                              <p className="text-xs text-muted-foreground mt-1 px-8">
                                {activeTab === "candidate"
                                  ? "No candidate mappings to show. Try generating some or check your filters."
                                  : "No ignored mappings found."}
                              </p>
                            </div>
                          )}
                        </>
                      )}
                    </div>
                  )}

                  {activeTab === "approved" && (
                    <div className="space-y-1">
                      {approvedMappings.isLoading ? (
                        <div className="space-y-4">
                          <Skeleton className="h-14 w-full rounded-xl" />
                          <Skeleton className="h-14 w-full rounded-xl" />
                        </div>
                      ) : (
                        <>
                          {sharedApproved.length > 0 && (
                            <div className="border border-blue-200 dark:border-blue-900/50 rounded-lg overflow-hidden bg-blue-50/40 dark:bg-blue-950/20 mb-4 shadow-sm transition-all">
                              <button
                                className="w-full flex items-center justify-between px-4 py-3 hover:bg-blue-100/50 dark:hover:bg-blue-900/30 transition-colors focus:outline-none"
                                onClick={() => setSharedExpanded(!sharedExpanded)}
                              >
                                <div className="flex items-center gap-2.5">
                                  <div className="bg-blue-100 dark:bg-blue-900/50 p-1 rounded-md text-blue-600 dark:text-blue-400">
                                    <Share2 className="w-3.5 h-3.5" />
                                  </div>
                                  <span className="text-sm font-semibold tracking-tight text-blue-900 dark:text-blue-200">Shared Stored Procedures</span>
                                </div>
                                <div className="flex items-center gap-3">
                                  <Badge variant="secondary" className="bg-blue-100 text-blue-700 border-none dark:bg-blue-900 dark:text-blue-300 rounded-sm">
                                    {sharedApproved.length} global
                                  </Badge>
                                  <ChevronRight className={cn("w-4 h-4 text-blue-500 transition-transform duration-200", sharedExpanded && "rotate-90")} />
                                </div>
                              </button>

                              {sharedExpanded && (
                                <div className="p-4 pt-2 flex flex-wrap gap-2 animate-in slide-in-from-top-2 fade-in duration-200">
                                  {sharedApproved.map((m) => (
                                    <div key={m.mappingId} className="flex items-center bg-background border border-blue-200 dark:border-blue-800 rounded-md shadow-sm overflow-hidden pl-2.5 pr-0.5 py-1">
                                      <span className="text-blue-800 dark:text-blue-200 font-mono text-xs mr-2">{m.storedProcedure}</span>
                                      <button onClick={(e) => { e.stopPropagation(); handleToggleShared(m.mappingId); }} className="text-muted-foreground hover:text-destructive hover:bg-destructive/10 rounded p-1">
                                        <X className="w-3 h-3" />
                                      </button>
                                    </div>
                                  ))}
                                </div>
                              )}
                            </div>
                          )}

                          {Object.entries(groupedApproved).map(([pageKey, rows]) => (
                            <PageGroup
                              key={pageKey} pageKey={pageKey} rows={rows} tab="approved"
                              defaultOpen={!!searchQuery} // strategically default to collapsed unless they are searching
                              onUpdate={(id, patch) => updateMapping.mutate({ mappingId: id, ...patch })}
                              onBulkApprove={(ids) => bulkAction.mutate({ ids, action: "approve" })}
                              onToggleShared={handleToggleShared}
                              isUpdating={updateMapping.isPending} isBulkPending={bulkAction.isPending}
                            />
                          ))}

                          {Object.keys(groupedApproved).length === 0 && sharedApproved.length === 0 && (
                            <div className="flex flex-col items-center justify-center py-20 text-center border border-dashed rounded-xl bg-muted/20">
                              <FileCode className="w-8 h-8 text-muted-foreground/40 mb-3" />
                              <p className="text-sm font-medium text-foreground">
                                {searchQuery ? "No matching mappings found" : "No approved mappings"}
                              </p>
                            </div>
                          )}
                        </>
                      )}
                    </div>
                  )}
                </TabsContent>
              </div>
            </ScrollArea>
          </Tabs>
        </div>

        {/* Right Pane (Generate & History) */}
        <div className="w-full lg:w-1/2 xl:w-1/2 shrink-0 flex flex-col gap-4 min-h-0">

          {/* Available Patches — PRIMARY */}
          <Card className="flex-1 min-h-0 shadow-md flex flex-col overflow-hidden border-t-0 sm:border-t bg-gradient-to-b from-card to-background">
            <CardHeader className="flex-none h-11 border-b bg-muted/10 px-3 py-0 flex flex-row items-center justify-between space-y-0">
              {/* Left Side: Title & Icon */}
              <div className="flex items-center gap-2 overflow-hidden">
                <div className="p-1 shrink-0">
                  <Package className="w-3.5 h-3.5 text-primary" />
                </div>
                <CardTitle className="text-[11px] font-bold uppercase tracking-tight text-foreground truncate">
                  Available Patches
                </CardTitle>
              </div>

              {/* Right Side: Badge - Now guaranteed on the same line */}
              {history.data && (
                <div className="flex items-center gap-2 shrink-0">
                  <div className="h-4 w-[1px] bg-border mx-1" /> {/* Subtle Separator */}
                  <Badge
                    variant="secondary"
                    className="h-5 px-1.5 font-mono text-[10px] rounded-md bg-muted/50 text-muted-foreground border-none"
                  >
                    {history.data.length} patches
                  </Badge>
                </div>
              )}
            </CardHeader>

            {/* Enhanced List with better visual hierarchy */}
            <ScrollArea className="flex-1 min-h-0 bg-muted/5">
              <div className="p-4">
                {history.isLoading ? (
                  <div className="space-y-3">
                    {[1, 2, 3].map(i => (
                      <div key={i} className="p-3 border rounded-xl bg-card/50 space-y-2">
                        <Skeleton className="h-4 w-1/3" />
                        <Skeleton className="h-3 w-full" />
                      </div>
                    ))}
                  </div>
                ) : (history.data || []).length === 0 ? (
                  <div className="py-20 text-center flex flex-col items-center">
                    <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mb-4">
                      <PackageCheck className="w-6 h-6 text-muted-foreground/40" />
                    </div>
                    <p className="text-sm font-medium text-foreground">No patches found</p>
                    <p className="text-xs text-muted-foreground mt-1 px-8">Complete a build below to see your generated patches here.</p>
                  </div>
                ) : (
                  <div className="grid gap-2">
                    {(history.data || []).map((item) => (
                      <div key={item.patchId} className="group relative">
                        {/* We wrap HistoryRow or enhance it to feel more "clickable" */}
                        <HistoryRow
                          item={item}
                          onDownload={downloadPatch}
                          onDownloadScript={downloadPatchScript}
                          onCopyPath={(path) => {
                            navigator.clipboard.writeText(path);
                            toast.success("Server path copied");
                          }}
                        />
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </ScrollArea>
          </Card>

          {/* Build Patch — Utility Card with "Active" state focus */}
          <Card className={cn(
            "flex-none shadow-lg flex flex-col overflow-hidden transition-all duration-300 border-2",
            buildExpanded ? "border-primary/20 ring-4 ring-primary/5" : "border-transparent"
          )}>
            {/* Collapsed/Expanded Header Action Bar */}
            <div
              className={cn(
                "flex items-center justify-between p-3 px-4 cursor-pointer hover:bg-muted/10 transition-colors select-none",
                buildExpanded && "border-b bg-muted/10 hover:bg-muted/10"
              )}
              onClick={() => setBuildExpanded(!buildExpanded)}
            >
              <div className="flex items-center gap-3">
                <div className="bg-primary/10 p-1.5 rounded-md text-primary shrink-0">
                  <PackageCheck className="w-4 h-4" />
                </div>
                <div>
                  <h3 className="text-sm font-semibold tracking-tight">2. Build Patch</h3>
                  {!buildExpanded ? (
                    selectedPageKeys.length > 0 ? (
                      <p className="text-[11px] text-muted-foreground">{selectedPageKeys.length} module{selectedPageKeys.length !== 1 && "s"} selected</p>
                    ) : (
                      <p className="text-[11px] text-muted-foreground">No modules selected</p>
                    )
                  ) : (
                    <p className="text-[11px] text-muted-foreground">Compile approved mappings</p>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-3">
                {!buildExpanded && (
                  <div className="flex items-center gap-2 mr-1" onClick={(e) => e.stopPropagation()}>
                    <Button variant="outline" size="sm" className="h-7 text-[11px] px-3" onClick={runStatusCheck} disabled={checkStatus.isPending || selectedPageKeys.length === 0}>
                      {checkStatus.isPending ? "Checking..." : "Verify"}
                    </Button>
                    <Button size="sm" className="h-7 text-[11px] px-3" onClick={runGeneratePatch} disabled={generatePatch.isPending || checkStatus.isPending || selectedPageKeys.length === 0}>
                      {generatePatch.isPending ? "Building..." : "Build"}
                    </Button>
                  </div>
                )}
                <Button variant="ghost" size="icon" className="h-7 w-7 text-muted-foreground pointer-events-none shrink-0">
                  <ChevronDown className={cn("w-4 h-4 transition-transform duration-200", buildExpanded && "rotate-180")} />
                </Button>
              </div>
            </div>

            {buildExpanded && (
              <div className="p-4 space-y-4 bg-card animate-in fade-in slide-in-from-top-2 duration-200">
                {availablePageKeys.length === 0 ? (
                  <div className="rounded-lg border border-dashed p-3 bg-muted/20 text-center">
                    <p className="text-sm font-medium">Ready to build</p>
                    <p className="text-xs text-muted-foreground mt-0.5">Approve items in Step 1 first.</p>
                  </div>
                ) : (
                  <>
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-xs font-semibold text-foreground tracking-tight">Included Modules</span>
                        <span className="text-[10px] uppercase font-mono text-muted-foreground">{selectedPageKeys.length} selected</span>
                      </div>
                      <ScrollArea className="max-h-[120px] w-full rounded-md border bg-muted/20 p-2">
                        <div className="flex flex-wrap gap-1.5">
                          {availablePageKeys.map((key) => {
                            const isSelected = selectedPageKeys.includes(key);
                            return (
                              <div key={key} className={cn(
                                "flex items-center gap-1 bg-background border rounded-md px-2 py-1 shadow-sm transition-all",
                                isSelected ? "border-primary/30 ring-1 ring-primary/10" : "opacity-60 hover:opacity-100"
                              )}>
                                <button className={cn("text-xs font-medium focus:outline-none", isSelected ? "text-foreground" : "text-muted-foreground")}
                                  onClick={() => setSelectedPageKeys(curr => curr.includes(key) ? curr.filter(v => v !== key) : [...curr, key])}>
                                  {key.split("/")[1] || key}
                                </button>
                                {isSelected && (
                                  <button className={cn("ml-1 text-[9px] px-1.5 py-0.5 rounded-sm transition-colors focus:outline-none font-medium",
                                    newPages[key] ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground hover:bg-muted-foreground/20")}
                                    title="Flag as new module"
                                    onClick={() => setNewPages(curr => ({ ...curr, [key]: !curr[key] }))}>
                                    NEW
                                  </button>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      </ScrollArea>
                    </div>

                    {checkResults.length > 0 && (
                      <div className="space-y-2">
                        <span className="text-xs font-semibold text-foreground tracking-tight">Pre-flight Results</span>
                        <ScrollArea className="max-h-[140px] border rounded-md bg-background">
                          <div className="divide-y divide-border/50">
                            {checkResults.map((result) => (
                              <div key={`${result.domainName}/${result.pageName}`} className="flex items-center justify-between p-2.5">
                                <span className="font-mono text-[11px] truncate mr-2">{result.pageName}</span>
                                <div className={cn("flex items-center gap-1.5 text-[11px] font-medium whitespace-nowrap",
                                  result.needsRegeneration ? "text-amber-600 dark:text-amber-500" : "text-emerald-600 dark:text-emerald-500")}>
                                  {result.needsRegeneration ? <AlertCircle className="w-3 h-3" /> : <CheckCircle2 className="w-3 h-3" />}
                                  {result.reason}
                                </div>
                              </div>
                            ))}
                          </div>
                        </ScrollArea>
                      </div>
                    )}

                    {selectedPageKeys.length > 0 && (
                      <div className="space-y-1.5 pt-2 border-t border-border/50">
                        <label className="text-[11px] font-semibold text-foreground tracking-tight flex items-center gap-1.5">
                          Patch Name <span className="text-muted-foreground font-normal">(Optional)</span>
                        </label>
                        <Input
                          placeholder="e.g. Sprint-42 Build"
                          className="h-8 text-xs font-medium"
                          value={patchName}
                          onChange={(e) => setPatchName(e.target.value)}
                        />
                        {selectedPageKeys.length > 1 && (
                          <p className="text-[10px] text-muted-foreground leading-tight">
                            Recommended when patching multiple pages to easily identify this build later.
                          </p>
                        )}
                      </div>
                    )}

                    <div className="flex gap-2">
                      <Button variant="outline" className="flex-1 text-xs h-9" onClick={runStatusCheck}
                        disabled={checkStatus.isPending || selectedPageKeys.length === 0}>
                        {checkStatus.isPending ? "Checking..." : "Verify"}
                      </Button>
                      <Button className="flex-1 text-xs h-9" onClick={runGeneratePatch}
                        disabled={generatePatch.isPending || checkStatus.isPending || selectedPageKeys.length === 0}>
                        {generatePatch.isPending ? "Building..." : "Build Package"}
                      </Button>
                    </div>
                  </>
                )}
              </div>
            )}
          </Card>
        </div>
      </main>
    </div>
  );
}
