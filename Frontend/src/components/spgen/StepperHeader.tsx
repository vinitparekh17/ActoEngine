import { Table2, SlidersHorizontal, Code2, Check, ChevronRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { SPType } from "./SPTypeCard";

export type Step = 0 | 1 | 2;

export const STEPS = [
    { id: 0, label: "Select Table", icon: Table2, desc: "Choose database table" },
    { id: 1, label: "Configure", icon: SlidersHorizontal, desc: "Set procedure options" },
    { id: 2, label: "Preview", icon: Code2, desc: "Review & export SQL" },
] as const;

export default function StepperHeader({
    current,
    completedUpTo,
    onStepClick,
    selectedTable,
    spType,
}: {
    current: Step;
    completedUpTo: Step;
    onStepClick: (step: Step) => void;
    selectedTable: string | null;
    spType: SPType;
}) {
    return (
        <div className="shrink-0 border-b border-border/40 bg-card/95 backdrop-blur-md sticky top-0 z-20 shadow-sm">
            <div className="flex flex-col md:flex-row md:items-center justify-between max-w-7xl mx-auto px-4 sm:px-6">
                <div className="flex items-center gap-1 overflow-x-auto hide-scrollbar py-2">
                    {STEPS.map((step, i) => {
                        const Icon = step.icon;
                        const isCompleted = completedUpTo > step.id;
                        const isCurrent = current === step.id;
                        const isClickable = step.id <= completedUpTo;

                        return (
                            <div key={step.id} className="flex items-center">
                                <button
                                    disabled={!isClickable}
                                    onClick={() => isClickable && onStepClick(step.id as Step)}
                                    className={cn(
                                        "flex items-center gap-3 py-3 px-4 relative transition-all rounded-lg text-left",
                                        "focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50",
                                        isCurrent ? "bg-primary/5 text-foreground" : "hover:bg-muted/50",
                                        !isCurrent && !isCompleted && "opacity-50 cursor-not-allowed",
                                    )}
                                >
                                    {/* Step circle */}
                                    <span
                                        className={cn(
                                            "flex items-center justify-center w-8 h-8 rounded-full text-xs font-bold shrink-0 transition-all duration-300",
                                            isCurrent && "bg-primary text-primary-foreground shadow-md shadow-primary/20 scale-110",
                                            isCompleted && !isCurrent && "bg-primary/15 text-primary",
                                            !isCurrent && !isCompleted && "bg-muted text-muted-foreground border border-border/50",
                                        )}
                                    >
                                        {isCompleted ? <Check className="w-4 h-4" /> : <Icon className="w-4 h-4" />}
                                    </span>

                                    <div className="hidden sm:block min-w-[120px]">
                                        <p className={cn("text-sm font-semibold tracking-tight transition-colors", isCurrent ? "text-primary" : "text-foreground")}>
                                            {step.label}
                                        </p>
                                        <p className="text-xs text-muted-foreground mt-0.5">{step.desc}</p>
                                    </div>
                                </button>

                                {/* Connector */}
                                {i < STEPS.length - 1 && (
                                    <ChevronRight className={cn(
                                        "w-4 h-4 shrink-0 mx-2 transition-colors",
                                        completedUpTo > step.id ? "text-primary/50" : "text-border"
                                    )} />
                                )}
                            </div>
                        );
                    })}
                </div>

                {/* Context pills */}
                <div className="flex items-center gap-3 py-3 md:py-0 border-t md:border-t-0 border-border/40">
                    <div className="flex items-center gap-2">
                        <span className="text-xs font-medium text-muted-foreground">Target:</span>
                        {selectedTable ? (
                            <Badge variant="outline" className="text-xs gap-1.5 font-mono bg-background border-border/60 py-1 px-2.5">
                                <Table2 className="w-3.5 h-3.5 text-primary" />
                                {selectedTable}
                            </Badge>
                        ) : (
                            <Badge variant="outline" className="text-xs text-muted-foreground/50 border-dashed">
                                None selected
                            </Badge>
                        )}
                    </div>

                    {current >= 1 && (
                        <Badge
                            variant="secondary"
                            className={cn(
                                "text-xs font-medium py-1 px-2.5",
                                spType === "CUD"
                                    ? "bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-500/20"
                                    : "bg-violet-500/10 text-violet-600 dark:text-violet-400 border border-violet-500/20"
                            )}
                        >
                            {spType === "CUD" ? "CUD Generator" : "SELECT Generator"}
                        </Badge>
                    )}
                </div>
            </div>
        </div>
    );
}
