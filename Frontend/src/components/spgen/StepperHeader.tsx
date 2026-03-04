import { Table2, SlidersHorizontal, Code2, Check, ChevronRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { SPType } from "./SPTypeCard";
import { Card, CardContent } from "../ui/card";
import { Button } from "../ui/button";

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
<div className="sticky top-0 z-30 shrink-0 bg-background pb-4 pt-4 px-4 sm:px-6">
    <Card className="max-w-7xl mx-auto shadow-sm border-border rounded-xl">
        <CardContent className="p-2 flex flex-col xl:flex-row xl:items-center justify-between gap-2">
            
            {/* Steps Container */}
            <div className="flex items-center overflow-x-auto hide-scrollbar flex-1 px-1">
                {STEPS.map((step, i) => {
                    const Icon = step.icon;
                    const isCompleted = completedUpTo > step.id;
                    const isCurrent = current === step.id;
                    const isClickable = step.id <= completedUpTo;

                    return (
                        <div key={step.id} className="flex items-center shrink-0">
                            <button
                                disabled={!isClickable}
                                onClick={() => isClickable && onStepClick(step.id as Step)}
                                aria-label={step.label}
                                aria-current={isCurrent ? "step" : undefined}
                                className={cn(
                                    "flex items-center gap-3 py-2 px-3 transition-colors rounded-md text-left group outline-none focus-visible:ring-2 focus-visible:ring-primary",
                                    isCurrent ? "bg-accent/60" : "hover:bg-muted/50",
                                    !isClickable && "opacity-50 cursor-not-allowed",
                                    isClickable && "cursor-pointer"
                                )}
                            >
                                {/* Step Circle Indicator */}
                                <span
                                    className={cn(
                                        "flex items-center justify-center w-8 h-8 rounded-full text-xs shrink-0 transition-colors border",
                                        isCurrent
                                            ? "bg-primary border-primary text-primary-foreground shadow-sm"
                                            : isCompleted
                                            ? "bg-primary/10 border-primary/20 text-primary"
                                            : "bg-background border-border text-muted-foreground"
                                    )}
                                >
                                    {isCompleted && !isCurrent ? (
                                        <Check className="w-4 h-4 stroke-[2.5]" />
                                    ) : (
                                        <Icon className="w-4 h-4" />
                                    )}
                                </span>

                                <div className="hidden sm:block min-w-[110px]">
                                    <p
                                        className={cn(
                                            "text-sm font-semibold tracking-tight leading-none transition-colors",
                                            isCurrent ? "text-foreground" : "text-muted-foreground group-hover:text-foreground"
                                        )}
                                    >
                                        {step.label}
                                    </p>
                                    <p className="text-[10px] text-muted-foreground mt-1.5 leading-none font-bold uppercase tracking-wider">
                                        Step {step.id + 1}
                                    </p>
                                </div>
                            </button>

                            {/* Connector */}
                            {i < STEPS.length - 1 && (
                                <ChevronRight
                                    className={cn(
                                        "w-4 h-4 shrink-0 mx-2",
                                        completedUpTo > step.id ? "text-primary/50" : "text-border"
                                    )}
                                />
                            )}
                        </div>
                    );
                })}
            </div>

            {/* Context Pills Panel */}
            <div className="flex flex-wrap items-center gap-4 px-4 py-3 xl:py-2.5 bg-muted/30 rounded-lg border border-border/50 mx-1 xl:mx-0 xl:mr-1 shrink-0">
                <div className="flex items-center gap-2">
                    <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                        Target Object:
                    </span>
                    {selectedTable ? (
                        <Badge
                            variant="outline"
                            className="text-xs gap-1.5 font-mono bg-background shadow-sm border-border py-0.5 px-2"
                        >
                            <Table2 className="w-3.5 h-3.5 text-primary" />
                            {selectedTable}
                        </Badge>
                    ) : (
                        <Badge
                            variant="outline"
                            className="text-xs text-muted-foreground/60 border-dashed bg-transparent"
                        >
                            None selected
                        </Badge>
                    )}
                </div>

                {current >= 1 && (
                    <>
                        <div className="w-px h-4 bg-border hidden sm:block" />
                        <Badge
                            variant="secondary"
                            className={cn(
                                "text-xs font-semibold py-0.5 px-2.5 shadow-sm",
                                spType === "CUD"
                                    ? "bg-blue-50 text-blue-700 hover:bg-blue-50 border border-blue-200 dark:bg-blue-900/30 dark:text-blue-400 dark:border-blue-800"
                                    : "bg-violet-50 text-violet-700 hover:bg-violet-50 border border-violet-200 dark:bg-violet-900/30 dark:text-violet-400 dark:border-violet-800"
                            )}
                        >
                            {spType === "CUD" ? "CUD Generator" : "SELECT Generator"}
                        </Badge>
                    </>
                )}
            </div>
            
        </CardContent>
    </Card>
</div>
    );
}
