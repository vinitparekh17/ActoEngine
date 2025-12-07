"use client";

import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import { ChevronDown, Clipboard, Download, FolderArchive } from "lucide-react";
import { ButtonGroup, ButtonGroupSeparator } from "../ui/button-group";

export default function CodeExportButton({
  onExport,
}: {
  onExport: (format: "sql" | "copy" | "zip") => void;
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
          <DropdownMenuItem onClick={() => onExport("zip")}>
            <FolderArchive className="h-4 w-4 mr-2" />
            Download as .zip
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </ButtonGroup>
  );
}
