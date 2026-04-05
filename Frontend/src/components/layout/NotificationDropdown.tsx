import { useState } from "react";
import { Bell, Check, CheckCircle2, Loader2 } from "lucide-react";
import { formatRelativeTime } from "@/lib/utils";
import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import {
  useNotifications,
  useUnreadNotificationCount,
  useMarkNotificationRead,
  useMarkAllNotificationsRead,
} from "../../hooks/useNotification";
import { toast } from "sonner";

export default function NotificationDropdown() {
  const [isOpen, setIsOpen] = useState(false);
  const [pendingNotificationIds, setPendingNotificationIds] = useState<Set<number>>(new Set());
  
  const { data: countData } = useUnreadNotificationCount();
  const { data: notifications, isLoading } = useNotifications(10, 0); // show top 10
  
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();

  const unreadCount = countData?.unreadCount ?? 0;

  const handleMarkRead = async (e: React.MouseEvent, notificationId: number) => {
    e.preventDefault(); 
    e.stopPropagation();
    if (pendingNotificationIds.has(notificationId)) return;
    setPendingNotificationIds((prev) => new Set(prev).add(notificationId));
    try {
      await markRead.mutateAsync(notificationId);
    } catch (error) {
      console.error("Failed to mark notification as read", error);
      toast.error("Failed to mark notification as read");
    } finally {
      setPendingNotificationIds((prev) => {
        const next = new Set(prev);
        next.delete(notificationId);
        return next;
      });
    }
  };

  const getTypeColor = (type: string) => {
    switch (type) {
      case "LFK_DETECTION_COMPLETE": return "bg-blue-500";
      case "ENTITY_RESYNC_COMPLETE": return "bg-green-500";
      case "ERROR": return "bg-red-500";
      default: return "bg-yellow-500";
    }
  };

  return (
    <DropdownMenu open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="relative">
          <Bell className="h-4 w-4" />
          {unreadCount > 0 && (
            <span className="absolute top-1.5 right-1 h-2 w-2 rounded-full bg-destructive flex items-center justify-center">
               {/* Tiny unseen dot indicator */}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      
      <DropdownMenuContent align="end" className="w-80 max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between px-4 py-2">
          <DropdownMenuLabel className="p-0">Notifications</DropdownMenuLabel>
          {unreadCount > 0 && (
            <Button 
              variant="ghost" 
              size="sm" 
              className="text-xs h-auto p-1 text-muted-foreground"
              onClick={async () => {
                try {
                  await markAllRead.mutateAsync();
                } catch (error) {
                  console.error("Failed to mark all notifications as read", error);
                  toast.error("Failed to mark all notifications as read");
                }
              }}
              disabled={markAllRead.isPending}
            >
              <CheckCircle2 className="h-3 w-3 mr-1" />
              Mark all read
            </Button>
          )}
        </div>
        <DropdownMenuSeparator />

        {isLoading ? (
          <div className="p-4 text-center text-sm text-muted-foreground">Loading...</div>
        ) : !notifications || notifications.length === 0 ? (
          <div className="p-8 text-center text-sm text-muted-foreground flex flex-col items-center">
            <Bell className="h-8 w-8 text-muted/30 mb-2" />
            No new notifications
          </div>
        ) : (
          <div className="flex flex-col">
            {notifications.map((notif) => (
              <div 
                key={notif.notificationId}
                className={`flex items-start gap-3 p-3 transition-colors ${notif.isRead ? 'opacity-70' : 'bg-muted/30'}`}
              >
                <div className={`mt-1 h-2 w-2 shrink-0 rounded-full ${notif.isRead ? 'bg-transparent border border-muted' : getTypeColor(notif.type)}`} />
                <div className="flex-1 space-y-1">
                  <p className="text-sm font-medium leading-none">{notif.title}</p>
                  <p className="text-xs text-muted-foreground line-clamp-2">{notif.message}</p>
                  <p className="text-[10px] text-muted-foreground font-medium pt-1">
                    {formatRelativeTime(notif.createdAt, "recently")}
                  </p>
                </div>
                {!notif.isRead && (
                  <Button 
                    variant="ghost" 
                    size="icon" 
                    className="h-6 w-6 shrink-0 rounded-full opacity-50 hover:opacity-100"
                    onClick={(e) => {
                      void handleMarkRead(e, notif.notificationId);
                    }}
                    title="Mark as read"
                    disabled={pendingNotificationIds.has(notif.notificationId)}
                  >
                    {pendingNotificationIds.has(notif.notificationId) ? (
                      <Loader2 className="h-3 w-3 animate-spin" />
                    ) : (
                      <Check className="h-3 w-3" />
                    )}
                  </Button>
                )}
              </div>
            ))}
          </div>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
