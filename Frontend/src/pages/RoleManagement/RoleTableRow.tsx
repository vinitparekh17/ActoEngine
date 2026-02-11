import { Button } from "../../components/ui/button";
import { TableCell, TableRow } from "../../components/ui/table";
import { Pencil, Trash2, Shield, Eye } from "lucide-react";
import type { Role } from "../../types/user-management";

interface RoleTableRowProps {
  role: Role;
  onView: (role: Role) => void;
  onEdit: (role: Role) => void;
  onDelete: (role: Role) => void;
}

export function RoleTableRow({
  role,
  onView,
  onEdit,
  onDelete,
}: Readonly<RoleTableRowProps>) {
  return (
    <TableRow>
      <TableCell>{role.roleId}</TableCell>
      <TableCell>
        <div className="flex items-center gap-2">
          {role.roleName}
          {role.isSystem && (
            <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200">
              <Shield className="h-3 w-3 mr-1" />
              System
            </span>
          )}
        </div>
      </TableCell>
      <TableCell>{role.description || "-"}</TableCell>
      <TableCell></TableCell>
      <TableCell>
        <span
          className={`px-2 py-1 rounded-full text-xs ${
            role.isActive
              ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
              : "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
          }`}
        >
          {role.isActive ? "Active" : "Inactive"}
        </span>
      </TableCell>
      <TableCell>
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onView(role)}
            title="View Details"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onEdit(role)}
            disabled={role.isSystem}
            title="Edit Role"
          >
            <Pencil className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onDelete(role)}
            className="text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
            disabled={role.isSystem}
            title="Delete Role"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  );
}
