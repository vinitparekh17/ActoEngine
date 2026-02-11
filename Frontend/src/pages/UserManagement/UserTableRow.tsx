import { Button } from "../../components/ui/button";
import { TableCell, TableRow } from "../../components/ui/table";
import { Pencil, Trash2, Eye, Key, FolderKanban } from "lucide-react";
import type { UserDto } from "../../types/user-management";
import { getActiveBadgeClass, getInactiveBadgeClass } from "./helpers";
import { formatDate } from "../../lib/utils";

interface UserTableRowProps {
  readonly user: UserDto;
  readonly onEdit: (user: UserDto) => void;
  readonly onDelete: (user: UserDto) => void;
  readonly onViewDetail: (user: UserDto) => void;
  readonly onChangePassword: (user: UserDto) => void;
  readonly onManageProjects: (user: UserDto) => void;
}

export function UserTableRow({
  user,
  onEdit,
  onDelete,
  onViewDetail,
  onChangePassword,
  onManageProjects,
}: UserTableRowProps) {
  return (
    <TableRow>
      <TableCell>{user.userId}</TableCell>
      <TableCell>{user.username}</TableCell>
      <TableCell>{user.fullName || "-"}</TableCell>
      <TableCell>{user.roleName || "-"}</TableCell>
      <TableCell>
        <span
          className={`px-2 py-1 rounded-full text-xs ${user.isActive ? getActiveBadgeClass() : getInactiveBadgeClass()}`}
        >
          {user.isActive ? "Active" : "Inactive"}
        </span>
      </TableCell>
      <TableCell>{formatDate(user.createdAt)}</TableCell>
      <TableCell>
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onViewDetail(user)}
            title="View Details"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onEdit(user)}
            title="Edit User"
          >
            <Pencil className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onChangePassword(user)}
            title="Change Password"
          >
            <Key className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onManageProjects(user)}
            title="Manage Projects"
          >
            <FolderKanban className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onDelete(user)}
            className="text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300"
            title="Delete User"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  );
}
