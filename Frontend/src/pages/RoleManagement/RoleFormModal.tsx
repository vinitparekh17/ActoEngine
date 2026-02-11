import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Button } from "../../components/ui/button";
import { Input } from "../../components/ui/input";
import { Label } from "../../components/ui/label";
import { Textarea } from "../../components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "../../components/ui/dialog";
import { PermissionSelector } from "./PermissionSelector";
import type { PermissionGroupDto } from "../../types/user-management";

const roleSchema = z.object({
  roleName: z.string().min(1, "Role name is required"),
  description: z.string().optional(),
  permissionIds: z.array(z.number()),
});

type RoleFormValues = z.infer<typeof roleSchema>;

interface RoleFormModalProps {
  readonly isOpen: boolean;
  readonly isEditing: boolean;
  readonly defaultValues?: Partial<RoleFormValues>;
  readonly permissionGroups: PermissionGroupDto[] | undefined;
  readonly isPending: boolean;
  readonly onClose: (open: boolean) => void;
  readonly onSubmit: (data: RoleFormValues) => void;
}

export function RoleFormModal({
  isOpen,
  isEditing,
  defaultValues,
  permissionGroups,
  isPending,
  onClose,
  onSubmit,
}: RoleFormModalProps) {
  const title = isEditing ? "Edit Role" : "Create New Role";
  const description = isEditing
    ? "Update the role details and permissions below."
    : "Enter the role details and select permissions.";

  let buttonText: string;

  if (isEditing) {
    buttonText = isPending ? "Updating..." : "Update Role";
  } else {
    buttonText = isPending ? "Creating..." : "Create Role";
  }
  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(roleSchema),
    defaultValues: {
      roleName: "",
      description: "",
      permissionIds: [],
      ...defaultValues,
    },
  });

  const selectedPermissionIds = watch("permissionIds") || [];

  useEffect(() => {
    if (isOpen) {
      reset({
        roleName: "",
        description: "",
        permissionIds: [],
        ...defaultValues,
      });
    }
  }, [isOpen, defaultValues, reset]);

  const handlePermissionToggle = (permissionId: number, checked: boolean) => {
    const currentIds = new Set(selectedPermissionIds);
    if (checked) {
      currentIds.add(permissionId);
    } else {
      currentIds.delete(permissionId);
    }
    setValue("permissionIds", Array.from(currentIds), { shouldValidate: true });
  };

  const handleCategorySelectAll = (
    categoryPermissions: number[],
    checked: boolean,
  ) => {
    const currentIds = new Set(selectedPermissionIds);
    if (checked) {
      categoryPermissions.forEach((id) => currentIds.add(id));
    } else {
      categoryPermissions.forEach((id) => currentIds.delete(id));
    }
    setValue("permissionIds", Array.from(currentIds), { shouldValidate: true });
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[600px] max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit((data) => onSubmit(data))}
          className="grid gap-4 py-4"
        >
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="roleName" className="text-right">
              Role Name
            </Label>
            <div className="col-span-3">
              <Input
                id="roleName"
                {...register("roleName")}
                placeholder="Enter role name"
                className={errors.roleName ? "border-destructive" : ""}
              />
              {errors.roleName && (
                <p className="text-destructive text-sm mt-1">
                  {errors.roleName.message}
                </p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-4 items-start gap-4">
            <Label htmlFor="description" className="text-right pt-2">
              Description
            </Label>
            <div className="col-span-3">
              <Textarea
                id="description"
                {...register("description")}
                placeholder="Enter role description"
                rows={3}
              />
            </div>
          </div>

          <PermissionSelector
            permissionGroups={permissionGroups}
            selectedPermissionIds={selectedPermissionIds}
            onPermissionToggle={handlePermissionToggle}
            onCategorySelectAll={handleCategorySelectAll}
          />

          <DialogFooter>
            <Button type="submit" disabled={isPending}>
              {buttonText}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
