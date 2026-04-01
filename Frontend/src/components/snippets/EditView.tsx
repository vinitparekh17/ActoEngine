import { CreateSnippetRequest, MONACO_LANGUAGE_MAP, SNIPPET_LANGUAGES, SnippetDetail } from "@/types/snippet";
import { zodResolver } from "@hookform/resolvers/zod";
import { lazy, Suspense, useEffect, useRef, useState, Fragment } from "react";
import z from "zod";
import { Button } from "../ui/button";
import { Label } from "../ui/label";
import { Controller, useForm } from "react-hook-form";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "../ui/select";
import { Input } from "../ui/input";
import { Badge } from "../ui/badge";
import { Textarea } from "../ui/textarea";
import { cn } from "@/lib/utils";
import { CodeWindowFrame, langBadgeClass } from "./CodeWindowFrame";

const snippetSchema = z.object({
  title: z.string().min(1, "Title is required").max(200, "Max 200 characters"),
  language: z.string().min(1, "Language is required"),
  code: z.string().min(1, "Code is required"),
  description: z.string().max(500, "Max 500 characters").optional(),
  notes: z.string().max(2000, "Max 2000 characters").optional(),
  tagsInput: z.string().optional(),
});
type SnippetFormValues = z.infer<typeof snippetSchema>;

interface EditViewProps {
  isEditing: boolean;
  editingSnippet?: SnippetDetail | null;
  isPending: boolean;
  onSubmit: (data: CreateSnippetRequest) => void;
  onCancel: () => void;
}
const MonacoEditor = lazy(() => import("@monaco-editor/react"));

export function EditView({ isEditing, editingSnippet, isPending, onSubmit, onCancel }: EditViewProps) {
  const editorRef = useRef<unknown>(null);
  const [copied, setCopied] = useState(false);
  const [isEditorFullscreen, setIsEditorFullscreen] = useState(false);

  const { register, handleSubmit, control, watch, reset, formState: { errors } } = useForm<SnippetFormValues>({
    resolver: zodResolver(snippetSchema),
    defaultValues: { title: "", language: "SQL", code: "", description: "", notes: "", tagsInput: "" },
  });

  useEffect(() => {
    if (isEditing && editingSnippet) {
      reset({
        title: editingSnippet.title, language: editingSnippet.language, code: editingSnippet.code,
        description: editingSnippet.description ?? "", notes: editingSnippet.notes ?? "",
        tagsInput: editingSnippet.tags.join(", "),
      });
    } else if (!isEditing) {
      reset({ title: "", language: "SQL", code: "", description: "", notes: "", tagsInput: "" });
    }
  }, [isEditing, editingSnippet, reset]);

  const selectedLanguage = watch("language");
  const monacoLang = MONACO_LANGUAGE_MAP[selectedLanguage] ?? "plaintext";
  const codeValue = watch("code");

  const handleFormSubmit = (values: SnippetFormValues) => {
    const tags = (values.tagsInput ?? "").split(",").map((t) => t.trim()).filter(Boolean);
    onSubmit({
      title: values.title, language: values.language, code: values.code,
      description: values.description || undefined, notes: values.notes || undefined, tags,
    });
  };

  const handleCopyCode = async () => {
    try {
      await navigator.clipboard.writeText(codeValue || "");
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch { }
  };

  const titleBarCenter = (
    <Fragment>
      <Badge
        variant="outline"
        className={cn("text-[10px] font-mono font-bold uppercase tracking-wider px-2 py-0.5 rounded border pointer-events-none", langBadgeClass(selectedLanguage))}
      >
        {selectedLanguage}
      </Badge>
      <Controller
        name="language" control={control}
        render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange}>
            <SelectTrigger
              className="h-6 text-[11px] font-mono text-[#c0c0c0] bg-[#3c3c3c] border-[#555] hover:bg-[#454545] focus:ring-0 focus:ring-offset-0 rounded px-2 w-auto gap-1"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent className="font-mono text-xs">
              {SNIPPET_LANGUAGES.map((lang) => (
                <SelectItem key={lang} value={lang} className="text-xs">{lang}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      />
      <span className="text-[11px] text-[#606060] font-mono hidden sm:block">{monacoLang}</span>
    </Fragment>
  );

  // ─── Monaco Editor ───────────────────────────────────────────────────
  const monacoEditor = (
    <Controller
      name="code" control={control}
      render={({ field }) => (
        <Suspense fallback={<div className="w-full h-full flex items-center justify-center text-[#a0a0a0] font-mono text-sm animate-pulse">Loading Editor...</div>}>
          <MonacoEditor
            height="100%"
            language={monacoLang}
            theme="vs-dark"
            value={field.value}
            onChange={(v) => field.onChange(v ?? "")}
            onMount={(editor) => { editorRef.current = editor; }}
            options={{
              minimap: { enabled: false }, fontSize: 14,
              fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace",
              padding: { top: 20, bottom: 20 }, scrollBeyondLastLine: false,
              renderLineHighlight: "none", overviewRulerBorder: false,
              automaticLayout: true, lineNumbers: "on",
            }}
          />
        </Suspense>
      )}
    />
  );

  // ─── Fullscreen overlay ──────────────────────────────────────────────
  if (isEditorFullscreen) {
    return (
      <div className="fixed inset-0 z-[100] bg-background/95 backdrop-blur flex flex-col">
        <CodeWindowFrame
          language={selectedLanguage}
          monacoLang={monacoLang}
          onCopy={handleCopyCode}
          onToggleFullscreen={() => setIsEditorFullscreen(false)}
          isFullscreen={true}
          copied={copied}
          titleBarCenter={titleBarCenter}
        >
          {monacoEditor}
        </CodeWindowFrame>
      </div>
    );
  }

  return (
    <div className="w-full h-full animate-in fade-in duration-300">
      <form id="snippet-form" onSubmit={handleSubmit(handleFormSubmit)}>

        {/* ─── Page heading ─────────────────────────────────────────── */}
        <div className="px-6 lg:px-8 pt-6 pb-4 border-b bg-background/80 backdrop-blur">
          <div>
            <h1 className="text-xl font-bold tracking-tight text-foreground">
              {isEditing ? "Edit Snippet" : "Create New Snippet"}
            </h1>
            <p className="text-sm text-muted-foreground mt-0.5">Fill in the details and code for your reusable snippet.</p>
          </div>
        </div>

        {/* ─── 60/40 Split Layout ───────────────────────────────────── */}
        <div className="flex flex-col lg:flex-row h-[calc(100vh-14rem)] lg:h-[calc(100vh-11rem)]">

          {/* LEFT: 60% — Mac Terminal Window */}
          <div className="lg:w-[60%] h-64 lg:h-full p-4 lg:p-6 flex flex-col">
            <CodeWindowFrame
              language={selectedLanguage}
              monacoLang={monacoLang}
              onCopy={handleCopyCode}
              onToggleFullscreen={() => setIsEditorFullscreen(true)}
              isFullscreen={false}
              copied={copied}
              titleBarCenter={titleBarCenter}
              className={cn("flex-1", errors.code ? "border-destructive/60" : "border-[#3c3c3c]")}
            >
              {monacoEditor}
            </CodeWindowFrame>
            {errors.code && <p className="text-xs text-destructive mt-1.5">{errors.code.message}</p>}
          </div>

          {/* RIGHT: 40% — Form Fields */}
          <div className="lg:w-[40%] h-full border-t lg:border-t-0 lg:border-l border-border/50 overflow-y-auto">
            <div className="p-4 lg:p-6 space-y-5">

              {/* Title */}
              <div className="space-y-1.5">
                <Label htmlFor="edit-title" className="text-sm font-medium">
                  Title <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="edit-title"
                  placeholder="e.g. Postgres Upsert Pattern"
                  {...register("title")}
                  className={cn("h-9", errors.title && "border-destructive")}
                />
                {errors.title && <p className="text-xs text-destructive">{errors.title.message}</p>}
              </div>

              {/* Description */}
              <div className="space-y-1.5">
                <Label htmlFor="edit-desc" className="text-sm font-medium">Description</Label>
                <Input
                  id="edit-desc"
                  placeholder="Briefly explain what this snippet accomplishes..."
                  {...register("description")}
                  className="h-9"
                />
                {errors.description && <p className="text-xs text-destructive">{errors.description.message}</p>}
              </div>

              {/* Tags */}
              <div className="space-y-1.5">
                <Label htmlFor="edit-tags" className="text-sm font-medium">Tags</Label>
                <Input
                  id="edit-tags"
                  placeholder="e.g. database, utility, security"
                  {...register("tagsInput")}
                  className="h-9"
                />
                <p className="text-xs text-muted-foreground">Comma-separated keywords for searching.</p>
              </div>

              {/* Notes */}
              <div className="space-y-1.5">
                <Label htmlFor="edit-notes" className="text-sm font-medium">Implementation Notes</Label>
                <Textarea
                  id="edit-notes"
                  placeholder="Warnings, edge cases, or usage instructions..."
                  rows={5}
                  {...register("notes")}
                  className="resize-none text-sm"
                />
                {errors.notes && <p className="text-xs text-destructive">{errors.notes.message}</p>}
              </div>

              {/* Divider + actions (visible on mobile since header hides on mobile) */}
              <div className="pt-2 flex gap-2 lg:hidden">
                <Button type="button" variant="outline" className="flex-1 h-9" onClick={onCancel} disabled={isPending}>
                  Cancel
                </Button>
                <Button type="submit" className="flex-1 h-9 shadow-sm" disabled={isPending}>
                  {isPending ? "Saving..." : isEditing ? "Save Changes" : "Create Snippet"}
                </Button>
              </div>

            </div>
          </div>

        </div>
      </form>
    </div>
  );
}