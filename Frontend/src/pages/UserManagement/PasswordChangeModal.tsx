import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Button } from "../../components/ui/button";
import { Input } from "../../components/ui/input";
import { Label } from "../../components/ui/label";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "../../components/ui/dialog";
import type { UserDto } from "../../types/user-management";
import { getPasswordChangingText, getChangePasswordText } from "./helpers";

const passwordChangeSchema = z
    .object({
        newPassword: z.string().min(8, "Password must be at least 8 characters"),
        confirmPassword: z.string().min(1, "Please confirm your password"),
    })
    .refine((data) => data.newPassword === data.confirmPassword, {
        message: "Passwords do not match",
        path: ["confirmPassword"],
    });

type PasswordChangeFormValues = z.infer<typeof passwordChangeSchema>;

interface PasswordChangeModalProps {
    readonly isOpen: boolean;
    readonly user: UserDto | null;
    readonly isPending: boolean;
    readonly onClose: (open: boolean) => void;
    readonly onSubmit: (data: PasswordChangeFormValues) => void;
}

export function PasswordChangeModal({
    isOpen,
    user,
    isPending,
    onClose,
    onSubmit,
}: PasswordChangeModalProps) {
    const {
        register,
        handleSubmit,
        reset,
        formState: { errors },
    } = useForm<PasswordChangeFormValues>({
        resolver: zodResolver(passwordChangeSchema),
        defaultValues: { newPassword: "", confirmPassword: "" },
    });

    useEffect(() => {
        if (isOpen) {
            reset({ newPassword: "", confirmPassword: "" });
        }
    }, [isOpen, reset]);

    const handleCancel = () => {
        onClose(false);
    };

    return (
        <Dialog open={isOpen} onOpenChange={onClose}>
            <DialogContent className="sm:max-w-[425px]">
                <DialogHeader>
                    <DialogTitle>Change Password</DialogTitle>
                    <DialogDescription>
                        Change password for user: {user?.username || "unknown user"}
                    </DialogDescription>
                </DialogHeader>

                <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 py-4">
                    <div className="grid grid-cols-4 items-center gap-4">
                        <Label htmlFor="newPassword" className="text-right">
                            New Password
                        </Label>
                        <div className="col-span-3">
                            <Input
                                id="newPassword"
                                type="password"
                                {...register("newPassword")}
                                placeholder="Min 8 characters"
                                className={errors.newPassword ? "border-destructive" : ""}
                            />
                            {errors.newPassword && (
                                <p className="text-destructive text-sm mt-1">
                                    {errors.newPassword.message}
                                </p>
                            )}
                        </div>
                    </div>

                    <div className="grid grid-cols-4 items-center gap-4">
                        <Label htmlFor="confirmPassword" className="text-right">
                            Confirm Password
                        </Label>
                        <div className="col-span-3">
                            <Input
                                id="confirmPassword"
                                type="password"
                                {...register("confirmPassword")}
                                placeholder="Confirm new password"
                                className={errors.confirmPassword ? "border-destructive" : ""}
                            />
                            {errors.confirmPassword && (
                                <p className="text-destructive text-sm mt-1">
                                    {errors.confirmPassword.message}
                                </p>
                            )}
                        </div>
                    </div>

                    <DialogFooter>
                        <Button type="button" variant="outline" onClick={handleCancel}>
                            Cancel
                        </Button>
                        <Button type="submit" disabled={isPending}>
                            {isPending
                                ? getPasswordChangingText()
                                : getChangePasswordText()}
                        </Button>
                    </DialogFooter>
                </form>
            </DialogContent>
        </Dialog>
    );
}
