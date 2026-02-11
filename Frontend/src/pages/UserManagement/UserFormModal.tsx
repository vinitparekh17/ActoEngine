import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Button } from "../../components/ui/button";
import { Input } from "../../components/ui/input";
import { Label } from "../../components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "../../components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "../../components/ui/dialog";
import type { Role } from "../../types/user-management";
import { getButtonText } from "./helpers";

// Base schema for user fields
const userSchemaShape = {
  username: z.string().min(1, "Username is required"),
  fullName: z.string().optional(),
  roleId: z.coerce.number().min(1, "Role is required"),
};

// Schema for creating a user (password required)
const createUserSchema = z.object({
  ...userSchemaShape,
  password: z.string().min(8, "Password must be at least 8 characters"),
});

// Schema for editing a user (password not editable here)
const updateUserSchema = z.object({
  ...userSchemaShape,
  password: z.string().optional(),
});

type UserFormValues = z.infer<typeof createUserSchema>;

interface UserFormModalProps {
  readonly isOpen: boolean;
  readonly isEditing: boolean;
  readonly defaultValues?: Partial<UserFormValues>;
  readonly roles: Role[] | undefined;
  readonly isPending: boolean;
  readonly onClose: (open: boolean) => void;
  readonly onSubmit: (data: any) => void;
}

export function UserFormModal({
  isOpen,
  isEditing,
  defaultValues,
  roles,
  isPending,
  onClose,
  onSubmit,
}: UserFormModalProps) {
  const title = isEditing ? "Edit User" : "Create New User";
  const description = isEditing
    ? 'Update the user details below and click "Update User" to save changes.'
    : 'Enter the details below and click "Create User" to add a new user.';

  const schema = isEditing ? updateUserSchema : createUserSchema;

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      username: "",
      password: "",
      fullName: "",
      roleId: 0,
      ...defaultValues,
    },
  });

  // Reset form only when modal opens
  useEffect(() => {
    if (isOpen) {
      reset({
        username: "",
        password: "",
        fullName: "",
        roleId: 0,
        ...defaultValues,
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const roleIdValue = watch("roleId");

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit((data) => onSubmit(data as UserFormValues))}
          className="grid gap-4 py-4"
        >
          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="username" className="text-right">
              Username
            </Label>
            <div className="col-span-3">
              <Input
                id="username"
                {...register("username")}
                placeholder="Enter username"
                disabled={isEditing}
                className={errors.username ? "border-destructive" : ""}
              />
              {errors.username && (
                <p className="text-destructive text-sm mt-1">
                  {errors.username.message}
                </p>
              )}
            </div>
          </div>

          {!isEditing && (
            <div className="grid grid-cols-4 items-center gap-4">
              <Label htmlFor="password" className="text-right">
                Password
              </Label>
              <div className="col-span-3">
                <Input
                  id="password"
                  type="password"
                  {...register("password")}
                  placeholder="Min 8 characters"
                  className={errors.password ? "border-destructive" : ""}
                />
                {errors.password && (
                  <p className="text-destructive text-sm mt-1">
                    {errors.password.message}
                  </p>
                )}
              </div>
            </div>
          )}

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="fullName" className="text-right">
              Full Name
            </Label>
            <div className="col-span-3">
              <Input
                id="fullName"
                {...register("fullName")}
                placeholder="Enter full name"
              />
              {/* fullName is optional, but if we had validation errors we'd show them here */}
            </div>
          </div>

          <div className="grid grid-cols-4 items-center gap-4">
            <Label htmlFor="role" className="text-right">
              Role
            </Label>
            <div className="col-span-3">
              <Select
                value={roleIdValue?.toString() || ""}
                onValueChange={(value) =>
                  setValue("roleId", Number.parseInt(value), {
                    shouldValidate: true,
                  })
                }
              >
                <SelectTrigger
                  className={errors.roleId ? "border-destructive" : ""}
                >
                  <SelectValue placeholder="Select a role" />
                </SelectTrigger>
                <SelectContent>
                  {roles?.map((role) => (
                    <SelectItem
                      key={role.roleId}
                      value={role.roleId.toString()}
                    >
                      {role.roleName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {errors.roleId && (
                <p className="text-destructive text-sm mt-1">
                  {errors.roleId.message}
                </p>
              )}
            </div>
          </div>

          <DialogFooter>
            <Button type="submit" disabled={isPending}>
              {getButtonText(isEditing, isPending)}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
