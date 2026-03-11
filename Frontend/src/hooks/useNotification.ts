import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { NotificationDto, UnreadCountResponse } from "../types/notification";
import { api } from "../lib/api";

export const NOTIFICATION_KEYS = {
  all: ["notifications"] as const,
  list: () => [...NOTIFICATION_KEYS.all, "list"] as const,
  unreadCount: () => [...NOTIFICATION_KEYS.all, "unread-count"] as const,
};

// Fetch notifications
export function useNotifications(limit = 50, offset = 0) {
  return useQuery<NotificationDto[]>({
    queryKey: [...NOTIFICATION_KEYS.list(), limit, offset],
    queryFn: async () => {
      // The custom api client already unwraps the response data
      return await api.get<NotificationDto[]>("/notifications?limit=" + limit + "&offset=" + offset);
    },
  });
}

// Polling for unread count
export function useUnreadNotificationCount() {
  return useQuery<UnreadCountResponse>({
    queryKey: NOTIFICATION_KEYS.unreadCount(),
    queryFn: async () => {
      return await api.get<UnreadCountResponse>("/notifications/unread-count");
    },
    refetchInterval: 30000, // Poll every 30s
    // staleTime: 10000,
  });
}

// Mark single notification as read
export function useMarkNotificationRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (notificationId: number) => {
      await api.put(`/notifications/${notificationId}/read`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
    },
  });
}

// Mark all as read
export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      await api.put("/notifications/read-all");
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATION_KEYS.all });
    },
  });
}
