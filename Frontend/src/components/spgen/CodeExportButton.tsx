"use client";

import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import { ChevronDown, Clipboard, Download } from "lucide-react";
import { ButtonGroup, ButtonGroupSeparator } from "../ui/button-group";

export default function CodeExportButton({
  onExport,
}: {
  // TODO: keep parent handlers narrowed to this union until ZIP support is reintroduced.
  onExport: (format: "sql" | "copy") => void;
}) {
  return (
    <ButtonGroup>
      <Button
        onClick={() => onExport("sql")}
        size="sm"
        className="rounded-r-none"
      >
        <Download className="h-4 w-4 mr-2" />
        Download .sql
      </Button>
      <ButtonGroupSeparator />
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="outline"
            size="sm"
            className="rounded-l-none bg-transparent"
          >
            <ChevronDown className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onClick={async () => onExport("copy")}>
            <Clipboard className="h-4 w-4 mr-2" />
            Copy to Clipboard
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </ButtonGroup>
  );
}
