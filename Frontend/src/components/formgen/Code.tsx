// ============================================
// Code Tab - Display generated code
// ============================================
import { lazy, useEffect, useRef, useState } from "react";
import { Button } from "../ui/button";
import { cn } from "../../lib/utils";
import { toast } from "sonner";
import { Copy } from "lucide-react";
import type { GeneratedCode } from "../../hooks/useFormBuilder";
const MonacoEditor = lazy(() => import("@monaco-editor/react"));

export default function CodeTab({ code }: { code: GeneratedCode }) {
  const [showHtml, setShowHtml] = useState(true);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [editorHeight, setEditorHeight] = useState(480);

  useEffect(() => {
    const updateHeight = () => {
      if (containerRef.current) {
        const height = containerRef.current.clientHeight;
        if (height > 0) {
          setEditorHeight(height);
        }
      }
    };

    updateHeight();
    window.addEventListener("resize", updateHeight);

    // Small delay to ensure DOM is ready
    const timeout = setTimeout(updateHeight, 100);

    return () => {
      window.removeEventListener("resize", updateHeight);
      clearTimeout(timeout);
    };
  }, []);

  const handleCopy = async () => {
    const codeToCopy = showHtml ? code.html : code.javaScript;
    try {
      await navigator.clipboard.writeText(codeToCopy);
      toast.success("Code copied to clipboard");
    } catch (error) {
      // Fallback for older browsers or permission denied
      const textarea = document.createElement("textarea");
      textarea.value = codeToCopy;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.select();
      try {
        const success = document.execCommand("copy");
        if (success) {
          toast.success("Code copied to clipboard");
        } else {
          throw new Error("execCommand returned false");
        }
      } catch (fallbackError) {
        toast.error(
          `Failed to copy: ${fallbackError instanceof Error ? fallbackError.message : "Unknown error"}`,
        );
      } finally {
        document.body.removeChild(textarea);
      }
    }
  };

  return (
    <div className="flex h-full flex-col">
      <div className="border-b p-2 flex-shrink-0">
        <div className="flex gap-2">
          <Button
            onClick={() => setShowHtml(true)}
            variant={showHtml ? "default" : "outline"}
            size="sm"
            className={cn(
              "text-sm",
              showHtml && "bg-blue-600 hover:bg-blue-700",
            )}
          >
            HTML
          </Button>
          <Button
            onClick={() => setShowHtml(false)}
            variant={!showHtml ? "default" : "outline"}
            size="sm"
            className={cn(
              "text-sm",
              !showHtml && "bg-blue-600 hover:bg-blue-700",
            )}
          >
            JavaScript
          </Button>
          <Button
            onClick={handleCopy}
            variant="outline"
            size="sm"
            className="flex items-center gap-1 text-sm"
          >
            <Copy className="h-4 w-4" />
            Copy
          </Button>
        </div>
      </div>

      <div ref={containerRef} className="flex-1 min-h-0">
        <MonacoEditor
          height={editorHeight}
          language={showHtml ? "html" : "javascript"}
          theme="vs-dark"
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
