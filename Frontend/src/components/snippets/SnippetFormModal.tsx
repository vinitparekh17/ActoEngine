import { lazy, Suspense, useEffect, useRef } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import type { CreateSnippetRequest, SnippetDetail } from "@/types/snippet";
import { SNIPPET_LANGUAGES, MONACO_LANGUAGE_MAP } from "@/types/snippet";

const MonacoEditor = lazy(() => import("@monaco-editor/react"));

const snippetSchema = z.object({
  title: z.string().min(1, "Title is required").max(200, "Max 200 characters"),
  language: z.string().min(1, "Language is required"),
  code: z.string().min(1, "Code is required"),
  description: z.string().max(500, "Max 500 characters").optional(),
  notes: z.string().optional(),
  tagsInput: z.string().optional(),
});

type SnippetFormValues = z.infer<typeof snippetSchema>;

interface SnippetFormModalProps {
  isOpen: boolean;
  isEditing: boolean;
  editingSnippet?: SnippetDetail | null;
  isPending: boolean;
  onSubmit: (data: CreateSnippetRequest) => void;
  onClose: () => void;
}

export function SnippetFormModal({
  isOpen,
  isEditing,
  editingSnippet,
  isPending,
  onSubmit,
  onClose,
}: SnippetFormModalProps) {
  const editorRef = useRef<unknown>(null);

  const {
    register,
    handleSubmit,
    control,
    watch,
    reset,
    formState: { errors },
  } = useForm<SnippetFormValues>({
    resolver: zodResolver(snippetSchema),
    defaultValues: {
      title: "",
      language: "SQL",
      code: "",
      description: "",
      notes: "",
      tagsInput: "",
    },
  });

  useEffect(() => {
    if (isOpen) {
      if (isEditing && editingSnippet) {
        reset({
          title: editingSnippet.title,
          language: editingSnippet.language,
          code: editingSnippet.code,
          description: editingSnippet.description ?? "",
          notes: editingSnippet.notes ?? "",
          tagsInput: editingSnippet.tags.join(", "),
        });
      } else {
        reset({
          title: "",
          language: "SQL",
          code: "",
          description: "",
          notes: "",
          tagsInput: "",
        });
      }
    }
  }, [isOpen, isEditing, editingSnippet, reset]);

  const selectedLanguage = watch("language");
  const monacoLang = MONACO_LANGUAGE_MAP[selectedLanguage] ?? "plaintext";

  const handleFormSubmit = (values: SnippetFormValues) => {
    const tags = (values.tagsInput ?? "")
      .split(",")
      .map((t) => t.trim())
      .filter(Boolean);

    onSubmit({
      title: values.title,
      language: values.language,
      code: values.code,
      description: values.description || undefined,
      notes: values.notes || undefined,
      tags,
    });
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden p-0">
        <DialogHeader className="px-6 pt-6 pb-4 border-b border-border/60 shrink-0">
          <DialogTitle>{isEditing ? "Edit Snippet" : "New Snippet"}</DialogTitle>
          <DialogDescription>
            {isEditing
              ? "Update the snippet details below."
              : "Add a reusable code snippet to the shared library."}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(handleFormSubmit)}
          className="flex flex-col flex-1 overflow-hidden"
        >
          <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
            {/* Title */}
            <div className="space-y-1.5">
              <Label htmlFor="title">
                Title <span className="text-destructive">*</span>
              </Label>
              <Input
                id="title"
                placeholder="e.g. Find SP by content, jqGrid to Excel"
                {...register("title")}
                className={errors.title ? "border-destructive" : ""}
              />
              {errors.title && (
                <p className="text-xs text-destructive">{errors.title.message}</p>
              )}
            </div>

            {/* Language + Description row */}
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <Label>
                  Language <span className="text-destructive">*</span>
                </Label>
                <Controller
                  name="language"
                  control={control}
                  render={({ field }) => (
                    <Select value={field.value} onValueChange={field.onChange}>
                      <SelectTrigger className={errors.language ? "border-destructive" : ""}>
                        <SelectValue placeholder="Select language" />
                      </SelectTrigger>
                      <SelectContent>
                        {SNIPPET_LANGUAGES.map((lang) => (
                          <SelectItem key={lang} value={lang}>
                            {lang}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}
                />
                {errors.language && (
                  <p className="text-xs text-destructive">{errors.language.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="description">Description</Label>
                <Input
                  id="description"
                  placeholder="Brief description of what this does"
                  {...register("description")}
                />
              </div>
            </div>

            {/* Code editor */}
            <div className="space-y-1.5">
              <Label>
                Code <span className="text-destructive">*</span>
              </Label>
              <Controller
                name="code"
                control={control}
                render={({ field }) => (
                  <div
                    className={`rounded-lg overflow-hidden border ${
                      errors.code ? "border-destructive" : "border-border/40"
                    } bg-[#1e1e1e]`}
                    style={{ height: 260 }}
                  >
                    <Suspense
                      fallback={
                        <div className="p-4 space-y-3">
                          {Array.from({ length: 8 }).map((_, i) => (
                            <Skeleton
                              key={i}
                              className="h-3.5 opacity-10 bg-primary"
                              style={{ width: `${40 + (i * 13) % 50}%` }}
                            />
                          ))}
                        </div>
                      }
                    >
                      <MonacoEditor
                        height="260px"
                        language={monacoLang}
                        theme="vs-dark"
                        value={field.value}
                        onChange={(v) => field.onChange(v ?? "")}
                        onMount={(editor) => {
                          editorRef.current = editor;
                        }}
                        options={{
                          minimap: { enabled: false },
                          fontSize: 13,
                          fontFamily: "'JetBrains Mono', 'Fira Code', monospace",
                          wordWrap: "on",
                          scrollBeyondLastLine: false,
                          padding: { top: 12, bottom: 12 },
                          lineHeight: 21,
                          renderLineHighlight: "all",
                        }}
                      />
                    </Suspense>
                  </div>
                )}
              />
              {errors.code && (
                <p className="text-xs text-destructive">{errors.code.message}</p>
              )}
            </div>

            {/* Notes */}
            <div className="space-y-1.5">
              <Label htmlFor="notes">Notes / Warnings / Instructions</Label>
              <Textarea
                id="notes"
                placeholder="Usage instructions, known gotchas, prerequisites, example output..."
                rows={3}
                {...register("notes")}
                className="resize-none"
              />
            </div>

            {/* Tags */}
            <div className="space-y-1.5">
              <Label htmlFor="tagsInput">Tags</Label>
              <Input
                id="tagsInput"
                placeholder="Comma-separated, e.g. jqGrid, Excel, Audit, Performance"
                {...register("tagsInput")}
              />
              <p className="text-xs text-muted-foreground">Separate tags with commas</p>
            </div>
          </div>

          <div className="flex justify-end gap-2 px-6 py-4 border-t border-border/60 shrink-0">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? "Saving..." : isEditing ? "Save Changes" : "Add Snippet"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
