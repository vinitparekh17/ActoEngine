import { Button } from "../../components/ui/button";
import { Label } from "../../components/ui/label";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "../../components/ui/dialog";
import { Key } from "lucide-react";
import type { UserDto, UserDetailResponse } from "../../types/user-management";
import { getActiveBadgeClass, getInactiveBadgeClass } from "./helpers";
import { safeFormatDate } from "../../lib/utils";



interface UserDetailModalProps {
    readonly isOpen: boolean;
    readonly userDetail: UserDetailResponse | undefined;
    readonly onClose: (open: boolean) => void;
    readonly onChangePassword: (user: UserDto) => void;
}

export function UserDetailModal({
    isOpen,
    userDetail,
    onClose,
    onChangePassword,
}: UserDetailModalProps) {
    return (
        <Dialog open={isOpen} onOpenChange={onClose}>
            <DialogContent className="sm:max-w-[500px]">
                <DialogHeader>
                    <DialogTitle>User Details</DialogTitle>
                    <DialogDescription>
                        Detailed information about {userDetail?.user?.username || "this user"}
                    </DialogDescription>
                </DialogHeader>

                {userDetail && (
                    <div className="grid gap-4 py-4">
                        <div className="grid grid-cols-2 gap-4">
                            <div>
                                <Label className="text-muted-foreground">User ID</Label>
                                <p className="font-medium">{userDetail.user.userId}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Username</Label>
                                <p className="font-medium">{userDetail.user.username}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Full Name</Label>
                                <p className="font-medium">{userDetail.user.fullName || "-"}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Role</Label>
                                <p className="font-medium">{userDetail.user.roleName || "-"}</p>
                            </div>
                            <div>
                                <Label className="text-muted-foreground">Status</Label>
                                <span
                                    className={`px-2 py-1 rounded-full text-xs inline-block ${userDetail.user.isActive ? getActiveBadgeClass() : getInactiveBadgeClass()}`}
                                >
                                    {userDetail.user.isActive ? "Active" : "Inactive"}
                                </span>
                            </div>
                            {safeFormatDate(userDetail.user.createdAt, "") && (
                                <div>
                                    <Label className="text-muted-foreground">Created At</Label>
                                    <p className="font-medium">
                                        {safeFormatDate(userDetail.user.createdAt)}
                                    </p>
                                </div>
                            )}
                            {userDetail.user.updatedAt && safeFormatDate(userDetail.user.updatedAt, "") && (
                                <div>
                                    <Label className="text-muted-foreground">Updated At</Label>
                                    <p className="font-medium">
                                        {safeFormatDate(userDetail.user.updatedAt)}
                                    </p>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                <DialogFooter>
                    <Button
                        variant="outline"
                        onClick={() => onClose(false)}
                    >
                        Close
                    </Button>
                    {userDetail?.user && (
                        <Button onClick={() => onChangePassword(userDetail.user)}>
                            <Key className="h-4 w-4 mr-2" />
                            Change Password
                        </Button>
                    )}
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
