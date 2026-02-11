import React, { useState, useEffect } from "react";
import {
  useNavigate,
  useLocation,
  useSearchParams,
} from "react-router-dom";
import {
  Home,
  ArrowLeft,
  Search,
  AlertCircle,
  Ghost,
  FileQuestion,
  XCircle,
  Moon,
  Sun,
} from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";

export default function NotFoundPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();

  // Smart detection: Check for 'q', 'search', 'query', or 'id' to customize the message
  const searchQuery =
    searchParams.get("q") ||
    searchParams.get("search") ||
    searchParams.get("query") ||
    searchParams.get("id");

  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    // Trigger toast on mount for immediate feedback
    toast.error("Page not found", {
      description: `The route "${location.pathname}" could not be resolved.`,
    });
  }, [location.pathname]);

  const handleGoBack = () => navigate(-1);
  const handleGoHome = () => navigate("/");

  if (!mounted) return null;

  return (
    <div className="min-h-[calc(100dvh-8rem)] w-full bg-neutral-50/50 dark:bg-neutral-950 flex items-center justify-center p-4 md:p-8 font-sans transition-colors duration-300">
      <div className="w-full max-w-3xl animate-in fade-in zoom-in duration-500 slide-in-from-bottom-4">
        {/* Main Card */}
        <div className="bg-white dark:bg-neutral-900 rounded-3xl shadow-xl shadow-neutral-200/50 dark:shadow-neutral-900/50 border border-neutral-100 dark:border-neutral-800 overflow-hidden relative transition-colors duration-300">
          {/* Decorative Background Elements */}
          <div className="absolute top-0 left-0 w-full h-2 bg-gradient-to-r from-emerald-500 via-cyan-500 to-teal-600" />

          <div className="absolute top-10 right-10 w-32 h-32 bg-emerald-100 dark:bg-emerald-900/20 rounded-full blur-3xl opacity-50 pointer-events-none" />
          <div className="absolute bottom-10 left-10 w-40 h-40 bg-cyan-100 dark:bg-cyan-900/15 rounded-full blur-3xl opacity-50 pointer-events-none" />

          <div className="relative z-10 flex flex-col md:flex-row">
            {/* Illustration Section */}
            <div className="w-full md:w-5/12 bg-neutral-50/50 dark:bg-neutral-950/50 p-8 md:p-12 flex flex-col items-center justify-center border-b md:border-b-0 md:border-r border-neutral-100 dark:border-neutral-800 transition-colors duration-300">
              <div className="relative w-48 h-48 flex items-center justify-center">
                {/* Composition of Standard Lucide Icons */}
                <div className="absolute inset-0 bg-neutral-200/50 dark:bg-neutral-800/50 rounded-full blur-2xl animate-pulse" />
                <Ghost
                  className="w-32 h-32 text-neutral-300 dark:text-neutral-700 relative z-10 animate-[bounce_3s_infinite] transition-colors duration-300"
                  strokeWidth={1.5}
                />
                <div className="absolute -right-2 top-0 bg-white dark:bg-neutral-800 p-2 rounded-full shadow-lg border border-neutral-100 dark:border-neutral-700 rotate-12 animate-[pulse_4s_infinite] transition-colors duration-300">
                  <FileQuestion
                    className="w-8 h-8 text-amber-500"
                    strokeWidth={2}
                  />
                </div>
                <div className="absolute -left-2 bottom-4 bg-white dark:bg-neutral-800 p-2 rounded-full shadow-lg border border-neutral-100 dark:border-neutral-700 -rotate-12 transition-colors duration-300">
                  <AlertCircle
                    className="w-6 h-6 text-red-400"
                    strokeWidth={2}
                  />
                </div>
              </div>
              <div className="mt-6 text-center">
                <span className="inline-flex items-center justify-center px-3 py-1 rounded-full bg-neutral-200/50 dark:bg-neutral-800/50 text-neutral-600 dark:text-neutral-400 text-xs font-bold tracking-wider uppercase border border-transparent dark:border-neutral-700">
                  Error 404
                </span>
              </div>
            </div>

            {/* Content Section */}
            <div className="w-full md:w-7/12 p-8 md:p-12 flex flex-col justify-center text-left">
              <div className="space-y-4">
                <h1 className="text-3xl md:text-4xl font-extrabold text-neutral-900 dark:text-neutral-50 tracking-tight transition-colors duration-300">
                  Dead End.
                </h1>

                <p className="text-neutral-500 dark:text-neutral-400 text-lg leading-relaxed transition-colors duration-300">
                  We searched everywhere, but couldn't find the page you were
                  looking for.
                </p>

                {/* Dynamic Error Context for Mistyped Query */}
                {searchQuery && (
                  <div className="bg-amber-50 dark:bg-amber-950/30 border border-amber-100 dark:border-amber-900/50 rounded-lg p-4 flex items-start gap-3 mt-2 transition-colors duration-300">
                    <Search className="w-5 h-5 text-amber-500 shrink-0 mt-0.5" />
                    <div className="space-y-1">
                      <p className="text-sm font-medium text-amber-800 dark:text-amber-200">
                        Looking for "{searchQuery}"?
                      </p>
                      <p className="text-xs text-amber-600/80 dark:text-amber-400/80">
                        The query parameter{" "}
                        <code className="bg-amber-100/50 dark:bg-amber-900/50 px-1 py-0.5 rounded text-amber-900 dark:text-amber-100 font-mono transition-colors duration-300">
                          {searchQuery}
                        </code>{" "}
                        didn't return any valid results or routes.
                      </p>
                    </div>
                  </div>
                )}

                {!searchQuery && (
                  <div className="bg-neutral-50 dark:bg-neutral-950/50 border border-neutral-100 dark:border-neutral-800 rounded-lg p-3 transition-colors duration-300">
                    <div className="font-mono text-xs text-neutral-400 dark:text-neutral-500 flex items-center gap-2">
                      <XCircle className="w-3 h-3 text-red-400 dark:text-red-500/80" />
                      <span>GET {location.pathname}</span>
                    </div>
                  </div>
                )}
              </div>

              <div className="mt-8 flex flex-col sm:flex-row gap-3">
                <Button
                  onClick={handleGoHome}
                  className="w-full sm:w-auto gap-2"
                  size="lg"
                >
                  <Home className="w-4 h-4" />
                  Dashboard
                </Button>
                <Button
                  variant="outline"
                  onClick={handleGoBack}
                  className="w-full sm:w-auto gap-2"
                  size="lg"
                >
                  <ArrowLeft className="w-4 h-4" />
                  Go Back
                </Button>
              </div>

              <div className="mt-8 pt-6 border-t border-neutral-100 dark:border-neutral-800 transition-colors duration-300">
                <p className="text-xs text-neutral-400 dark:text-neutral-500 text-center sm:text-left">
                  If you believe this is a bug, please{" "}
                  <a
                    href="mailto:support@actoengine.com"
                    className="underline hover:text-neutral-600 dark:hover:text-neutral-300 transition-colors"
                  >
                    contact support
                  </a>
                  .
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// -----------------------------------------------------------------------------
// APP WRAPPER (For Preview Context)
// -----------------------------------------------------------------------------

function ThemeToggle() {
  const [isDark, setIsDark] = useState(false);

  const toggleTheme = () => {
    setIsDark(!isDark);
    document.documentElement.classList.toggle("dark");
  };

  return (
    <button
      onClick={toggleTheme}
      className="fixed top-4 right-4 z-50 p-2 rounded-full bg-white dark:bg-neutral-800 shadow-lg border border-neutral-200 dark:border-neutral-700 text-neutral-600 dark:text-neutral-400 hover:scale-110 transition-all duration-200"
      title="Toggle Theme for Preview"
    >
      {isDark ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
    </button>
  );
}
