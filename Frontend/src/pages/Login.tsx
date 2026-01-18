import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { useAuth } from "../hooks/useAuth";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Database, Zap, Shield, Code2, AlertCircle, CheckCircle2, ArrowRight, Eye, EyeOff } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";

// --- Schema ---
const loginSchema = z.object({
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const { login, isLoggingIn, loginError, clearError } = useAuth();
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      username: "",
      password: "",
    },
  });

  const onSubmit = async (data: LoginFormValues) => {
    try {
      clearError();
      await login(data);
      toast.success("Welcome back!");
      navigate("/");
    } catch {
      // Error is handled by the useAuth hook
    }
  };

  return (
    <div className="min-h-screen w-full lg:grid lg:grid-cols-2 font-sans text-zinc-900 dark:text-zinc-100 transition-colors duration-300">

      {/* --- LEFT PANEL: Brand & Features --- */}
      <div className="hidden lg:flex relative flex-col justify-between p-12 xl:p-16 overflow-hidden bg-zinc-100 dark:bg-zinc-950 border-r border-zinc-200 dark:border-zinc-800 transition-colors duration-300">

        {/* Dot Pattern Background - Now using inline SVG for reliability */}
        <div
          className="absolute inset-0 opacity-40 dark:opacity-20 pointer-events-none"
          style={{
            backgroundImage: `radial-gradient(circle at 1px 1px, rgb(161 161 170) 1px, transparent 0)`,
            backgroundSize: '24px 24px'
          }}
        />

        {/* Ambient Gradient Overlay */}
        <div className="absolute inset-0 bg-gradient-to-br from-white/60 via-transparent to-emerald-500/5 dark:from-black/40 dark:via-transparent dark:to-emerald-500/10 pointer-events-none" />

        {/* Content Container */}
        <div className="relative z-10 flex flex-col h-full justify-between">

          {/* Logo */}
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-600 text-white shadow-md shadow-emerald-600/30">
              <Database className="h-5 w-5" />
            </div>
            <span className="text-lg font-bold tracking-tight text-zinc-900 dark:text-white">Acto Engine</span>
          </div>

          {/* Value Prop */}
          <div className="max-w-md space-y-8">
            <h1 className="text-4xl font-semibold tracking-tight leading-[1.1] text-zinc-900 dark:text-white">
              Database schema <br />
              management, <span className="text-emerald-600 dark:text-emerald-400">reimagined.</span>
            </h1>

            <p className="text-base text-zinc-600 dark:text-zinc-400 leading-relaxed">
              Stop fighting your database. Visualize dependencies, detect drift, and deploy changes with absolute confidence.
            </p>

            {/* Feature Grid (Bento Style) */}
            <div className="grid grid-cols-1 gap-3 mt-4">
              {[
                { icon: Zap, title: "Impact Analysis", desc: "Predict breaking changes before merge." },
                { icon: Shield, title: "Drift Detection", desc: "Keep production aligned with code." },
                { icon: Code2, title: "Semantic Context", desc: "Document intent, not just types." }
              ].map((item, idx) => (
                <div
                  key={idx}
                  className="group flex items-start gap-4 p-4 rounded-xl border border-zinc-200/80 bg-white/70 dark:border-zinc-800 dark:bg-zinc-900/50 backdrop-blur-sm transition-all hover:border-emerald-500/50 hover:shadow-md hover:shadow-emerald-500/5 dark:hover:border-emerald-500/30"
                >
                  <div className="mt-0.5 p-2 rounded-lg bg-zinc-100 dark:bg-zinc-800 text-emerald-600 dark:text-emerald-400 group-hover:scale-110 transition-transform">
                    <item.icon className="w-4 h-4" />
                  </div>
                  <div>
                    <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">{item.title}</h3>
                    <p className="text-xs text-zinc-500 dark:text-zinc-500 mt-1 leading-snug">{item.desc}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Footer */}
          <div className="flex items-center gap-2 text-xs font-medium text-zinc-500 dark:text-zinc-500">
            <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" />
            <span>Enterprise-grade security included</span>
          </div>
        </div>
      </div>

      {/* --- RIGHT PANEL: Login Form --- */}
      <div className="relative flex flex-col items-center justify-center p-6 sm:p-8 bg-white dark:bg-zinc-950 transition-colors duration-300">

        {/* Mobile Background Pattern */}
        <div
          className="absolute inset-0 lg:hidden opacity-30 dark:opacity-10 pointer-events-none"
          style={{
            backgroundImage: `radial-gradient(circle at 1px 1px, rgb(161 161 170) 1px, transparent 0)`,
            backgroundSize: '24px 24px'
          }}
        />

        {/* Premium Card Wrapper - Using reliable Tailwind utilities */}
        <div className="relative w-full max-w-[400px] animate-in fade-in slide-in-from-bottom-4 duration-700">

          {/* The Card - Premium styling with visible effects */}
          <div className="relative p-8 sm:p-10 rounded-2xl bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 shadow-2xl shadow-zinc-300/50 dark:shadow-black/50 ring-1 ring-zinc-900/5 dark:ring-white/5">

            {/* Top highlight line - visible premium effect */}
            <div className="absolute top-0 left-8 right-8 h-px bg-gradient-to-r from-transparent via-zinc-300 dark:via-zinc-600 to-transparent" />

            {/* Mobile Logo */}
            <div className="lg:hidden flex justify-center mb-8">
              <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-emerald-600 text-white shadow-lg shadow-emerald-600/30">
                <Database className="h-6 w-6" />
              </div>
            </div>

            {/* Form Header */}
            <div className="mb-8 text-center lg:text-left">
              <h2 className="text-2xl font-bold tracking-tight text-zinc-900 dark:text-white">
                Welcome back
              </h2>
              <p className="mt-2 text-sm text-zinc-500 dark:text-zinc-400">
                Please enter your details to sign in.
              </p>
            </div>

            {/* Main Form */}
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">

              {/* Username Field */}
              <div className="space-y-2">
                <Label htmlFor="username" className="text-xs uppercase tracking-wider font-semibold text-zinc-500 dark:text-zinc-400">
                  Username
                </Label>
                <Input
                  id="username"
                  {...register("username")}
                  disabled={isLoggingIn}
                  className={`h-11 bg-zinc-50 dark:bg-zinc-800/50 border-zinc-200 dark:border-zinc-700 text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 dark:placeholder:text-zinc-500 rounded-lg transition-all 
                    focus-visible:ring-2 focus-visible:ring-emerald-500/20 focus-visible:border-emerald-500
                    ${errors.username ? "border-red-500 focus-visible:ring-red-500/20 focus-visible:border-red-500" : ""}
                  `}
                  placeholder="name@company.com"
                />
                {errors.username && (
                  <p className="text-xs text-red-500 font-medium flex items-center gap-1">
                    <AlertCircle className="w-3 h-3" /> {errors.username.message}
                  </p>
                )}
              </div>

              {/* Password Field */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label htmlFor="password" className="text-xs uppercase tracking-wider font-semibold text-zinc-500 dark:text-zinc-400">
                    Password
                  </Label>
                  {/* TODO: Add forgot password link in future */}
                  {/* <a href="#" className="text-xs font-medium text-emerald-600 hover:text-emerald-500 dark:text-emerald-400 dark:hover:text-emerald-300 transition-colors">
                    Forgot password?
                  </a> */}
                </div>
                <div className="relative">
                  <Input
                    id="password"
                    type={showPassword ? "text" : "password"}
                    {...register("password")}
                    disabled={isLoggingIn}
                    className={`h-11 pr-10 bg-zinc-50 dark:bg-zinc-800/50 border-zinc-200 dark:border-zinc-700 text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 dark:placeholder:text-zinc-500 rounded-lg transition-all 
                      focus-visible:ring-2 focus-visible:ring-emerald-500/20 focus-visible:border-emerald-500
                      ${errors.password ? "border-red-500 focus-visible:ring-red-500/20 focus-visible:border-red-500" : ""}
                    `}
                    placeholder="••••••••"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-300 transition-colors"
                  >
                    {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
                {errors.password && (
                  <p className="text-xs text-red-500 font-medium flex items-center gap-1">
                    <AlertCircle className="w-3 h-3" /> {errors.password.message}
                  </p>
                )}
              </div>

              {/* General Error Message */}
              {loginError && (
                <div className="p-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/50 text-red-600 dark:text-red-400 text-sm flex items-center gap-2 animate-in fade-in slide-in-from-top-1">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>{loginError}</span>
                </div>
              )}

              {/* Submit Button */}
              <button
                type="submit"
                disabled={isLoggingIn}
                className="w-full h-11 mt-2 inline-flex items-center justify-center gap-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-sm font-semibold shadow-lg shadow-emerald-600/25 transition-all duration-200 disabled:opacity-70 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 dark:focus:ring-offset-zinc-900 active:scale-[0.98]"
              >
                {isLoggingIn ? (
                  <>
                    <div className="h-4 w-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                    <span>Authenticating...</span>
                  </>
                ) : (
                  <>
                    <span>Sign In</span>
                    <ArrowRight className="w-4 h-4" />
                  </>
                )}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}