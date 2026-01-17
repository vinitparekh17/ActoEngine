import { useEffect, useRef, useState } from "react";
import type { CSSProperties } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { useAuth } from "../hooks/useAuth";
import { useNavigate, Link } from "react-router-dom";
import { toast } from "sonner";
import { User, Lock, Eye, EyeOff, Database, Zap, Shield, Code, Code2, Terminal, AlertCircle, ArrowRight } from "lucide-react";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";

// --- Schema ---
const loginSchema = z.object({
  username: z.string().min(1, "Username is required"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormValues = z.infer<typeof loginSchema>;


const useParallax = () => {
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  useEffect(() => {
    const handleMove = (e: MouseEvent) => {
      setOffset({
        x: (e.clientX - window.innerWidth / 2) / 40,
        y: (e.clientY - window.innerHeight / 2) / 40,
      });
    };
    window.addEventListener('mousemove', handleMove);
    return () => window.removeEventListener('mousemove', handleMove);
  }, []);
  return offset;
};

const useRelativeMouse = () => {
  const ref = useRef<HTMLDivElement>(null);
  const [position, setPosition] = useState({ x: 0, y: 0 });

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (ref.current) {
        const rect = ref.current.getBoundingClientRect();
        setPosition({
          x: e.clientX - rect.left,
          y: e.clientY - rect.top,
        });
      }
    };
    // Attach to window to ensure smooth tracking even near edges
    window.addEventListener("mousemove", handleMouseMove);
    return () => window.removeEventListener("mousemove", handleMouseMove);
  }, []);

  return { ref, position };
};

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
    } catch (error) {
      // Error is handled by the useAuth hook and displayed via loginError state
      // Don't have to handle it here
    }
  };

  const CustomStyles = () => (
    <style>{`
    @keyframes float {
      0%, 100% { transform: translateY(0px); }
      50% { transform: translateY(-20px); }
    }
    @keyframes pulse-glow {
      0%, 100% { opacity: 0.4; transform: scale(1); }
      50% { opacity: 0.6; transform: scale(1.05); }
    }
    @keyframes scanline {
      0% { transform: translateY(-100%); }
      100% { transform: translateY(100%); }
    }
    .bg-noise {
      background-image: url("data:image/svg+xml,%3Csvg viewBox='0 0 200 200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noiseFilter'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noiseFilter)' opacity='0.05'/%3E%3C/svg%3E");
    }
    .spotlight-card {
      position: relative;
      background: rgba(15, 23, 42, 0.6);
      overflow: hidden;
    }
    .spotlight-card::before {
      content: '';
      position: absolute;
      top: 0; left: 0; right: 0; bottom: 0;
      background: radial-gradient(800px circle at var(--mouse-x) var(--mouse-y), rgba(16, 185, 129, 0.15), transparent 40%);
      opacity: 0;
      transition: opacity 0.5s;
      pointer-events: none;
      z-index: 0;
    }
    .spotlight-card:hover::before {
      opacity: 1;
    }
    .spotlight-border {
      position: absolute;
      inset: -1px;
      border-radius: inherit;
      background: radial-gradient(400px circle at var(--mouse-x) var(--mouse-y), rgba(16, 185, 129, 0.6), transparent 40%);
      mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
      -webkit-mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
      mask-composite: xor;
      -webkit-mask-composite: xor;
      pointer-events: none;
      opacity: 0;
      transition: opacity 0.5s;
      z-index: 10;
    }
    .spotlight-card:hover .spotlight-border {
      opacity: 1;
    }
  `}</style>
  );

  const { ref: cardRef, position: mousePos } = useRelativeMouse();
  // Parallax Offset
  const parallax = useParallax();

  return (
    <div className="min-h-screen w-full lg:grid lg:grid-cols-2 font-sans bg-neutral-950 selection:bg-emerald-500/30 overflow-x-hidden">
      <CustomStyles />
      {/* LEFT PANEL: The Brand Experience */}
      <div className="hidden lg:flex relative flex-col justify-between overflow-hidden bg-neutral-950 text-white p-16">

        {/* Dynamic Background Layer */}
        <div className="absolute inset-0 z-0 bg-noise pointer-events-none opacity-20" />

        <div className="absolute inset-0 z-0">
          {/* Base Gradient */}
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,_var(--tw-gradient-stops))] from-emerald-900/20 via-neutral-950 to-neutral-950" />

          {/* Animated Grid */}
          <div className="absolute inset-0 opacity-20"
            style={{
              backgroundImage: 'linear-gradient(rgba(16, 185, 129, 0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(16, 185, 129, 0.1) 1px, transparent 1px)',
              backgroundSize: '50px 50px',
              maskImage: 'linear-gradient(to bottom, black 30%, transparent 100%)',
              transform: `translate(${parallax.x * -1}px, ${parallax.y * -1}px)`
            }}>
          </div>

          {/* Floating "Data" Orbs with Parallax */}
          <div
            className="absolute top-1/4 right-1/4 w-96 h-96 bg-emerald-500/10 rounded-full blur-[100px]"
            style={{
              animation: 'pulse-glow 4s infinite alternate',
              transform: `translate(${parallax.x}px, ${parallax.y}px)`
            }}
          />
          <div
            className="absolute bottom-1/4 left-1/4 w-64 h-64 bg-teal-500/10 rounded-full blur-[80px]"
            style={{
              animation: 'pulse-glow 7s infinite alternate',
              transform: `translate(${parallax.x * 1.5}px, ${parallax.y * 1.5}px)`
            }}
          />
        </div>

        {/* Content Layer */}
        <div className="relative z-10 h-full flex flex-col justify-between">
          {/* Logo */}
          <div className="flex items-center gap-3 group cursor-default">
            <div className="relative flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-tr from-emerald-500 to-teal-500 shadow-lg shadow-emerald-500/20 group-hover:scale-110 transition-transform duration-500 ease-out">
              <Database className="h-5 w-5 text-white relative z-10" />
              {/* Logo Glow */}
              <div className="absolute inset-0 rounded-xl bg-emerald-400 blur-md opacity-40 group-hover:opacity-70 transition-opacity duration-500" />
            </div>
            <span className="text-xl font-bold tracking-tight text-neutral-100">Acto Engine</span>
          </div>

          {/* Hero Copy */}
          <div className="space-y-12 max-w-xl">
            <div className="space-y-6">
              <h1 className="text-5xl font-bold leading-[1.1] tracking-tight">
                Your Database,<br />
                <span className="text-transparent bg-clip-text bg-gradient-to-r from-emerald-400 to-teal-200 animate-in fade-in duration-1000">
                  Explained.
                </span>
              </h1>
              <p className="text-lg text-neutral-300 leading-relaxed font-light">
                Capture what documentation misses: the <span className="text-emerald-200 font-medium border-b border-emerald-500/30">intent</span> behind your database.
                Understand hidden dependencies, detect schema drift, and analyze the impact of changes before they reach production.
              </p>
            </div>

            {/* Feature Pills */}
            <div className="flex flex-wrap gap-3">
              {[
                { icon: Zap, text: "Impact Analysis", color: "text-amber-300", bg: "hover:bg-amber-900/20 hover:border-amber-500/30" },
                { icon: Shield, text: "Drift Detection", color: "text-blue-300", bg: "hover:bg-blue-900/20 hover:border-blue-500/30" },
                { icon: Code2, text: "Context-Aware Metadata", color: "text-purple-300", bg: "hover:bg-purple-900/20 hover:border-purple-500/30" }
              ].map((pill, idx) => (
                <div key={idx} className={`group flex items-center gap-2 rounded-full bg-neutral-900/40 backdrop-blur-md border border-neutral-700/50 px-4 py-2 text-sm font-medium text-neutral-200 transition-all duration-300 hover:scale-105 cursor-default ${pill.bg}`}>
                  <pill.icon className={`h-4 w-4 ${pill.color} transition-transform group-hover:rotate-12`} />
                  <span>{pill.text}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Social Proof */}
          <div className="space-y-6">
            <div className="h-px w-full max-w-xs bg-gradient-to-r from-emerald-500/50 to-transparent opacity-50" />
            <div className="space-y-2">
              <blockquote className="text-lg text-neutral-300 font-light italic leading-relaxed">
                "What breaks if I change this? Why does this exist? Which <span className="text-emerald-300 not-italic font-medium">services</span> depend on this version?"
              </blockquote>
              <p className="flex items-center gap-2 text-sm font-medium text-neutral-500 uppercase tracking-wider">
                <Terminal className="w-4 h-4" />
                Questions Acto Engine answers for you.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* RIGHT PANEL: The Interface */}
      <div className="relative flex min-h-screen items-center justify-center p-6 lg:p-12 overflow-hidden bg-neutral-950">

        {/* Mobile Background */}
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_center,_var(--tw-gradient-stops))] from-emerald-900/10 via-neutral-950 to-neutral-950 lg:hidden" />
        <div className="absolute inset-0 bg-noise opacity-30 pointer-events-none" />

        {/* Auth Card Container */}
        <div className="w-full max-w-[420px] relative z-20 perspective-1000">

          {/* Spotlight Card */}
          <div
            ref={cardRef}
            className="spotlight-card relative rounded-2xl border border-white/5 p-8 shadow-2xl backdrop-blur-xl ring-1 ring-white/10 transition-transform duration-200 ease-out"
            style={{
              "--mouse-x": `${mousePos.x}px`,
              "--mouse-y": `${mousePos.y}px`,
              // Subtle 3D tilt based on mouse position relative to center of screen (simplified)
              transform: `perspective(1000px) rotateX(${(mousePos.y - 300) / 100}deg) rotateY(${(mousePos.x - 200) / 100}deg)`
            } as CSSProperties}
          >
            <div className="spotlight-border rounded-2xl" />

            {/* Header */}
            <div className="relative z-10 mb-10">
              {/* Mobile Logo */}
              <div className="lg:hidden mb-6 flex justify-center">
                <div className="h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 flex shadow-lg shadow-emerald-500/20">
                  <Database className="h-6 w-6 text-white" />
                </div>
              </div>

              <h2 className="text-3xl font-bold tracking-tight text-white mb-2">Welcome back</h2>
              <p className="text-neutral-400 text-sm">
                Sign in to continue working with your database context.
              </p>
            </div>

            {/* Login Form */}
            <form onSubmit={handleSubmit(onSubmit)} className="relative z-10 space-y-6">

              {/* Username */}
              <div className="space-y-2 group">
                <Label htmlFor="username">Username</Label>
                <div className="relative transform transition-transform duration-200 group-focus-within:scale-[1.01]">
                  <div className="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-neutral-500 group-focus-within:text-emerald-400 transition-colors">
                    <User className="h-4 w-4" />
                  </div>
                  <Input
                    id="username"
                    type="text"
                    {...register("username")}
                    className={`w-full rounded-xl border pl-9 py-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/20 ${errors.username
                      ? "border-red-500 focus:border-red-500"
                      : "border-gray-300 focus:border-blue-500"
                      }`}
                    placeholder="Enter your username"
                    disabled={isLoggingIn}
                  />
                </div>
              </div>

              {/* Password */}
              <div className="space-y-2 group">
                <div className="flex items-center justify-between">
                  <Label htmlFor="password">Password</Label>
                  <Link to="/forgot-password" className="text-xs font-medium text-emerald-500 hover:text-emerald-400 transition-colors hover:underline">
                    Forgot password?
                  </Link>
                </div>
                <div className="relative transform transition-transform duration-200 group-focus-within:scale-[1.01]">
                  <div className="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-neutral-500 group-focus-within:text-emerald-400 transition-colors">
                    <Lock className="h-4 w-4" />
                  </div>
                  <Input
                    id="password"
                    type={showPassword ? "text" : "password"}
                    {...register("password")}
                    className={`w-full rounded-xl border pr-10 pl-9 py-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/20 ${errors.password
                      ? "border-red-500 focus:border-red-500"
                      : "border-gray-300 focus:border-blue-500"
                      }`}
                    placeholder="Enter your password"
                    disabled={isLoggingIn}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    className="absolute inset-y-0 right-0 flex items-center pr-3 text-neutral-500 hover:text-neutral-300 transition-colors focus:outline-none focus:ring-2 focus:ring-emerald-500/50 rounded"
                    aria-label={showPassword ? "Hide password" : "Show password"}
                  >
                    {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </button>
                </div>
              </div>

              {/* Error Message */}
              {loginError && (
                <div className="flex items-center gap-2 rounded-lg bg-red-950/30 border border-red-500/20 p-3 text-sm text-red-400 animate-in slide-in-from-top-2 duration-200">
                  <AlertCircle className="h-4 w-4 shrink-0" />
                  <span>{loginError}</span>
                </div>
              )}

              {/* Submit Button */}
              <button
                type="submit"
                disabled={isLoggingIn}
                className="group relative w-full h-11 flex items-center justify-center gap-2 rounded-lg bg-emerald-600 text-sm font-bold text-white shadow-lg shadow-emerald-900/20 transition-all hover:bg-emerald-500 hover:shadow-emerald-900/40 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 focus:ring-offset-neutral-900 disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98] overflow-hidden"
              >
                {/* Button Shine Effect */}
                <div className="absolute inset-0 flex h-full w-full justify-center [transform:skew(-12deg)_translateX(-100%)] group-hover:duration-1000 group-hover:[transform:skew(-12deg)_translateX(100%)]">
                  <div className="relative h-full w-8 bg-white/20" />
                </div>

                {isLoggingIn ? (
                  <>
                    <svg className="h-4 w-4 animate-spin text-white/80" viewBox="0 0 24 24" fill="none">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                    </svg>
                    <span>Authenticating...</span>
                  </>
                ) : (
                  <>
                    <span>Sign In</span>
                    <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" />
                  </>
                )}
              </button>
            </form>
          </div>

          {/* Footer */}
          <div className="mt-8 text-center space-y-2">
            <p className="flex items-center justify-center gap-2 text-xs text-neutral-600">
              <Shield className="w-3 h-3" />
              <span className="font-medium">256-bit Encryption Active</span>
            </p>
            <p className="text-[10px] text-neutral-700 uppercase tracking-widest">
              Acto Engine v1.2 &middot; Internal Platform
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}