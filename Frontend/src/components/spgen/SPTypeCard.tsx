"use client";

import { Card } from "../ui/card";
import { cn } from "../../lib/utils";

export type SPType = "CUD" | "SELECT";

export default function SPTypeCard({
  type,
  selected,
  onChange,
}: {
  type: SPType;
  selected?: boolean;
  onChange: (type: SPType) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onChange(type)}
      className="text-left"
      aria-pressed={selected}
    >
      <Card
        className={cn(
          "rounded-2xl p-4 transition shadow-sm",
          selected ? "ring-2 ring-primary bg-accent" : "hover:bg-accent/50",
        )}
      >
        <div className="text-lg font-semibold">{type}</div>
        <div className="text-sm text-muted-foreground">
          {type === "CUD"
            ? "Generate Insert, Update, Delete procedures"
            : "Generate SELECT procedure with filtering & paging"}
        </div>
      </Card>
    </button>
    //     return (
    //     <Card className="mb-6 rounded-xl shadow-md">
    //       <Tabs value={selectedType} onValueChange={(v) => onChange(v as 'CUD' | 'SELECT')} className="p-6">
    //         <TabsList className="grid w-full grid-cols-2">
    //           <TabsTrigger value="CUD">CUD (Create/Update/Delete)</TabsTrigger>
    //           <TabsTrigger value="SELECT">SELECT (with filtering/paging)</TabsTrigger>
    //         </TabsList>
    //       </Tabs>
    //     </Card>
    //   );
  );
}
